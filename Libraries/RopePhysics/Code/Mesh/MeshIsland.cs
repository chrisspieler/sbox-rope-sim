namespace Duccsoft;

public class MeshIsland
{
	private readonly struct MeshTriFace
	{
		public MeshTriFace( uint i0, uint i1, uint i2, Vector3 v0, Vector3 v1, Vector3 v2 )
		{
			Index0 = i0;
			Index1 = i1;
			Index2 = i2;
			Vertex0 = v0;
			Vertex1 = v1;
			Vertex2 = v2;
		}

		public uint Index0 { get; }
		public uint Index1 { get; }
		public uint Index2 { get; }
		public Vector3 Vertex0 { get; }
		public Vector3 Vertex1 { get; }
		public Vector3 Vertex2 { get; }

		public readonly MeshEdge Edge01 => new( Index0, Index1 );
		public readonly MeshEdge Edge12 => new( Index1, Index2 );
		public readonly MeshEdge Edge20 => new( Index2, Index0 );

		public override int GetHashCode() => HashCode.Combine( Index0, Index1, Index2 );
	}

	private readonly struct MeshEdge
	{
		public MeshEdge( uint index0, uint index1 )
		{
			Index0 = index0;
			Index1 = index1;
		}

		public uint Index0 { get; }
		public uint Index1 { get; }
		public override int GetHashCode() => HashCode.Combine( Index0, Index1 );
	}

	private MeshIsland() { }

	public MeshIsland( int indexCount )
	{
		Vertices = [];
		Indices = new uint[indexCount];
		Faces = new MeshTriFace[indexCount / 3];
		Edges = [];
	}

	public Dictionary<uint, Vector3> Vertices { get; }
	public uint[] Indices { get; }
	private int IndexCount { get; set; } = 0;
	private MeshTriFace[] Faces { get; }
	private HashSet<MeshEdge> Edges { get; }

	public int EulerCharacteristic => Indices.Length + Faces.Length - Edges.Count;

	public static IEnumerable<MeshIsland> GetIslands( Vector3[] vertices, uint[] indices )
	{
		var unionFind = new DisjointSet<uint>();
		var idxMap = new Dictionary<uint, DisjointSet<uint>.Node>();

		// Associate each vertex index with a root node in the disjoint-set.
		for ( int i = 0; i < indices.Length; i++ )
		{
			var index = indices[i];
			var ufNode = unionFind.MakeSet( index );
			idxMap[index] = ufNode;
		}

		var allTris = new MeshTriFace[indices.Length / 3];

		// Merge both vertices each edge in to the same set, store the face for later.
		for ( int i = 0; i < indices.Length; i += 3 )
		{
			var i0 = indices[i];
			var i1 = indices[i + 1];
			var i2 = indices[i + 2];

			var ufNode0 = idxMap[i0];
			var ufNode1 = idxMap[i1];
			var ufNode2 = idxMap[i2];

			var v0 = vertices[i0];
			var v1 = vertices[i1];
			var v2 = vertices[i2];

			allTris[i / 3] = new MeshTriFace( i0, i1, i2, v0, v1, v2 );

			unionFind.Union( ufNode0, ufNode1 );
			unionFind.Union( ufNode1, ufNode2 );
			unionFind.Union( ufNode2, ufNode0 );
		}

		var islands = new Dictionary<DisjointSet<uint>.Node, MeshIsland>();

		// Split the set of all triangles in to individual mesh islands.
		foreach( var tri in allTris )
		{
			// Figure out which island this index belongs to.
			var root = unionFind.Find( idxMap[tri.Index0] );
			if ( !islands.TryGetValue( root, out MeshIsland island ) )
			{
				// The disjoint-set has one node each index in the mesh island.
				// Use the size of the root node as the island's index count.
				island = new MeshIsland( root.Size );
				islands[root] = island;
			}

			var i = island.IndexCount;
			island.Faces[i / 3] = tri;
			island.Indices[i] = tri.Index0;
			island.Indices[i + 1] = tri.Index1;
			island.Indices[i + 2] = tri.Index2;
			// Vertex indices will change if we are splitting the mesh in to subsets.
			// Store the vertices in a Dictionary to work around this.
			island.Vertices[tri.Index0] = indices[tri.Index0];
			island.Vertices[tri.Index1] = indices[tri.Index1];
			island.Vertices[tri.Index2] = indices[tri.Index2];
			island.IndexCount += 3;

			// Multiple triangles may share the same edge, so store edges in a HashMap.
			island.Edges.Add( tri.Edge01 );
			island.Edges.Add( tri.Edge12 );
			island.Edges.Add( tri.Edge20 );
		}

		foreach( var island in islands.Values )
		{
			yield return island;
		}
	}
}
