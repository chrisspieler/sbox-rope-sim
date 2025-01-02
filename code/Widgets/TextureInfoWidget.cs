using Duccsoft;
using Duccsoft.ImGui;
using Duccsoft.ImGui.Elements;
using Duccsoft.ImGui.Extensions;
using Duccsoft.ImGui.Rendering;

public class TextureInfoWidget : ImageWidget
{
	public TextureInfoWidget( Window parent, VoxelSdfData voxelData, int textureSlice, Texture texture, 
		Vector2 size, Vector2 uv0, Vector2 uv1, Color tintColor, Color borderColor, 
		ImDrawList.ImageTextureFiltering textureFiltering, float angle ) 
		: base( parent, texture, size, uv0, uv1, tintColor, borderColor, textureFiltering, angle )
	{
		VoxelData = voxelData;
		TextureSlice = textureSlice;
	}

	public VoxelSdfData VoxelData { get; }
	public int TextureSlice { get; }
	public Vector2 MouseLocalPos { get; private set; }
	public Vector2? HoveredNormal { get; private set; }
	public Vector2Int? HoveredPixel { get; private set; }
	public Vector2 HoveredPixelScreenPos => PixelToScreenPos( HoveredPixel ?? 0 );
	public Vector2 PixelSize => ImageSize / ColorTexture.Size;
	public Vector2Int? SelectedPixel { get; set; }
	public Vector2 SelectedPixelScreenPos => PixelToScreenPos( SelectedPixel ?? 0 );

	public Vector2 PixelToScreenPos( Vector2Int pixel )
	{
		return ScreenPosition + ImageSize * PixelToNormal( pixel );
	}

	public Vector2 PixelToNormal( Vector2Int pixel )
	{
		var normal = (Vector2)pixel / ColorTexture.Size;
		normal = normal.RotateUV( Angle );
		normal.x = MathX.Remap( normal.x, 0, 1, UV0.x, UV1.x );
		normal.y = MathX.Remap( normal.y, 0, 1, UV0.y, UV1.y );
		return normal;
	}

	public Vector2 ScreenPosToNormal( Vector2 screenPos )
	{
		var localPos = screenPos - ScreenPosition;
		var normal = localPos / ImageSize;
		normal = normal.RotateUV( Angle );
		normal.x = MathX.Remap( normal.x, 0, 1, UV0.x, UV1.x );
		normal.y = MathX.Remap( normal.y, 0, 1, UV0.y, UV1.y );
		return normal;
	}

	public Vector2Int ScreenPosToPixel( Vector2 screenPos )
	{
		var texel = ScreenPosToNormal( screenPos ) * ColorTexture.Size;
		return (Vector2Int)texel;
	}

	public override void OnUpdateInput()
	{
		base.OnUpdateInput();

		if ( IsHovered )
		{
			var mouseScreenPos = ImGui.GetMousePos();
			HoveredPixel = ScreenPosToPixel( mouseScreenPos );
			if ( IsActive )
			{
				SelectedPixel = HoveredPixel;
			}
		}
	}

	protected override void OnDrawSelf( ImDrawList drawList )
	{
		base.OnDrawSelf( drawList );

		if ( SelectedPixel is not null )
		{
			drawList.AddRect( SelectedPixelScreenPos, SelectedPixelScreenPos + PixelSize, Color.Green, thickness: 2 );
		}

		if ( !IsHovered )
			return;

		if ( HoveredPixel is Vector2Int hovered )
		{
			var voxel = new Vector3Int( hovered.x, hovered.y, TextureSlice );
			var distance = VoxelData[voxel];
			var distanceText = $"{distance:F3}";
			var distanceTextSize = ImGui.CalcTextSize( distanceText );
			var normal = VoxelData.EstimateVoxelSurfaceNormal( voxel );
			var normalText = $"({normal.x:F3},{normal.y:F3},{normal.z:F3})";
			var normalTextSize = ImGui.CalcTextSize( normalText );
			var textSizeMaxs = distanceTextSize.ComponentMax( normalTextSize );
			textSizeMaxs.y *= 2;

			var popupRect = new Rect( HoveredPixelScreenPos + new Vector2( -textSizeMaxs.x * 0.5f, -textSizeMaxs.y * 2 + 5 ), textSizeMaxs + 10 );
			var bgCol = ImGui.GetColorU32( ImGuiCol.WindowBg );
			bgCol.a = 255;
			var borderCol = ImGui.GetColorU32( ImGuiCol.Border );
			drawList.AddRectFilled( popupRect.TopLeft, popupRect.BottomRight, bgCol );
			drawList.AddRect( popupRect.TopLeft, popupRect.BottomRight, borderCol );
			drawList.AddText( popupRect.Center + Vector2.Down * textSizeMaxs.y * 0.25f, Color.White, distanceText, TextFlag.Center );
			drawList.AddText( popupRect.Center - Vector2.Down * textSizeMaxs.y * 0.25f, Color.White, normalText, TextFlag.Center );
		}
		if ( HoveredPixel is not null )
		{
			drawList.AddRect( HoveredPixelScreenPos, HoveredPixelScreenPos + PixelSize, Color.Gray, thickness: 2);
		}
	}
}
