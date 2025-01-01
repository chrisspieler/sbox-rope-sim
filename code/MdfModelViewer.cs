using Duccsoft;
using Duccsoft.ImGui;

namespace Sandbox;

public class MdfModelViewer : Component
{
	[Property] public bool ShouldDrawOctree { get; set; } = true;
	[Property] public bool ShowTextureViewer { get; set; } = false;
	public GameObject MdfGameObject { get; set; }
	public MeshDistanceField Mdf { get; set; }

	protected override void OnUpdate()
	{
		if ( Mdf is null )
			return;

		if ( ShowTextureViewer )
		{
			PaintTextureViewer();
		}

		if ( !MdfGameObject.IsValid() )
			return;

		UpdateInput();
		PaintInstanceViewer();
		DrawInstanceViewerOverlay();
	}

	#region GameObject Viewer

	private float MouseSignedDistance { get; set; }
	private Vector3Int HighlightedVoxel { get; set; }
	private Vector3Int SelectedVoxel { get; set; }
	private float MouseToVoxelDistance { get; set; }
	private void UpdateInput()
	{
		UpdateHighlightedVoxel();

		if ( Input.Released( "attack1" ) )
		{
			SelectedVoxel = HighlightedVoxel;
			if ( SelectedVoxel.x > -1 )
			{
				ShowTextureViewer = true;
			}
		}
	}

