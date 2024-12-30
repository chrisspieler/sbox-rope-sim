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
		RootNode = new OctreeNode( Vector3Int.Zero, Size );
	}

	public int Size { get; private set; }
	public int MaxDepth { get; private set; }
	public OctreeNode RootNode { get; private set; }
	public Vector3Int PositionToVoxel( Vector3 localPos )
	{
		return (Vector3Int)(localPos + Size / 2f);
	}

	public Vector3 VoxelToPosition( Vector3Int voxel )
	{
		return (Vector3)voxel - Size / 2f;
	}

	public bool HasLeaf( Vector3 point )
	{
		var normalized = PositionToVoxel( point );
		return HasLeaf( normalized );
	}

	public bool HasLeaf( Vector3Int leaf )
	{
		var found = FindRecursive( RootNode, leaf, 0 );
		return found.Size == Size >> MaxDepth;
	}

	public OctreeNode Find( Vector3 point )
	{
		var normalized = PositionToVoxel( point );
		return FindRecursive( RootNode, normalized, 0 );
	}

	private OctreeNode FindRecursive( OctreeNode node, Vector3Int point, int depth )
	{
		if ( node.IsLeaf || depth >= MaxDepth )
			return node;

		int childSize = node.Size / 2;
		int childIndex = GetChildIndex( point, node.Position, childSize );
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
		var normalized = PositionToVoxel( point );
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

	public void DebugDraw( Transform tx )
	{
		var overlay = DebugOverlaySystem.Current;

		if ( RootNode is null || overlay is null)
			return;

		DrawChildren( RootNode );

		void DrawChildren( OctreeNode node )
		{
			var pos = node.Position - Size / 2;
			var bbox = new BBox( pos, pos + node.Size );
			var color = Color.Blue.WithAlpha( 0.15f );
			if ( node.IsLeaf )
			{
				if ( node.Data is null )
				{
					color = Color.Red.WithAlpha( 0.5f );
				}
				else
				{
					color = Color.Yellow.WithAlpha( 0.35f );
				}
			}
			overlay.Box( bbox, color, transform: tx );
			foreach ( var child in node.Children )
			{
				if ( child is null )
					continue;

				DrawChildren( child );
			}
		}
	}
}
