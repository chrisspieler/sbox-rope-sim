namespace Duccsoft;

public class CpuMeshData
{
	public CpuMeshData( Vector3[] vertices, uint[] indices )
	{
		Vertices = vertices;
		Indices = indices;
		Bounds = CalculateBounds( Vertices );
	}

	public Vector3[] Vertices { get; }
	public uint[] Indices { get; }
	public int TriangleCount => Indices.Length / 3;
	public BBox Bounds { get; set; }

	private static BBox CalculateBounds( Vector3[] vertices )
	{
		var firstVtx = vertices[0];
		var bbox = BBox.FromPositionAndSize( firstVtx, 0f );
		for ( int i = 1; i < vertices.Length; i++ )
		{
			var vtx = vertices[i];
			bbox = bbox.AddPoint( vtx );
		}
		return bbox;
	}
}