	private void UpdateHighlightedVoxel()
	{
		MouseToVoxelDistance = -1f;
		MouseSignedDistance = 0f;
		HighlightedVoxel = new Vector3Int( -1 );

		if ( ImGui.GetIO().WantCaptureMouse )
			return;

		var mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );
		var tracePos = MdfGameObject.WorldTransform.PointToLocal( mouseRay.Position );
		var traceDir = MdfGameObject.WorldTransform.NormalToLocal( mouseRay.Forward );
		var filter = new Vector3Int( -1, -1, ShowTextureViewer ? _octreeSlice * 16 : -1 );
		var tr = Mdf.Trace( tracePos, traceDir, out float hitDistance, filter );
		if ( tr is not null )
		{
			HighlightedVoxel = tr.Position;
			MouseToVoxelDistance = hitDistance;
			if ( tr.Data is not null )
			{
				var hitPos = new Ray( tracePos, traceDir ).Project( hitDistance );
				var hitSdfVoxel = tr.Data.PositionToVoxel( hitPos );
				MouseSignedDistance = tr.Data[hitSdfVoxel];
			}
		}
	}

	private void PaintInstanceViewer()
	{
		ImGui.SetNextWindowPos( new Vector2( 50, 500 ) * ImGuiStyle.UIScale );
		if ( ImGui.Begin( $"Mesh Distance Field ({MdfGameObject?.Name ?? "null"})" ) )
		{
			PaintInstanceViewerWindow();	
		}
		ImGui.End();
	}

	private void PaintInstanceViewerWindow()
	{
		if ( !MdfGameObject.IsValid() )
		{
			ImGui.Text( "No instance is selected." );
			return;
		}

		if ( Mdf is null )
		{
			ImGui.Text( "No mesh distance field exists for this instance." );
			return;
		}

		var showTextureViewer = ShowTextureViewer;
		ImGui.Checkbox( "Show Texture Viewer", ref showTextureViewer );
		ShowTextureViewer = showTextureViewer;

		PaintInstanceStats();
		if ( ImGui.Button( "Regenerate MDF" ) )
		{
			MeshDistanceSystem.Current.RemoveMdf( Mdf.Id );
		}
	}

	private void PaintInstanceStats()
	{
		if ( Mdf.IsBuilding )
		{
			ImGui.Text( $"Building, elapsed time: {Mdf.SinceBuildStarted.Relative:F3}ms" );
		}
		else
		{
			float buildTime = Mdf.SinceBuildStarted - Mdf.SinceBuildFinished;
			ImGui.Text( $"Build complete in: {buildTime:F3}ms" );
		}
		if ( Mdf.VertexCount < 0 )
			return;
		ImGui.Text( $"size: {Mdf.Bounds.Size.x:F2},{Mdf.Bounds.Size.y:F2},{Mdf.Bounds.Size.z:F2} tris: {Mdf.TriangleCount}" );
		ImGui.Text( $"Octree Leaves: {Mdf.OctreeLeafCount}/{Mdf.OctreeLeafCount + Mdf.QueuedJumpFloodJobs}" );
		ImGui.Text( $"Data Size: {Mdf.DataSize.FormatBytes():F3}" );
		var drawOctree = ShouldDrawOctree;
		ImGui.Checkbox( "Draw Octree", ref drawOctree );
		ShouldDrawOctree = drawOctree;
		if ( ShouldDrawOctree )
		{
			ImGui.Text( $"mouseover voxel: {HighlightedVoxel / 16}" );
			ImGui.Text( $"mouse to voxel distance: {MouseToVoxelDistance}" );
			ImGui.Text( $"mouseover signed distance: {MouseSignedDistance}" );
		}
	}

	private void DrawInstanceViewerOverlay()
	{
		if ( Mdf is null || !MdfGameObject.IsValid() )
			return;

		var tx = MdfGameObject.WorldTransform;

		if ( ShouldDrawOctree )
		{
			// Draw the octree
			var slice = ShowTextureViewer ? _octreeSlice * 16 : -1;
			Mdf.DebugDraw( tx, HighlightedVoxel, SelectedVoxel, slice );
		}
	}
	#endregion

	#region Texture Viewer

	public int TextureSlice 
	{
		get => _textureSlice.Clamp( 0, _maxSlice );
		set => _textureSlice = value.Clamp( 0, _maxSlice );
	}
	private int _textureSlice = 0;
	int _maxSlice;
	Texture _copiedTex;
	private void PaintTextureViewer()
	{
		if ( Mdf is null || Mdf.OctreeLeafDims < 0 )
			return;

		ImGui.SetNextWindowPos( new Vector2( 50, 50 ) * ImGuiStyle.UIScale );
		if ( ImGui.Begin( $"Volume Texture Viewer ({Mdf.Id})" ) )
		{
			PaintTextureViewerWindow();
		}
		ImGui.End();
	}

	private void PaintTextureViewerWindow()
	{
		if ( Mdf.IsBuilding )
		{
			ImGui.Text( $"No mesh distance field exists yet." );
			return;
		}

		PaintTextureViewerStats();
		PaintTextureViewerViewport();
	}

	private int _octreeSlice = 0;

	private void PaintTextureViewerStats()
	{
		var size = Mdf.OctreeLeafDims;
		ImGui.Text( $"Octree Slice:" ); ImGui.SameLine();
		var octreeSlice = _octreeSlice;
		ImGui.SliderInt( "octreeSlice", ref octreeSlice, 0, Mdf.OctreeSize / 16 - 1 );
		_octreeSlice = octreeSlice;
		ImGui.Text( $"Selected Octree Voxel: {SelectedVoxel / 16}" );
		ImGui.NewLine();
		ImGui.Text( $"Texture: {size}x{size}x{size}, {Mdf.DataSize.FormatBytes()}" );
	}

	private Vector3Int _lastSelectedVoxel;

	private void PaintTextureViewerViewport()
	{
		ImGui.Text( "Texture Slice:" ); ImGui.SameLine();
		_maxSlice = Mdf.OctreeLeafDims - 1;
		
		var textureSlice = TextureSlice;
		ImGui.SliderInt( nameof( TextureSlice ), ref textureSlice, 0, _maxSlice );
		if ( _copiedTex is null || textureSlice != TextureSlice || SelectedVoxel != _lastSelectedVoxel )
		{
			TextureSlice = textureSlice;
			var sdfTex = Mdf.GetSdfTexture( SelectedVoxel );
			_copiedTex = CopyMdfTexture( sdfTex, TextureSlice );
			_lastSelectedVoxel = SelectedVoxel;
		}
		ImGui.Image( _copiedTex, new Vector2( 400 ) * ImGuiStyle.UIScale, new Vector2( 0, 0 ), new Vector2( 1, 1 ), Color.Transparent, ImGui.GetColorU32( ImGuiCol.Border ), Duccsoft.ImGui.Rendering.ImDrawList.ImageTextureFiltering.Point );
	}

	readonly ComputeShader _textureSliceCs = new( "mesh_sdf_preview_cs" );

	private Texture CopyMdfTexture( VoxelSdfData voxelData, int z )
	{
		var size = voxelData.VoxelGridDims;

		var outputTex = Texture.Create( size, size )
			.WithUAVBinding()
			.Finish();

		var voxelSdfGpu = new GpuBuffer<int>( size * size * size / 4, GpuBuffer.UsageFlags.Structured );
		voxelSdfGpu.SetData( voxelData.VoxelSdf );
		_textureSliceCs.Attributes.Set( "VoxelMinsOs", voxelData.Bounds.Mins );
		_textureSliceCs.Attributes.Set( "VoxelMaxsOs", voxelData.Bounds.Maxs );
		_textureSliceCs.Attributes.Set( "VoxelVolumeDims", new Vector3( size ) );
		_textureSliceCs.Attributes.Set( "VoxelSdf", voxelSdfGpu );
		_textureSliceCs.Attributes.Set( "ZLayer", z );
		_textureSliceCs.Attributes.Set( "OutputTexture", outputTex );
		_textureSliceCs.Dispatch( size, size, size );
		return outputTex;
	}

	#endregion
}
