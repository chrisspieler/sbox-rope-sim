namespace Duccsoft;

public class SparseVoxelOctree<T>
{
	public class OctreeNode
	{
		public OctreeNode( Vector3Int position, int size )
		{
			Position = position;
			Size = size;
		}

		public T Data;
		public bool IsLeaf { get; set; }
		public OctreeNode this[int index]
		{
			get => Children[index];
			set => Children[index] = value;
		}
		public Vector3Int Position { get; private set; }
		public int Size { get; private set; }
		public bool ContainsVoxel( Vector3Int voxel )
		{
			var maxs = Position + ( Size - 1 );
			return voxel.x >= Position.x && voxel.y >= Position.y && voxel.z >= Position.z
				&& voxel.x <= maxs.x && voxel.y <= maxs.y && voxel.z <= maxs.z;
		}

		public readonly OctreeNode[] Children = [null, null, null, null, null, null, null, null];
	}

	public SparseVoxelOctree( int size, int maxDepth )
	{
		Size = size;
		MaxDepth = maxDepth;
		RootNode = new OctreeNode( Vector3Int.Zero, Size );
	}

	public int Size { get; private set; }
	public int MaxDepth { get; private set; }
	public int LeafSize => Size >> MaxDepth;
	public OctreeNode RootNode { get; private set; }

	public bool ContainsPoint( Vector3 localPos )
	{
		var voxel = PositionToVoxel( localPos );
		return RootNode.ContainsVoxel( voxel );
	}

	public Vector3Int PositionToVoxel( Vector3 localPos )
	{
		// Remap -halfSize to halfSize -> 0 to size
		var voxel = (Vector3Int)(localPos + Size / 2f);
		return voxel.SnapToGrid( LeafSize );
	}

	public Vector3 VoxelToPosition( Vector3Int voxel )
	{
		return (Vector3)voxel - Size / 2f;
	}

	public BBox GetVoxelBounds( Vector3Int voxel )
	{
		var mins = VoxelToPosition( voxel );
		var maxs = mins + LeafSize * MeshDistanceSystem.VoxelSize;
		return new BBox( mins, maxs );
	}

	public bool HasLeaf( Vector3 point )
	{
		var normalized = PositionToVoxel( point );
		return HasLeaf( normalized );
	}

	public bool HasLeaf( Vector3Int leaf )
	{
		var found = FindRecursive( RootNode, leaf, 0 );
		return found.Size == LeafSize;
	}

	public OctreeNode Trace( Vector3 point, Vector3 direction, out float hitDistance )
	{
		return TraceRecursive( RootNode, point, direction, 0f, out hitDistance );
	}

	private struct RayTraceResult
	{
		public OctreeNode Node;
		public float Distance;
	}

	private OctreeNode TraceRecursive( OctreeNode node, Vector3 point, Vector3 direction, float lastDistance, out float hitDistance )
	{
		hitDistance = lastDistance;
		if ( node.IsLeaf && node.Data is not null )
		{
			return node;
		}

		var hits = new List<RayTraceResult>();
		for ( int i = 0; i < 8; i++ )
		{
			var child = node[i];
			if ( child is null )
				continue;

			var ray = new Ray( point, direction );
			var childLocalPos = VoxelToPosition( child.Position );
			var bounds = BBox.FromPositionAndSize( childLocalPos + child.Size * 0.5f, child.Size );
			var hit = bounds.Trace( ray, 1e20f, out float childDistance );
			if ( !hit )
			{
				continue;
			}

			hits.Add( new RayTraceResult()
			{
				Node = child,
				Distance = childDistance,
			});
		}

		if ( hits.Count < 1 )
		{
			return null;
		}
		var sorted = hits.OrderBy( h => h.Distance );
		foreach( var hit in sorted )
		{
			var foundNode = TraceRecursive( hit.Node, point, direction, hit.Distance, out hitDistance );
			if ( foundNode is not null )
				return foundNode;
		}
		return null;
	}

	public OctreeNode Find( Vector3 point )
	{
		var normalized = PositionToVoxel( point );
		return FindRecursive( RootNode, normalized, 0 );
	}

	public OctreeNode Find( Vector3Int voxel ) => FindRecursive( RootNode, voxel, 0 );

	private OctreeNode FindRecursive( OctreeNode node, Vector3Int point, int depth )
	{
		if ( node.IsLeaf || depth >= MaxDepth )
			return node;

		int childSize = node.Size / 2;
		int childIndex = GetChildIndex( point, node.Position, childSize );
		if ( node[childIndex] is null )
			return node;

		return FindRecursive( node[childIndex], point, depth + 1 );
	}

	private static int GetChildIndex( Vector3Int point, Vector3Int parentPosition, int childSize )
	{
		int x = point.x >= parentPosition.x + childSize ? 1 : 0;
		int y = point.y >= parentPosition.y + childSize ? 1 : 0;
		int z = point.z >= parentPosition.z + childSize ? 1 : 0;
		// Each node contains 8 children, so the index is octal.
		return (x << 2) | (y << 1) | z;
	}

	private static Vector3Int CalculateChildPosition( Vector3Int parentPosition, int childSize, int childIndex )
	{
		int x = parentPosition.x + ((childIndex & 4) >> 2) * childSize;
		int y = parentPosition.y + ((childIndex & 2) >> 1) * childSize;
		int z = parentPosition.z + (childIndex & 1) * childSize;
		return new Vector3Int( x, y, z );
	}


	public void Insert( Vector3 point, T data )
	{
		var voxel = PositionToVoxel( point );
		InsertRecursive( RootNode, voxel, data, 0 );
	}

	public void Insert( Vector3Int voxel, T data ) => InsertRecursive( RootNode, voxel, data, 0 );

	private void InsertRecursive( OctreeNode node, Vector3Int point, T data, int depth )
	{
		node.Data = data;
		if ( depth == MaxDepth )
		{
			node.IsLeaf = true;
			return;
		}

		int childSize = node.Size / 2;
		int childIndex = GetChildIndex( point, node.Position, childSize );

		if ( node[childIndex] is null )
		{
			Vector3Int childPosition = CalculateChildPosition( node.Position, childSize, childIndex );
			node[childIndex] = new OctreeNode( childPosition, childSize );
			node.IsLeaf = false;
		}

		InsertRecursive( node[childIndex], point, data, depth + 1 );
	}
}
