using Duccsoft;
using Duccsoft.ImGui;
using Duccsoft.ImGui.Elements;
using Duccsoft.ImGui.Rendering;

public class TextureInfoWidget : ImageWidget
{
	public TextureInfoWidget( Window parent, VoxelSdfData voxelData, int textureSlice,
		Texture texture, Vector2 size, Vector2 uv0, Vector2 uv1, 
		Color tintColor, Color borderColor, ImDrawList.ImageTextureFiltering textureFiltering = ImDrawList.ImageTextureFiltering.Anisotropic ) 
		: base( parent, texture, size, uv0, uv1, tintColor, borderColor, textureFiltering )
	{
		VoxelData = voxelData;
		TextureSlice = textureSlice;
	}

	public VoxelSdfData VoxelData { get; }
	public int TextureSlice { get; }
	public Vector2 MouseLocalPos { get; private set; }
	public Vector2 HoveredNormal { get; private set; }
	public Vector2Int HoveredPixel { get; private set; }
	public Vector2 HoveredPixelScreenPos => ScreenPosition + HoveredPixel * HoveredPixelSize;
	public Vector2 HoveredPixelSize => ImageSize / ColorTexture.Size;

	public override void OnUpdateInput()
	{
		base.OnUpdateInput();

		var mouseScreenPos = ImGui.GetMousePos();
		MouseLocalPos = mouseScreenPos - ScreenPosition;
		HoveredNormal = MouseLocalPos / ImageSize;
		HoveredPixel = (Vector2Int)(HoveredNormal * ColorTexture.Size);
	}

	protected override void OnDrawSelf( ImDrawList drawList )
	{
		base.OnDrawSelf( drawList );

		if ( !IsHovered )
			return;

		var voxel = new Vector3Int( HoveredPixel.x, HoveredPixel.y, TextureSlice );
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
		drawList.AddRect( HoveredPixelScreenPos, HoveredPixelScreenPos + HoveredPixelSize, Color.Gray, thickness: 2);
		// drawList.AddRect( )
	}
}
