namespace Duccsoft;

internal class MeshDistanceSystem : GameObjectSystem<MeshDistanceSystem>
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
			return bbox;
		}
	}

	private readonly Dictionary<int, MeshDistanceField> _meshDistanceFields = new();

	private readonly Dictionary<int, MeshData> _mdfBuildQueue = new();

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

	private void BuildMdf( int id, MeshData data )
	{
		var bounds = data.CalculateMeshBounds();
		var volume = Texture
			// TODO: Calculate texture size appropriate for VoxelsPerWorldUnit.
			// TODO: Do the texture dimensions need to each be a power of two?
			.CreateArray( 32, 32, 32, ImageFormat.RGBA8888 )
			.WithUAVBinding()
			.Finish();
		Log.Info( $"Build MDF ID {id}! Bounds: {bounds}, Volume: {volume.Size.x}x{volume.Size.y}x{volume.Depth}" );
		var mdf = new MeshDistanceField( id, volume, bounds );
		// TODO: Dispatch JFA compute shader to fill texture.
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
