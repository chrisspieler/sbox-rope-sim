namespace Duccsoft;

internal class CreateMeshOctreeJob : Job<GpuMeshData, SparseVoxelOctree<VoxelSdfData>>
{
	public CreateMeshOctreeJob( int id, GpuMeshData cpuMesh ) : base( id, cpuMesh ) { }

	private const int LEAF_SIZE_LOG2 = 4;

	protected override bool RunInternal( out SparseVoxelOctree<VoxelSdfData> result )
	{
		 static int NearestPowerOf2( int v )
		{
			v--;
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			v++;
			return v;
		}

		var bounds = InputData.Bounds;

		// The size of the octree should fully contain the bounds of the mesh...
		int svoSize = (int)Math.Max( Math.Max( bounds.Size.x, bounds.Size.y ), bounds.Size.z );
		// ...but be rounded up to the nearest power of two.
		svoSize = NearestPowerOf2( svoSize );

		// Determine the difference in powers of two between the size of the octree and that of its leaves.
		int logDiff = (int)Math.Log2( svoSize ) - LEAF_SIZE_LOG2;
		// Ensure that the octree depth is set such that we guarantee a leaf size of 2^LEAF_SIZE_LOG2
		int octreeDepth = Math.Max( 1, logDiff );

		Log.Info( $"Create octree of size {svoSize} and depth {octreeDepth}" );
		result = new SparseVoxelOctree<VoxelSdfData>( svoSize, octreeDepth );

		for ( int i = 0; i < InputData.CpuMesh.Vertices.Length; i++ )
		{
			var vtx = InputData.CpuMesh.Vertices[i];
			result.Insert( vtx, null );
		}
		return true;
	}
}
