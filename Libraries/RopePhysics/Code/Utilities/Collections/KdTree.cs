using System.Collections;

namespace Duccsoft;

public class KdTree<T>
{
	private class Node
	{
        public Node( Vector3 position, int depth, T data )
        {
			Position = position;
			Depth = depth;
			Data = data;
        }

        public Vector3 Position { get; set; }
		public T Data { get; set; }
		public int Depth { get; set; }
		public int Axis => Depth % 3;
		public Node Left { get; set; }
		public Node Right { get; set; }
	}

	public struct NodeSearchResult
	{
		public bool WasSuccessful { get; set; }
		public Vector3 NodePosition { get; set; }
		public float Distance { get; set; }
		public T Data { get; set; }
	}

	private Node RootNode { get; set; }

    public object Current => throw new NotImplementedException();

    public void Insert( Vector3 position, T data )
	{
		RootNode = InsertRecursive( RootNode, position, data, 0 );
	}

	private Node InsertRecursive( Node node, Vector3 position, T data, int depth, bool replaceOnOverlap = true )
	{
		if ( node == null )
		{
			return new Node( position, depth, data );
		}

		float axisCoord = position[node.Axis];
		float nodeCoord = node.Position[node.Axis];

		if ( MathF.Abs( axisCoord - nodeCoord ) < float.Epsilon )
		{
			if ( replaceOnOverlap )
			{
				// Overwrite whatever node might exist at the exact same position.
				node.Data = data;
			}
			return node;
		}

		if ( axisCoord < nodeCoord )
		{
			node.Left = InsertRecursive( node.Left, position, data, depth + 1 );
		}
		else
		{
			node.Right = InsertRecursive( node.Right, position, data, depth + 1 );
		}

		return node;
	}

	public void Remove( Vector3 position )
	{
		throw new NotImplementedException();
	}

	public bool TryGetNearestNeighbor( Vector3 position, out NodeSearchResult nodeData )
	{
		if ( RootNode is null )
		{
			nodeData = default;
			return false;
		}

		NodeSearchResult searchResult = new NodeSearchResult()
		{
			Distance = float.MaxValue
		};

		NodeSearchResult foundNode = default;
		searchResult.WasSuccessful = TryGetNearestNeighborRecursive( RootNode, position, ref foundNode );

		nodeData = foundNode;
		return searchResult.WasSuccessful;
	}

	private bool TryGetNearestNeighborRecursive( Node node, Vector3 position, ref NodeSearchResult searchResult )
	{
		if ( node is null )
		{
			return false;
		}

		float distance = position.Distance( node.Position );
		if ( distance < searchResult.Distance)
		{
			searchResult.Distance = distance;
			searchResult.NodePosition = node.Position;
			searchResult.Data = node.Data;
		}

		float axisCoord = position[node.Axis];
		float nodeCoord = node.Position[node.Axis];

		Node first = axisCoord < nodeCoord ? node.Left : node.Right;
		Node second = axisCoord < nodeCoord ? node.Right : node.Left;

		var foundFirst = TryGetNearestNeighborRecursive( first, position, ref searchResult );
		var foundSecond = false;


		float axisDist = MathF.Abs( axisCoord - nodeCoord );
		if ( axisDist < searchResult.Distance )
		{
			foundSecond = TryGetNearestNeighborRecursive( second, position, ref searchResult );
		}
		return foundFirst || foundSecond;
	}
}
