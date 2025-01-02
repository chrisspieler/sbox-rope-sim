﻿namespace Duccsoft;

internal class CreateMeshOctreeJob : Job<CreateMeshOctreeJob.InputData, CreateMeshOctreeJob.OutputData>
{
	public struct InputData
	{
		public MeshDistanceField Mdf;
	}

	public struct OutputData
	{
		public MeshDistanceField Mdf;
		public SparseVoxelOctree<VoxelSdfData> Octree;
		// TODO: Traverse the Octree itself to get this.
		public HashSet<Vector3Int> LeafPoints;
	}

	public CreateMeshOctreeJob( int id, InputData input ) : base( id, input ) { }

	private const int LEAF_SIZE_LOG2 = 4;
	private const int LEAF_SIZE = 2 << LEAF_SIZE_LOG2 - 1;

	public double FrameTimeBudget { get; set; } = 4.0;
	private int _triProgress = 0;
	private SparseVoxelOctree<VoxelSdfData> Octree { get; set; }
	private Triangle[] Tris { get; set; }
	private HashSet<Vector3Int> LeafPoints { get; set; } = new();

	private SparseVoxelOctree<VoxelSdfData> CreateOctree()
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

		var bounds = Input.Mdf.MeshData.Bounds;

		// The size of the octree should fully contain the bounds of the mesh...
		int svoSize = (int)Math.Max( Math.Max( bounds.Size.x, bounds.Size.y ), bounds.Size.z );
		// ...but be rounded up to the nearest power of two.
		svoSize = NearestPowerOf2( svoSize );

		// Determine the difference in powers of two between the size of the octree and that of its leaves.
		int logDiff = (int)Math.Log2( svoSize ) - LEAF_SIZE_LOG2;
		// Ensure that the octree depth is set such that we guarantee a leaf size of 2^LEAF_SIZE_LOG2
		int octreeDepth = Math.Max( 1, logDiff );

		return new SparseVoxelOctree<VoxelSdfData>( svoSize, octreeDepth );
	}

	private Triangle[] GetTriangles()
	{
		var indices = Input.Mdf.MeshData.CpuMesh.Indices;
		var vertices = Input.Mdf.MeshData.CpuMesh.Vertices;
		var tris = new Triangle[indices.Length / 3];
		for ( int i = 0; i < indices.Length; i += 3 )
		{
			uint i0 = indices[i];
			uint i1 = indices[i + 1];
			uint i2 = indices[i + 2];
			Vector3 v0 = vertices[i0];
			Vector3 v1 = vertices[i1];
			Vector3 v2 = vertices[i2];

			tris[i / 3] = new Triangle( v0, v1, v2 );
		}
		return tris;
	}

	private void InitializeOverlappingLeaves( Triangle tri )
	{
		var triBounds = tri.GetBounds().Grow( 1f );
		var voxelMins = Octree.PositionToVoxel( triBounds.Mins );
		voxelMins -= LEAF_SIZE;
		var voxelMaxs = Octree.PositionToVoxel( triBounds.Maxs );
		voxelMaxs += LEAF_SIZE;
		for ( int z = voxelMins.z; z < voxelMaxs.z; z += LEAF_SIZE )
		{
			for ( int y = voxelMins.y; y < voxelMaxs.y; y += LEAF_SIZE )
			{
				for ( int x = voxelMins.x; x < voxelMaxs.x; x += LEAF_SIZE )
				{
					var voxel = new Vector3Int( x, y, z );
					var voxelPos = Octree.VoxelToPosition( voxel );
					
					// Use voxel center as BBox center.
					voxelPos += LEAF_SIZE / 2f;
					var voxelBounds = BBox.FromPositionAndSize( voxelPos, LEAF_SIZE );
					if ( tri.IntersectsAABB( voxelBounds ) )
					{
						LeafPoints.Add( voxel );
					}
				}
			}
		}
	}

	protected override bool RunInternal( out OutputData output )
	{
		Octree ??= CreateOctree();
		output = new OutputData()
		{
			Mdf = Input.Mdf,
			LeafPoints = LeafPoints,
			Octree = Octree
		};

		Tris ??= GetTriangles();

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
