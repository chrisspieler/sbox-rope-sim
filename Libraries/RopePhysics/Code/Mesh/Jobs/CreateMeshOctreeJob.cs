﻿using Sandbox.Diagnostics;

namespace Duccsoft;

internal class CreateMeshOctreeJob : Job<GpuMeshData, SparseVoxelOctree<VoxelSdfData>>
{
	public CreateMeshOctreeJob( int id, GpuMeshData cpuMesh ) : base( id, cpuMesh ) { }

	private const int LEAF_SIZE_LOG2 = 4;
	private const int LEAF_SIZE = 2 << LEAF_SIZE_LOG2 - 1;

	public double FrameTimeBudget { get; set; } = 4.0;
	private int _triProgress = 0;
	private SparseVoxelOctree<VoxelSdfData> Octree { get; set; }
	private Triangle[] Tris { get; set; }

	private SparseVoxelOctree<VoxelSdfData> CreateOctree()
	{
		Log.Info( "Creating octree" );
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
		return new SparseVoxelOctree<VoxelSdfData>( svoSize, octreeDepth );
	}

	private Triangle[] GetTriangles()
	{
		Log.Info( $"Creating triangles" );
		var indices = InputData.CpuMesh.Indices;
		var vertices = InputData.CpuMesh.Vertices;
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
		var triBounds = tri.GetBounds().Grow( 0.001f );
		var voxelMins = Octree.PositionToVoxel( triBounds.Mins );
		var voxelMaxs = Octree.PositionToVoxel( triBounds.Maxs );
		for ( int z = voxelMins.z; z < voxelMaxs.z; z++ )
		{
			for ( int y = voxelMins.y; y < voxelMaxs.y; y++ )
			{
				for ( int x = voxelMins.x; x < voxelMaxs.x; x++ )
				{
					var voxel = new Vector3Int( x, y, z );
					if ( Octree.HasLeaf( voxel ) )
						continue;
					var voxelPos = Octree.VoxelToPosition( voxel );
					
					// Use voxel center as BBox center.
					voxelPos += LEAF_SIZE / 2f;
					var voxelBounds = BBox.FromPositionAndSize( voxelPos, LEAF_SIZE );
					if ( tri.IntersectsAABB( voxelBounds ) )
					{
						Octree.Insert( voxelBounds.Center, null );
					}
				}
			}
		}
	}

	protected override bool RunInternal( out SparseVoxelOctree<VoxelSdfData> result )
	{
		Octree ??= CreateOctree();
		result = Octree;

		Tris ??= GetTriangles();

		var leafCount = Octree.Size / LEAF_SIZE;
		var remainingTime = FrameTimeBudget;

		Log.Info( $"load progress: {_triProgress}" );
		for ( int i = _triProgress; i < Tris.Length; i++ )
		{
			var timer = FastTimer.StartNew();
			InitializeOverlappingLeaves( Tris[i] );
			remainingTime -= timer.ElapsedMilliSeconds;
			if ( remainingTime < 0 )
			{
				_triProgress = ++i;
				Log.Info( $"stored progress {_triProgress}" );
				return false;
			}
		}
		Log.Info( $"l: {LEAF_SIZE}, l2: {LEAF_SIZE_LOG2}, lc: {leafCount}, g: {leafCount * leafCount * leafCount }, t: {Tris.Length}" );
		return true;
	}
}
