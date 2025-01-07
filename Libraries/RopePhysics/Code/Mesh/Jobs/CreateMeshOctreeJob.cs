using Sandbox.Diagnostics;

namespace Duccsoft;

internal class CreateMeshOctreeJob : Job<CreateMeshOctreeJob.InputData, CreateMeshOctreeJob.OutputData>
{
	public struct InputData
	{
		public MeshDistanceField Mdf;
		public int LeafSize;
	}

	public struct OutputData
	{
		public MeshDistanceField Mdf;
		public SparseVoxelOctree<SignedDistanceField> Octree;
		public Triangle[] Triangles;
		// TODO: Traverse the Octree itself to get this.
		public HashSet<Vector3Int> LeafPoints;
	}

	public CreateMeshOctreeJob( int id, InputData input ) : base( id, input ) { }

	public double FrameTimeBudget { get; set; } = 4.0;
	private int _triProgress = 0;
	private SparseVoxelOctree<SignedDistanceField> Octree { get; set; }
	private Triangle[] Tris { get; set; }
	private HashSet<Vector3Int> LeafPoints { get; set; } = [];

	private SparseVoxelOctree<SignedDistanceField> CreateOctree()
	{
		var bounds = Input.Mdf.MeshData.Bounds;

		// The size of the octree should fully contain the bounds of the mesh...
		int svoSize = (int)Math.Max( Math.Max( bounds.Size.x, bounds.Size.y ), bounds.Size.z );
		// ...but be rounded up to the nearest power of two.
		svoSize = svoSize.NearestPowerOf2();

		int leafSize = Input.LeafSize.NearestPowerOf2();
		// Determine the difference in powers of two between the size of the octree and that of its leaves.
		int logDiff = (int)Math.Log2( svoSize ) - (int)Math.Log2( leafSize );
		// Ensure that the octree depth is set such that we guarantee a leaf size of 2^LEAF_SIZE_LOG2
		int octreeDepth = Math.Max( 1, logDiff );

		return new SparseVoxelOctree<SignedDistanceField>( svoSize, octreeDepth );
	}

	private Triangle[] GetTriangles()
	{
		Assert.IsNull( Tris );

		using var perflog = PerfLog.Scope( Input.Mdf.Id, "GetTriangles" );

		var mesh = Input.Mdf.MeshData.CpuMesh;
		var indices = mesh.Indices;
		var vertices = mesh.Vertices;
		var tris = new Triangle[indices.Length / 3];
		for ( int i = 0; i < indices.Length; i += 3 )
		{
			uint i0 = indices[i];
			uint i1 = indices[i + 1];
			uint i2 = indices[i + 2];
			Vector3 v0 = vertices[i0];
			Vector3 v1 = vertices[i1];
			Vector3 v2 = vertices[i2];

			// Move each vertex so that the origin of the mesh is in the center of the octree.
			v0 -= mesh.Bounds.Center;
			v1 -= mesh.Bounds.Center;
			v2 -= mesh.Bounds.Center;

			var tri = new Triangle( v0, v1, v2 );
			var triIndex = i / 3;
			tris[triIndex] = tri;
		}
		return tris;
	}

	private void InitializeOverlappingLeaves( Triangle tri )
	{
		var triBounds = tri.GetBounds().Grow( 1f );

		int step = Octree.LeafSize;
		Vector3Int mins = Octree.PositionToVoxel( triBounds.Mins );
		Vector3Int maxs = Octree.PositionToVoxel( triBounds.Maxs + step );
		for ( int z = mins.z; z < maxs.z; z += step )
		{
			for ( int y = mins.y; y < maxs.y; y += step )
			{
				for ( int x = mins.x; x < maxs.x; x += step )
				{
					var voxel = new Vector3Int( x, y, z );
					LeafPoints.Add( voxel );
				}
			}
		}
	}

	protected override bool RunInternal( out OutputData output )
	{
		Octree ??= CreateOctree();
		Tris ??= GetTriangles();

		output = new OutputData()
		{
			Mdf = Input.Mdf,
			LeafPoints = LeafPoints,
			Octree = Octree,
			Triangles = Tris
		};

		var timer = new MultiTimer();
		for ( int i = _triProgress; i < Tris.Length; i++ )
		{
			using ( timer.RecordTime() )
			{
				InitializeOverlappingLeaves( Tris[i] );
			}

			if ( timer.TotalMilliseconds > FrameTimeBudget )
			{
				_triProgress = ++i;
				return false;
			}
		}
		return true;
	}
}
