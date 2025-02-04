﻿using Duccsoft.ImGui.Rendering;

namespace Duccsoft.ImGui.Elements;

public class ImageWidget : Element
{
    public ImageWidget( Window parent, Texture texture, Vector2 size, 
		Vector2 uv0, Vector2 uv1, Color tintColor, Color borderColor,
		ImDrawList.ImageTextureFiltering textureFiltering, float angle ) 
		: base( parent )
    {
		ColorTexture = texture;
		ImageSize = size;
		UV0 = uv0;
		UV1 = uv1;
		TintColor = tintColor;
		BorderColor = borderColor;
		TextureFiltering = textureFiltering;
		Angle = angle;

		Size = ImageSize;

		OnBegin();
		OnEnd();
    }

	public Texture ColorTexture { get; set; }
	public Vector2 ImageSize { get; set; }
	public Vector2 UV0 { get; set; }
	public Vector2 UV1 { get; set; }
	public Color TintColor { get; set; }
	public Color BorderColor { get; set; }
	public ImDrawList.ImageTextureFiltering TextureFiltering { get; set; } = ImDrawList.ImageTextureFiltering.Anisotropic;
	public float Angle { get; set; } = 0f;

	protected override void OnDrawSelf( ImDrawList drawList )
	{
		drawList.AddRect( ScreenRect.TopLeft, ScreenRect.BottomRight, BorderColor, rounding: 0f, flags: ImDrawFlags.None, thickness: 2f );
		drawList.AddImage( ColorTexture, ScreenRect.TopLeft, ScreenRect.BottomRight, UV0, UV1, TintColor, TextureFiltering, -Angle );
	}
}
