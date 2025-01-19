namespace Duccsoft;

public class SceneClothObject : SceneCustomObject
{
	private static readonly Material ClothShader = Material.FromShader( "shaders/cloth.shader" );

	public GpuBuffer<VerletVertex> Vertices { get; set; }
	public GpuBuffer<uint> Indices { get; set; }

	public bool EnableWireframe
	{
		get => Attributes.GetComboBool( "D_WIREFRAME" );
		set => Attributes.SetCombo( "D_WIREFRAME", value );
	}

	public Color Tint
	{
		get => Attributes.GetVector4( "Tint" );
		set => Attributes.Set( "Tint", value );
	}

	public Material TextureSourceMaterial
	{
		get => _textureSourceMaterial;
		set
		{
			_textureSourceMaterial = value;
			var colorTex = value?.GetTexture( "g_tColor" );
			Attributes.Set( "UseColorTexture", colorTex is not null );
			Attributes.Set( "TexColor", colorTex );
			var normalTex = value?.GetTexture( "g_tNormal" );
			Attributes.Set( "UseNormalTexture", normalTex is not null );
			Attributes.Set( "TexNormal", normalTex );
			var maskTex = value?.GetTexture( "g_tTintMask" );
			Attributes.Set( "UseTintMask", maskTex is not null );
			Attributes.Set( "TexTintMask", maskTex );
			var tint = value?.Attributes?.GetVector4( "g_vColorTint" );
			if ( tint.HasValue )
			{
				Tint = tint.Value with { w = 1 };
			}
			else
			{
				Tint = Vector4.One;
			}
		}
	}
	private Material _textureSourceMaterial;

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
