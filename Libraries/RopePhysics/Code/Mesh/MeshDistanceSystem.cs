namespace Duccsoft;

public class MeshDistanceSystem : GameObjectSystem<MeshDistanceSystem>
{
	[ConVar( "rope_collision_mdf_build_rate" )]
	public static int MaxBuildsPerUpdate { get; set; } = 10;
	[ConVar( "rope_collision_mdf_voxel_density")]
	public static float VoxelsPerWorldUnit { get; set; } = 1;

	public MeshDistanceSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, BuildAllMdfs, "Build Mesh Distance Fields" );
	}

	private struct MeshData
	{
		public GpuBuffer<Vector3> Vertices;
		public GpuBuffer<uint> Indices;
		public Transform Transform;

		public readonly BBox CalculateMeshBounds()
		{
			var vertices = new Vector3[Vertices.ElementCount];
			Vertices.GetData( vertices );
			if ( vertices.Length < 1 )
				return default;

			var bbox = BBox.FromPositionAndSize( vertices[0] );
			for ( int i = 1; i < vertices.Length; i++ )
			{
				bbox = bbox.AddPoint( vertices[i] );
			}
			return bbox.Grow( 1f );
		}
	}

	private readonly Dictionary<int, MeshDistanceField> _meshDistanceFields = new();

	private readonly Dictionary<int, MeshData> _mdfBuildQueue = new();
	private readonly ComputeShader _meshSdfCs = new ComputeShader( "mesh_sdf_cs" );

	public void RemoveMdf( int id )
	{
		_meshDistanceFields.Remove( id );
	}

	private void BuildAllMdfs()
	{
		var built = 0;
		while( _mdfBuildQueue.Count > 0 && built < MaxBuildsPerUpdate )
		{
			(int id, MeshData data ) = _mdfBuildQueue.First();
			BuildMdf( id, data );
			_mdfBuildQueue.Remove( id );
			built++;
		}
	}

	private enum MdfBuildStage
	{
		AddSeedPoints,
		JumpFlood
	}

	private void BuildMdf( int id, MeshData data )
	{
		var bounds = data.CalculateMeshBounds();
		// var size = bounds.Size * 1f;
		var size = new Vector3( 64 );
		var max = MathF.Max( size.x, size.y );
		max = MathF.Max( max, size.z );
		var volume = Texture
			.CreateVolume( (int)max, (int)max, (int)max )
			.WithUAVBinding()
			.Finish();
		Log.Info( $"Build MDF ID {id}! Bounds: {bounds}, Volume: {volume.Size.x}x{volume.Size.y}x{volume.Depth}" );
		_meshSdfCs.Attributes.Set( "Mins", bounds.Mins );
		_meshSdfCs.Attributes.Set( "Maxs", bounds.Maxs );
		_meshSdfCs.Attributes.Set( "Vertices", data.Vertices );
		_meshSdfCs.Attributes.Set( "VertexCount", data.Vertices.ElementCount );
		_meshSdfCs.Attributes.Set( "Indices", data.Indices );
		_meshSdfCs.Attributes.Set( "IndexCount", data.Indices.ElementCount );
		_meshSdfCs.Attributes.Set( "OutputTexture", volume );
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.AddSeedPoints );
		//var dims = (Vector3Int)new Vector3()
		//{
		//	x = MathF.Ceiling( volume.Width / 8.0f ),
		//	y = MathF.Ceiling( volume.Height / 8.0f ),
		//	z = MathF.Ceiling( volume.Depth / 8.0f ),
		//};
		//_meshSdfCs.Dispatch( dims.x, dims.y, dims.z );
		_meshSdfCs.Dispatch( volume.Width, volume.Height, volume.Depth );

		for ( int step = (int)(max / 2 ); step > 0; step /= 2 )
		{
			Log.Info( "jump step " + step );
			_meshSdfCs.Attributes.Set( "JumpStep", step );
			_meshSdfCs.Attributes.Set( "Mins", bounds.Mins );
			_meshSdfCs.Attributes.Set( "Maxs", bounds.Maxs );
			_meshSdfCs.Attributes.Set( "Vertices", data.Vertices );
			_meshSdfCs.Attributes.Set( "VertexCount", data.Vertices.ElementCount );
			_meshSdfCs.Attributes.Set( "Indices", data.Indices );
			_meshSdfCs.Attributes.Set( "IndexCount", data.Indices.ElementCount );
			_meshSdfCs.Attributes.Set( "OutputTexture", volume );
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.JumpFlood );
			_meshSdfCs.Dispatch( volume.Width, volume.Height, volume.Depth );
		}
		var mdf = new MeshDistanceField( id, volume, bounds );
		_meshDistanceFields[id] = mdf;
	}

	public bool TryGetMdf( PhysicsShape shape, out MeshDistanceField meshDistanceField )
	{
		var id = shape.GetHashCode();

		// Did we already build a mesh distance field?
		if ( _meshDistanceFields.TryGetValue( id, out meshDistanceField ) )
			return true;

		// Should we begin building a mesh distance field?
		if ( !_mdfBuildQueue.ContainsKey( id ) )
			AddPhysicsShape( id, shape );
		
		return false;
	}

	private void AddPhysicsShape( int id, PhysicsShape shape )
	{
		shape.Triangulate( out Vector3[] vertices, out uint[] indices );
		Log.Info( $"Queue MDF ID {id}! v: {vertices.Length}, i: {indices.Length}" );
		var vtxBuffer = new GpuBuffer<Vector3>( vertices.Length, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
		vtxBuffer.SetData( vertices, 0 );
		var idxBuffer = new GpuBuffer<uint>( indices.Length, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Index );
		idxBuffer.SetData( indices, 0 );
		var meshData = new MeshData()
		{
			Vertices = vtxBuffer,
			Indices = idxBuffer,
			Transform = shape.Body.Transform,
		};
		_mdfBuildQueue[id] = meshData;
	}
}
