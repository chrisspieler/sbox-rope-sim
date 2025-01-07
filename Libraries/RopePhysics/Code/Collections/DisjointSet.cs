namespace Duccsoft;

// Adapted from pseudocode on Wikipedia: https://en.wikipedia.org/wiki/Disjoint-set_data_structure
public class DisjointSet<T>
{
	public class Node
	{
		public Node Parent { get; set; }
		public int Size { get; set; }
		public T Data { get; set; }
		public bool IsRoot => Parent == this;

		public override int GetHashCode() => Data.GetHashCode();
	}

	public Node MakeSet( T data )
	{
		var root = new Node();
		// The root node is its own parent.
		root.Parent = root;
		root.Size = 1;
		root.Data = data;
		Sets.Add( root );
		return root;
	}

	public Node Find( Node node )
	{
		if ( node.IsRoot )
			return node;

		// This version of Find uses path compression, speeding up subsequent
		// finds by moving the parent pointer closer to root.
		node.Parent = Find( node.Parent );
		return node.Parent;
	}

	public Node Union( Node x, Node y )
	{
		x = Find( x );
		y = Find( y );

		if ( x == y )
			return x;

		if ( x.Size < y.Size )
		{
			(x, y) = (y, x);
		}

		y.Parent = x;
		x.Size += y.Size;

		return x;
	}

	public IEnumerable<Node> Forest => Sets;
	private HashSet<Node> Sets { get; set; } = [];
}
