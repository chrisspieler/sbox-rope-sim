using Duccsoft;
using Duccsoft.ImGui;

namespace Sandbox;

public class MdfModelViewer : Component
{
	[Property] public bool ShouldDrawOctree { get; set; } = true;
	[Property] public bool ShowTextureViewer { get; set; } = false;
	public GameObject MdfGameObject { get; set; }
	public MeshDistanceField Mdf 
	{
		get => _mdf;
		set
		{
			var changed = value != _mdf;
			_mdf = value;
			if ( changed )
			{
				_copiedTex = null;
				_octreeSlice = 0;
				_textureSlice = 0;
				SelectedVoxel = -1;
				HighlightedVoxel = -1;
			}
		}
	}
	private MeshDistanceField _mdf;

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
	private Vector3Int SelectedVoxel 
	{
		get => _selectedVoxel;
		set
		{
			SignedDistanceField sdfTex = Mdf?.GetSdfTexture( value );
			if ( sdfTex is null )
			{
				value = -1;
			}
			var changed = _selectedVoxel != value;
			if ( value.x > -1 )
			{
				_octreeSlice = value.z / Mdf?.OctreeLeafDims ?? 16;
				LastValidVoxel = value;
			}
			_selectedVoxel = value;
			if ( changed )
			{
				_shouldRefreshTexture = true;
				if ( sdfTex is not null && !sdfTex.IsRebuilding && sdfTex.Debug is null )
				{
					RebuildSelectedVoxel();
				}
			}
		}
	}
	private Vector3Int _selectedVoxel;
	private Vector3Int LastValidVoxel { get; set; } = new Vector3Int( -1 );
	private float MouseToVoxelDistance { get; set; }
	private void UpdateInput()
	{
		if ( !ShouldDrawOctree )
			return;

		var lastSelectedVoxel = SelectedVoxel;
		UpdateHighlightedVoxel();

		if ( Input.Released( "attack1" ) )
		{
			SelectedVoxel = HighlightedVoxel;
			if ( SelectedVoxel.x > -1 )
			{
				ShowTextureViewer = true;
			}
			else
			{
				if ( lastSelectedVoxel.x < 0 )
				{
					_octreeSlice = 0;
					ShowTextureViewer = false;
				}
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
		var filter = new Vector3Int( -1, -1, ShowTextureViewer ? _octreeSlice * Mdf.OctreeLeafDims : -1 );
		var tr = Mdf.Trace( tracePos, traceDir, out float hitDistance, filter );
		if ( tr is not null )
		{
			HighlightedVoxel = tr.Position;
			MouseToVoxelDistance = hitDistance;
			if ( tr.Data is not null )
			{
				var hitPos = new Ray( tracePos, traceDir ).Project( hitDistance );
				var hitTexel = tr.Data.PositionToTexel( hitPos );
				MouseSignedDistance = tr.Data[hitTexel];
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
			Mdf.RebuildFromMesh();
		}
	}

	private void PaintInstanceStats()
	{
		if ( Mdf.IsBuilding )
		{
			ImGui.Text( $"Building, elapsed time: {Mdf.SinceBuildStarted.Relative * 1000:F3}ms" );
		}
		else
		{
			float buildTime = ( Mdf.SinceBuildFinished.Absolute - Mdf.SinceBuildStarted.Absolute ) * 1000f;
			string buildString = buildTime == 0 
				? "<4ms"
				: $"{buildTime:F3}ms";
			ImGui.Text( $"Build complete in: {buildString}" );
		}
		ImGui.Text( $"HasMesh: {Mdf.IsMeshBuilt}, HasOctree: {Mdf.IsOctreeBuilt}, JFAJobs: {Mdf.QueuedJumpFloodJobs}" );
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
			ImGui.Text( $"mouseover voxel: {HighlightedVoxel / Mdf.OctreeLeafDims}" );
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
			var slice = _octreeSlice * Mdf.OctreeLeafDims;
			if ( !ShowTextureViewer )
				slice = -1;

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

		ImGui.SetNextWindowPos( new Vector2( 1400, 200 ) * ImGuiStyle.UIScale );
		if ( ImGui.Begin( $"Volume Texture Viewer ({Mdf.Id})" ) )
		{
			PaintTextureViewerWindow();
		}
		ImGui.End();
	}

	private void PaintTextureViewerWindow()
	{
		if ( !Mdf.IsOctreeBuilt )
		{
			ImGui.Text( $"No mesh distance field exists yet." );
			return;
		}

		PaintTextureViewerStats();
		PaintTextureViewerDebugOptions();
		PaintTextureViewerViewport();
		DrawTextureViewerOverlay();
	}

	private int _octreeSlice = 0;

	private void PaintTextureViewerStats()
	{
		var size = Mdf.OctreeLeafDims;
		ImGui.Text( $"Octree Slice:" ); ImGui.SameLine();
		var octreeSlice = _octreeSlice;
		if ( ImGui.SliderInt( "octreeSlice", ref octreeSlice, 0, Mdf.OctreeSize / Mdf.OctreeLeafDims - 1 ) )
		{
			var diff = octreeSlice - _octreeSlice;
			Scene.Camera.WorldPosition += new Vector3( 0f, 0f, Mdf.OctreeLeafSize * diff );
			_octreeSlice = octreeSlice;
			SelectedVoxel = LastValidVoxel.WithZ( octreeSlice * Mdf.OctreeLeafDims );
		}
		
		ImGui.Text( $"Selected {size}^3 Voxel: {SelectedVoxel / Mdf.OctreeLeafDims}" );
		var bounds = Mdf.VoxelToLocalBounds( SelectedVoxel );
		ImGui.Text( $"mins: ({bounds.Mins.x:F3},{bounds.Mins.y:F3},{bounds.Mins.z:F3})" );
		ImGui.Text( $"maxs: ({bounds.Maxs.x:F3},{bounds.Maxs.y:F3},{bounds.Maxs.z:F3})" );
		
	}

	private void PaintTextureViewerDebugOptions()
	{
		if ( Mdf.GetSdfTexture( SelectedVoxel ) is SignedDistanceField existingData )
		{
			if ( !existingData.IsRebuilding && ImGui.Button( "Rebuild Texture" ) )
			{
				RebuildSelectedVoxel();
			}
		}
	}

	private void RebuildSelectedVoxel()
	{
		Mdf.RebuildOctreeVoxel( _selectedVoxel, true, onCompleted: () => _shouldRefreshTexture = true );
	}

	private bool _shouldRefreshTexture;
	private Vector3Int? _selectedTexel;
	private Vector3Int? _hoveredTexel;
	private bool _showGradients;

	private void PaintTextureViewerViewport()
	{
		if ( SelectedVoxel.x < 0 || Mdf?.IsInBounds( SelectedVoxel ) != true )
			return;

		var sdfTex = Mdf.GetSdfTexture( SelectedVoxel );
		if ( sdfTex is null )
		{
			ImGui.NewLine();
			ImGui.Text( $"Selected voxel contains no SDF texture!" );
			return;
		}

		ImGui.Text( "Texture Slice:" ); ImGui.SameLine();
		if ( sdfTex.Debug is not null )
		{
			var showGradients = _showGradients;
			if ( ImGui.Checkbox( "Show Gradients", ref showGradients ) )
			{
				_shouldRefreshTexture = true;
				if ( showGradients )
				{
					sdfTex.Debug.PrecalculateGradients();
				}
			}
			_showGradients = showGradients;
		}
		_maxSlice = sdfTex.TextureSize - 1;
		var textureSlice = TextureSlice;
		if ( ImGui.SliderInt( nameof( TextureSlice ), ref textureSlice, 0, _maxSlice ) )
		{
			var diff = textureSlice - TextureSlice;
			Scene.Camera.WorldPosition += new Vector3( 0f, 0f, sdfTex.TexelSize * diff );
		}
		if ( _copiedTex is null || textureSlice != TextureSlice || _shouldRefreshTexture )
		{
			TextureSlice = textureSlice;
			var debugVisMode = _showGradients
				? SdfSliceDebugVisualization.Gradient
				: SdfSliceDebugVisualization.InsideOutside;
			_copiedTex = CopyMdfTexture( sdfTex, TextureSlice, debugVisMode );
			if ( _selectedTexel is not null )
			{
				_selectedTexel = _selectedTexel.Value.WithZ( textureSlice );
			}
			_shouldRefreshTexture = false;
		}
		var localRot = MdfGameObject.WorldTransform.RotationToLocal( Scene.Camera.WorldRotation ).Angles();
		localRot *= -1f;
		float angle = localRot.yaw;
		angle -= 90f;
		angle += 45f;
		angle -= MathX.UnsignedMod( angle, 90f );
		angle = angle.DegreeToRadian();
		var uv0 = new Vector2( 1f, 0f );
		var uv1 = new Vector2( 0f, 1f );
		var texImage = new TextureInfoWidget( ImGui.CurrentWindow, sdfTex, TextureSlice, _copiedTex, new Vector2( 400 ) * ImGuiStyle.UIScale, uv0, uv1, Color.White, ImGui.GetColorU32( ImGuiCol.Border ), Duccsoft.ImGui.Rendering.ImDrawList.ImageTextureFiltering.Point, angle );
		if ( texImage.SelectedPixel is Vector2Int selected)
		{
			_selectedTexel = new Vector3Int( selected.x, selected.y, _textureSlice );
		}
		else if ( _selectedTexel is not null )
		{
			texImage.SelectedPixel = new Vector2Int( _selectedTexel.Value.x, _selectedTexel.Value.y );
		}
		if ( texImage.HoveredPixel is Vector2Int hovered )
		{
			_hoveredTexel = new Vector3Int( hovered.x, hovered.y, _textureSlice );
		}
		else
		{
			_hoveredTexel = null;
		}
		var res = sdfTex.TextureSize;
		ImGui.Text( $"Texture: {res}x{res}x{res}, {sdfTex.DataSize.FormatBytes()}" );

		if ( _selectedTexel is not Vector3Int texel  )
			return;

		ImGui.Text( $"Selected Texel: {texel}" );
		//ImGui.Text( $"Signed Distance: {sdfTex[texel]:F3}" );
		//var gradient = sdfTex.CalculateGradient( texel );

		//ImGui.Text( $"Gradient: {gradient.x:F3},{gradient.y:F3},{gradient.z:F3}" );
		if ( sdfTex.Debug is not null )
		{
			ImGui.Text( $"SeedId: {sdfTex.Debug.GetSeedId( texel )}" );
			if ( ImGui.Button( "Dump Debug Info" ) )
			{
				sdfTex.Debug.DumpAllData();
			}
		}

	}

	private void DrawTextureViewerOverlay()
	{
		if ( SelectedVoxel.x < 0 )
			return;

		if ( Mdf is null || !MdfGameObject.IsValid() )
			return;

		var sdfTex = Mdf.GetSdfTexture( SelectedVoxel );
		if ( sdfTex is null )
			return;

		DrawTexel( _selectedTexel, Color.Green );
		if ( _selectedTexel != _hoveredTexel )
		{
			DrawTexel( _hoveredTexel, Color.White );
		}

		if ( _selectedTexel is Vector3Int texel && sdfTex.Debug is not null)
		{
			var seedId = sdfTex.Debug.GetSeedId( texel );
			var maybeTriangle = sdfTex.Debug.GetSeedTriangle( seedId );
			if ( maybeTriangle is not Triangle tri )
			{
				return;
			}
			var tx = MdfGameObject.WorldTransform;
			var visibleColor = Color.Green;
			var hiddenColor = (Color.Green * 0.5f).WithAlpha( 0.5f );
			var texelPos = Mdf.GetTexelBounds( SelectedVoxel, texel ).Center;
			var subSeedId = seedId % 4;
			var triCenter = (tri.A + tri.B + tri.C) / 3f;
			var seedPos = subSeedId switch
			{
				1 => tri.A,
				2 => tri.B,
				3 => tri.C,
				_ => triCenter,
			};
			var lineToSeed = new Line( texelPos, seedPos );
			DebugOverlay.Line( lineToSeed, hiddenColor, transform: tx, overlay: true );
			DebugOverlay.Line( lineToSeed, visibleColor, transform: tx );
			var v01 = new Line( tri.A, tri.B );
			var v12 = new Line( tri.B, tri.C );
			var v20 = new Line( tri.C, tri.A );
			DebugOverlay.Line( v01, hiddenColor, transform: tx, overlay: true );
			DebugOverlay.Line( v01, visibleColor, transform: tx );
			DebugOverlay.Line( v12, hiddenColor, transform: tx, overlay: true );
			DebugOverlay.Line( v12, visibleColor, transform: tx );
			DebugOverlay.Line( v20, hiddenColor, transform: tx, overlay: true );
			DebugOverlay.Line( v20, visibleColor, transform: tx );

			var triNormal = tri.Normal;
			var normalColor = new Color( triNormal.x, triNormal.y, triNormal.z, 1f );
			visibleColor = normalColor;
			hiddenColor = normalColor.WithAlpha( 0.5f );
			DebugOverlay.Line( triCenter, triCenter + triNormal * 1f, hiddenColor, transform: tx, overlay: true );
			DebugOverlay.Line( triCenter, triCenter + triNormal * 1f, visibleColor, transform: tx );
		}

		void DrawTexel( Vector3Int? maybeTexel, Color color )
		{
			if ( maybeTexel is not Vector3Int texel )
				return;

			var texelPos = sdfTex.TexelToPosition( texel );
			var tx = MdfGameObject.WorldTransform;
			var texelSize = sdfTex.TexelSize;
			var texelCenter = texelPos + texelSize * 0.5f;
			var texelBounds = BBox.FromPositionAndSize( texelCenter, texelSize );
			var insideColor = (color * 0.2f).WithAlpha( 0.25f );
			// Draw texel bounds
			DebugOverlay.Box( texelBounds, color: color.WithAlpha( 1f ), transform: tx, overlay: false );
			DebugOverlay.Box( texelBounds, color: insideColor, transform: tx, overlay: true );

			var texelGradient = sdfTex.CalculateGradient( texel );
			var distance = sdfTex[texel];
			// Draw gradient line
			DebugOverlay.Line( texelBounds.Center, texelBounds.Center + texelGradient * distance, color: Color.Blue, transform: tx, overlay: true );

			var bounds = Mdf.VoxelToLocalBounds( SelectedVoxel );
			var z = ((float)_textureSlice).Remap( 0f, sdfTex.TextureSize, bounds.Mins.z, bounds.Maxs.z );
			var slice = BBox.FromPositionAndSize( bounds.Center.WithZ( z + 0.5f ), bounds.Size.WithZ( 0.2f ) );
			// Draw slice plane
			DebugOverlay.Box( slice, color: Color.White, transform: tx, overlay: true );
		}
	}

	readonly ComputeShader _textureSliceCs = new( "shaders/sdf/mesh_sdf_preview_cs.shader" );

	private enum SdfSliceDebugVisualization
	{
		InsideOutside,
		Gradient
	}

	private Texture CopyMdfTexture( SignedDistanceField voxelData, int z, SdfSliceDebugVisualization debugMode )
	{
		var size = voxelData.TextureSize;

		var outputTex = Texture.Create( size, size, ImageFormat.RGBA8888 )
			.WithUAVBinding()
			.Finish();

		if ( voxelData.Debug is null )
		{
			debugMode = SdfSliceDebugVisualization.InsideOutside;
		}
		_textureSliceCs.Attributes.SetComboEnum( "D_MODE", debugMode );
		_textureSliceCs.Attributes.Set( "SdfTextureIndex", voxelData.DataTexture.Index );
		_textureSliceCs.Attributes.Set( "BoundsSizeOs", voxelData.Bounds.Size.x );
		_textureSliceCs.Attributes.Set( "TextureSize", voxelData.TextureSize );
		_textureSliceCs.Attributes.Set( "ZLayer", z );
		_textureSliceCs.Attributes.Set( "OutputTexture", outputTex );
		_textureSliceCs.Dispatch( size, size, 1 );
		return outputTex;
	}

	#endregion
}
