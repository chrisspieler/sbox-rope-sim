namespace Duccsoft;

public struct VerletVertex
{
	[VertexLayout.Position]
	public Vector4 Position;
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
}
