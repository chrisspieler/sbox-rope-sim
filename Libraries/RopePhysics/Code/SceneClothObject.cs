namespace Duccsoft;

public class SceneClothObject : SceneCustomObject
{
	private static readonly Material ClothShader = Material.FromShader( "shaders/cloth.shader" );

	public GpuBuffer<VerletVertex> Vertices { get; set; }
	public GpuBuffer<uint> Indices { get; set; }

	public SceneClothObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		RenderOverride = Render;
	}

	private void Render( SceneObject so )
	{
		if ( !Vertices.IsValid() || Vertices.ElementCount < 4 || !Indices.IsValid() || Indices.ElementCount < 6 )
			return;

		Graphics.Draw( Vertices, Indices, ClothShader, attributes: Attributes, primitiveType: Graphics.PrimitiveType.Triangles );
	}
}
