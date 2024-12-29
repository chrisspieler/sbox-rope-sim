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

		public readonly OctreeNode[] Children = [null, null, null, null, null, null, null, null];
	}

	public SparseVoxelOctree( int size, int maxDepth )
	{
		Size = size;
		MaxDepth = maxDepth;
	}

	public int Size { get; private set; }
	public int MaxDepth { get; private set; }
	public OctreeNode RootNode { get; private set; }

	public OctreeNode Find( Vector3 point )
	{
		var normalized = (Vector2Int)(point + Size / 2);
		return FindRecursive( RootNode, normalized, 0 );
	}

	private OctreeNode FindRecursive( OctreeNode node, Vector3Int point, int depth )
	{
		if ( node.IsLeaf || depth >= MaxDepth )
			return node;

		int childIndex = GetChildIndex( point, node.Position, node.Size / 2 );
		if ( node[childIndex] is null )
			return node;

		return FindRecursive( node, point, depth + 1 );
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
		RootNode ??= new OctreeNode( Vector3Int.Zero, Size );
		var normalized = (Vector3Int)( point + Size / 2 );
		InsertRecursive( RootNode, normalized, data, 0 );
	}

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
