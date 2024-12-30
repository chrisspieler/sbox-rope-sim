namespace Duccsoft;

public class CpuMeshData
{
	public CpuMeshData( Vector3[] vertices, uint[] indices, BBox bounds = default )
	{
		Vertices = vertices;
		Indices = indices;
		if ( bounds == default )
		{
			Bounds = CalculateBounds( Vertices );
		}
	}

	public Vector3[] Vertices { get; }
	public uint[] Indices { get; }
	public BBox Bounds { get; set; }

	private static BBox CalculateBounds( Vector3[] vertices )
	{
		var bbox = BBox.FromPositionAndSize( Vector3.Zero, 16f );

		if ( vertices.Length > 1 )
		{
			bbox = BBox.FromPositionAndSize( vertices[0] );
			for ( int i = 1; i < vertices.Length; i++ )
			{
				bbox = bbox.AddPoint( vertices[i] );
			}
			bbox = bbox.Grow( 4f );
		}
		return bbox;
	}
}
