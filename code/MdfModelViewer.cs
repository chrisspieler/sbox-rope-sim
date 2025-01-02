using Duccsoft;
using Duccsoft.ImGui;
using Duccsoft.ImGui.Extensions;
using System;
using System.Runtime.CompilerServices;

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
			if ( Mdf?.GetSdfTexture( value ) is null )
			{
				value = -1;
			}
			var changed = _selectedVoxel != value;
			if ( value.x > -1 )
			{
				_octreeSlice = value.z / 16;
				LastValidVoxel = value;
			}
			_selectedVoxel = value;
			if ( changed )
			{
				_shouldRefreshTexture = true;
			}
		}
	}
	private Vector3Int _selectedVoxel;
	private Vector3Int LastValidVoxel { get; set; } = new Vector3Int( -1 );
	private float MouseToVoxelDistance { get; set; }
	private void UpdateInput()
	{
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

		ImGui.SetNextWindowPos( new Vector2( 1400, 200 ) * ImGuiStyle.UIScale );
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
		DrawTextureViewerOverlay();
	}

	private int _octreeSlice = 0;

	private void PaintTextureViewerStats()
	{
		var size = Mdf.OctreeLeafDims;
		ImGui.Text( $"Octree Slice:" ); ImGui.SameLine();
		var octreeSlice = _octreeSlice;
		if ( ImGui.SliderInt( "octreeSlice", ref octreeSlice, 0, Mdf.OctreeSize / 16 - 1 ) )
		{
			var diff = octreeSlice - _octreeSlice;
			Scene.Camera.WorldPosition += new Vector3( 0f, 0f, Mdf.OctreeLeafSize * diff );
			_octreeSlice = octreeSlice;
			SelectedVoxel = LastValidVoxel.WithZ( octreeSlice * 16 );
		}
		
		ImGui.Text( $"Selected Octree Voxel: {SelectedVoxel / 16}" );
		ImGui.NewLine();
		ImGui.Text( $"Texture: {size}x{size}x{size}, {Mdf.DataSize.FormatBytes()}" );
	}

	private bool _shouldRefreshTexture;
	private Vector3Int? _selectedTexel;
	private Vector3Int? _hoveredTexel;

	private void PaintTextureViewerViewport()
	{
		if ( SelectedVoxel.x < 0 || !Mdf.IsInBounds( SelectedVoxel ) )
			return;

		ImGui.Text( "Texture Slice:" ); ImGui.SameLine();
		_maxSlice = Mdf.OctreeLeafDims - 1;
		var sdfTex = Mdf.GetSdfTexture( SelectedVoxel );
		var textureSlice = TextureSlice;
		if ( ImGui.SliderInt( nameof( TextureSlice ), ref textureSlice, 0, _maxSlice ) )
		{
			var diff = textureSlice - TextureSlice;
			Scene.Camera.WorldPosition += new Vector3( 0f, 0f, MeshDistanceSystem.VoxelSize * diff );
		}
		if ( _copiedTex is null || textureSlice != TextureSlice || _shouldRefreshTexture )
		{
			TextureSlice = textureSlice;
			_copiedTex = CopyMdfTexture( sdfTex, TextureSlice );
			if ( _selectedTexel is Vector3Int texel )
			{
				_selectedTexel = texel.WithZ( textureSlice );
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
		var texImage = new TextureInfoWidget( ImGui.CurrentWindow, sdfTex, TextureSlice, _copiedTex, new Vector2( 400 ) * ImGuiStyle.UIScale, uv0, uv1, Color.Transparent, ImGui.GetColorU32( ImGuiCol.Border ), Duccsoft.ImGui.Rendering.ImDrawList.ImageTextureFiltering.Point, angle );
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

		void DrawTexel( Vector3Int? maybeTexel, Color color )
		{
			if ( maybeTexel is not Vector3Int texel )
				return;

			var texelPos = sdfTex.VoxelToPosition( texel );
			var texelDistance = sdfTex[texel];
			var texelNormal = sdfTex.EstimateVoxelSurfaceNormal( texel );
			var tx = MdfGameObject.WorldTransform;
			var size = MeshDistanceSystem.VoxelSize;
			var pos = texelPos + size * 0.5f;
			var bbox = BBox.FromPositionAndSize( pos, size );
			// color.a = texelDistance < 0 ? 0.15f : 1f;
			var insideColor = (color * 0.2f).WithAlpha( 0.25f );
			DebugOverlay.Box( bbox, color: color.WithAlpha( 1f ), transform: tx, overlay: false );
			DebugOverlay.Box( bbox, color: insideColor, transform: tx, overlay: true );
			DebugOverlay.Line( pos, pos + texelNormal * 3f, color: Color.Blue, transform: tx, overlay: true );
			var bounds = Mdf.VoxelToLocalBounds( SelectedVoxel );
			var z = ((float)_textureSlice).Remap( 0f, Mdf.OctreeLeafSize, bounds.Mins.z, bounds.Maxs.z );
			var slice = BBox.FromPositionAndSize( bounds.Center.WithZ( z + 0.5f ), bounds.Size.WithZ( 0.2f ) );
			DebugOverlay.Box( slice, color: Color.White, transform: tx, overlay: true );
		}
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
