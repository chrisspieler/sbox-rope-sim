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
		JumpFlood,
		DebugNormalized
	}

	private void BuildMdf( int id, MeshData data )
	{
		var bounds = data.CalculateMeshBounds();
		// var size = bounds.Size * 1f;
		var size = new Vector3( 128 );
		var max = MathF.Max( size.x, size.y );
		max = MathF.Max( max, size.z );
		var volume = Texture
			.CreateVolume( (int)max, (int)max, (int)max, ImageFormat.RGBA32323232F )
			.WithUAVBinding()
			.Finish();

		Log.Info( $"Build MDF ID {id}! Bounds: {bounds}, Volume: {volume.Size.x}x{volume.Size.y}x{volume.Depth}" );
		DispatchBuildShader( volume, data, bounds );
		var mdf = new MeshDistanceField( id, volume, bounds );
		_meshDistanceFields[id] = mdf;
	}

	private void DispatchBuildShader( Texture volume, MeshData data, BBox bounds )
	{
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.AddSeedPoints );
		_meshSdfCs.Attributes.Set( "Mins", bounds.Mins );
		_meshSdfCs.Attributes.Set( "Maxs", bounds.Maxs );
		_meshSdfCs.Attributes.Set( "Vertices", data.Vertices );
		_meshSdfCs.Attributes.Set( "VertexCount", data.Vertices.ElementCount );
		_meshSdfCs.Attributes.Set( "Indices", data.Indices );
		_meshSdfCs.Attributes.Set( "IndexCount", data.Indices.ElementCount );
		_meshSdfCs.Attributes.Set( "OutputTexture", volume );
		_meshSdfCs.Dispatch( volume.Width, volume.Height, volume.Depth );

		// The max step size will be half of the size of the largest dimension of the volume texture.
		var max = Math.Max( volume.Width, volume.Height );
		max = Math.Max( max, volume.Depth );

		for ( int step = max / 2; step > 0; step /= 2 )
		{
			Log.Info( "jump step " + step );
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.JumpFlood );
			_meshSdfCs.Attributes.Set( "JumpStep", step );
			_meshSdfCs.Dispatch( volume.Width, volume.Height, volume.Depth );
		}

		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.DebugNormalized );
		_meshSdfCs.Dispatch( volume.Width, volume.Height, volume.Depth );
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
		Log.Info( $"Queue MDF ID {id}! v: {vertices.Length}, i: {indices.Length}, t: {indices.Length / 3}" );
		// DebugLogMesh( vertices, indices, shape );
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

	private void DebugLogMesh( Vector3[] vertices, uint[] indices, PhysicsShape shape )
	{
		for ( int i = 0; i < indices.Length; i += 3 )
		{
			var i0 = indices[i];
			var i1 = indices[i + 1];
			var i2 = indices[i + 2];
			var v0 = vertices[i0];
			var v1 = vertices[i1];
			var v2 = vertices[i2];

			DebugOverlaySystem.Current.Sphere( new Sphere( v0, 1f ), color: Color.Red, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Sphere( new Sphere( v1, 1f ), color: Color.Red, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Sphere( new Sphere( v2, 1f ), color: Color.Red, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v0, v1, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v1, v2, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v2, v0, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			Log.Info( $"i ({i0},{i1},{i2}), v: ({v0},{v1},{v2})" );
		}
	}
}
