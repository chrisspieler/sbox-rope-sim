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
		public GpuBuffer<Vector4> Vertices;
		public GpuBuffer<uint> Indices;
		public Transform Transform;

		public readonly BBox CalculateMeshBounds()
		{
			var vertices = new Vector4[Vertices.ElementCount];
			Vertices.GetData( vertices );
			if ( vertices.Length < 1 )
				return default;

			var bbox = BBox.FromPositionAndSize( vertices[0] );
			for ( int i = 1; i < vertices.Length; i++ )
			{
				bbox = bbox.AddPoint( vertices[i] );
			}
			return bbox.Grow( 2f );
		}
	}

	private readonly Dictionary<int, MeshDistanceField> _meshDistanceFields = new();

	private readonly Dictionary<int, MeshData> _mdfBuildQueue = new();
	private readonly ComputeShader _meshSdfCs = new( "mesh_sdf_cs" );

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
		InitializeVolume,
		FindSeeds,
		InitializeSeeds,
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

		// Log.Info( $"Build MDF ID {id}! Bounds: {bounds}, Volume: {volume.Size.x}x{volume.Size.y}x{volume.Depth}" );
		DispatchBuildShader( volume, data, bounds );
		var mdf = new MeshDistanceField( id, volume, bounds );
		_meshDistanceFields[id] = mdf;
	}

	private void DispatchBuildShader( Texture volume, MeshData data, BBox bounds )
	{
		int triCount = data.Indices.ElementCount / 3;
		int voxelCount = volume.Width * volume.Height * volume.Depth;

		var cellDataSize = 2;
		var cellData = new GpuBuffer<Vector4>( cellDataSize * voxelCount, GpuBuffer.UsageFlags.Structured );
		// Initialize each voxel of the volume texture and cell data.
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.InitializeVolume );
		_meshSdfCs.Attributes.Set( "Mins", bounds.Mins );
		_meshSdfCs.Attributes.Set( "Maxs", bounds.Maxs );
		_meshSdfCs.Attributes.Set( "Data", cellData );
		_meshSdfCs.Attributes.Set( "OutputTexture", volume );
		_meshSdfCs.Dispatch( volume.Width, volume.Height, volume.Depth );

		var seedCount = triCount;// * 4;
		var seedDataSize = 2;
		var seedData = new GpuBuffer<Vector4>( seedDataSize * seedCount, GpuBuffer.UsageFlags.Structured );
		// For each triangle, find a seed value.
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.FindSeeds );
		_meshSdfCs.Attributes.Set( "Vertices", data.Vertices );
		_meshSdfCs.Attributes.Set( "Indices", data.Indices );
		_meshSdfCs.Attributes.Set( "Seeds", seedData );
		_meshSdfCs.Dispatch( threadsX: triCount );

		// For each seed we found, write it to the volume data.
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.InitializeSeeds );
		_meshSdfCs.Dispatch( threadsX: seedCount );

		//var retData = new Vector4[seedData.ElementCount];
		//seedData.GetData( retData );
		//for ( int i = 0; i < retData.Length; i += 2 )
		//{
		//	Vector4 positionOs = retData[i];
		//	Vector4 normal = retData[i + 1];
		//	DebugOverlaySystem.Current.Sphere( new Sphere( bounds.Center + positionOs, 1f ), color: Color.Cyan, duration: 5f, transform: data.Transform, overlay: true );
		//	Log.Info( $"# {i / 2} pOs: {positionOs}, nor: {normal}" );
		//}

		// Run a jump flooding algorithm to disperse information about the mesh to each texel/voxel.
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.JumpFlood );
		var max = Math.Max( volume.Width, volume.Height );
		max = Math.Max( max, volume.Depth );
		for ( int step = max / 2; step > 0; step /= 2 )
		{
			_meshSdfCs.Attributes.Set( "JumpStep", step );
			_meshSdfCs.Dispatch( volume.Width, volume.Height, volume.Depth );
		}


		// Debug visualization - uncomment to see the data normalized for display.
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
		shape.Triangulate( out Vector3[] vtx3, out uint[] indices );
		var vertices = vtx3.Select( v => new Vector4( v.x, v.y, v.z, 0 ) ).ToArray();
		// Log.Info( $"Queue MDF ID {id}! v: {vertices.Length}, i: {indices.Length}, t: {indices.Length / 3}" );
		// DebugLogMesh( vertices, indices, shape );
		var vtxBuffer = new GpuBuffer<Vector4>( vertices.Length, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
		vtxBuffer.SetData( vertices );
		var idxBuffer = new GpuBuffer<uint>( indices.Length, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Index );
		idxBuffer.SetData( indices );
		var meshData = new MeshData()
		{
			Vertices = vtxBuffer,
			Indices = idxBuffer,
			Transform = shape.Body.Transform,
		};
		_mdfBuildQueue[id] = meshData;
	}

	private void DebugLogMesh( Vector4[] vertices, uint[] indices, PhysicsShape shape )
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
			DebugOverlaySystem.Current.Sphere( new Sphere( ( v0 + v1 + v2 ) / 3.0f, 1f ), color: Color.Orange, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v0, v1, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v1, v2, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v2, v0, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			Log.Info( $"i (0:{i0} 1:{i1} 2:{i2}), v: (0:{v0} 1:{v1} 2:{v2})" );
		}
	}
}
