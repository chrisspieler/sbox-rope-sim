namespace Duccsoft;

public class SceneClothObject : SceneCustomObject
{
	private static readonly Material ClothShader = Material.FromShader( "shaders/cloth.shader" );

	public GpuBuffer<VerletVertex> Vertices { get; set; }

	public SceneClothObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		RenderOverride = Render;
	}

	private void Render( SceneObject so )
	{
		if ( !Vertices.IsValid() || Vertices.ElementCount < 4 )
			return;

		Graphics.Draw( Vertices, ClothShader, startVertex: 0, vertexCount: 0, attributes: Attributes, primitiveType: Graphics.PrimitiveType.LineStrip );
	}
}
