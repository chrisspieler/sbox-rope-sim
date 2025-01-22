namespace Duccsoft;

public struct VerletVertex
{
	[VertexLayout.Position]
	public Vector3 Position;
	[VertexLayout.TexCoord( 0 )]
	public Vector4 TexCoord0;
	[VertexLayout.Normal]
	public Vector4 Normal;
	[VertexLayout.Tangent( 0 )]
	public Vector4 Tangent0;
	[VertexLayout.TexCoord( 1 )]
	public Vector4 TexCoord1;
	[VertexLayout.Color( 0 )]
	public Vector4 Color0;

	public static VertexAttribute[] Layout = [
		new VertexAttribute( VertexAttributeType.Position, VertexAttributeFormat.Float32, 3, 0 ),
		new VertexAttribute( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 4, 0 ),
		new VertexAttribute( VertexAttributeType.Normal, VertexAttributeFormat.Float32, 4, 0 ),
		new VertexAttribute( VertexAttributeType.Tangent, VertexAttributeFormat.Float32, 4, 0 ),
		new VertexAttribute( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 4, 1 ),
		new VertexAttribute( VertexAttributeType.Color, VertexAttributeFormat.Float32, 4, 0 ),
	];
}
