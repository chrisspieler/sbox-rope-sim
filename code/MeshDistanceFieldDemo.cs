using Duccsoft;
using Duccsoft.ImGui;

namespace Sandbox;

public class MeshDistanceFieldDemo : Component
{
	[Property] public GameObject MeshContainer { get; set; }
	[Property] public int SelectedMeshIndex
	{
		get => _selectedMeshIndex;
		set
		{
			_selectedMeshIndex = value;

			if ( !Game.IsPlaying )
				return;

			_selectedMeshIndex = _selectedMeshIndex.UnsignedMod( MeshContainer.Children.Count );
			for ( int i = 0; i < MeshContainer.Children.Count; i++ )
			{
				var child = MeshContainer.Children[i];
				child.Enabled = _selectedMeshIndex == i;
			}
		}
	}
	private int _selectedMeshIndex = 0;
	[Property] public MdfTextureViewer TextureViewer { get; set; }
	[Property] public bool ShouldDrawSlicePlane { get; set; } = true;
	[Property] public bool ShouldDrawSliceVoxels { get; set; } = false;
	[Property] public bool ShouldDrawOctree { get; set; } = false;

	public GameObject SelectedMeshGameObject => MeshContainer?.Children[SelectedMeshIndex];

	public MeshDistanceField Mdf => TextureViewer?.Mdf;

	protected override void OnUpdate()
	{
		UpdateInput();
		UpdateUI();
		UpdateOverlays();
	}

	private float _sdf;
	private Vector3 _dirToSurface;
	private Angles _cameraAngles => new Angles( -25f, -180f, 0 );
	private float _cameraDistance = 200;

	private void UpdateInput()
	{
		if ( Mdf is null )
			return;

		var camera = Scene.Camera;
		var tx = SelectedMeshGameObject.WorldTransform;
		_cameraDistance = Mdf.Bounds.Maxs.x * 8f;

		var z = ((float)TextureViewer.TextureSlice).Remap( 0, Mdf.VoxelSdf.VoxelGridDims - 1, Mdf.Bounds.Mins.z, Mdf.Bounds.Maxs.z );
		var worldCenter = tx.PointToWorld( Mdf.Bounds.Center.WithZ( z ) );
		
		camera.WorldPosition = worldCenter + _cameraAngles.Forward * _cameraDistance;
		camera.WorldRotation = Rotation.LookAt( Vector3.Direction( camera.WorldPosition, worldCenter ) );
		var mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );
		var plane = new Plane( worldCenter, Vector3.Up );
		var tracePos = plane.Trace( mouseRay, true );
		if ( !tracePos.HasValue )
			return;

		var mouseWorldPos = tracePos.Value;
		var mouseLocalPos = tx.PointToLocal( mouseWorldPos );
		var sample = Mdf.Sample( mouseLocalPos );
		_sdf = sample.SignedDistance;
		_dirToSurface = sample.SurfaceNormal * ( _sdf < 0 ? 1 : -1 );

		if ( _sdf < 0 )
		{
			DebugOverlay.Sphere( new Sphere( mouseWorldPos, 0.5f ), color: Color.White.WithAlpha( 0.15f), overlay: true );
		}
		else
		{
			DebugOverlay.Sphere( new Sphere( mouseWorldPos, 0.5f ), color: Color.Red );
		}
		
		var distance = _sdf < 0 ? -_sdf : _sdf;
		distance += 0.25f;
		var surfaceLocalPos = mouseLocalPos + _dirToSurface * distance;
		var surfaceWorldPos = SelectedMeshGameObject.WorldTransform.PointToWorld( surfaceLocalPos );
		DebugOverlay.Sphere( new Sphere( surfaceWorldPos, 0.5f ), color: Color.Blue, overlay: false );
		DebugOverlay.Line( surfaceWorldPos, surfaceWorldPos + tx.NormalToWorld( sample.SurfaceNormal ) * 3f, color: Color.Green, overlay: false );
	}

	private void UpdateOverlays()
	{
		if ( Mdf is null )
			return;

		if ( ShouldDrawSlicePlane )
		{
			DrawSlicePlane( Mdf, SelectedMeshGameObject, TextureViewer.TextureSlice );
		}
		if ( ShouldDrawSliceVoxels )
		{
			DrawSliceVoxels( Mdf, SelectedMeshGameObject, TextureViewer.TextureSlice );
		}
		if ( ShouldDrawOctree )
		{
			DrawOctree( Mdf, SelectedMeshGameObject );
		}
	}

	private void UpdateUI()
	{
		ImGui.SetNextWindowPos( new Vector2( 875, 50 ) * ImGuiStyle.UIScale );
		if ( ImGui.Begin( "Mesh Distance Field" ) )
		{
			PaintWindow();
		}
		ImGui.End();
	}

	private static void DrawSlicePlane( MeshDistanceField mdf, GameObject go, int slice )
	{
		var overlay = DebugOverlaySystem.Current;
		var mins = mdf.Bounds.Mins;
		var maxs = mdf.Bounds.Maxs;
		var z = ((float)slice).Remap( 0, mdf.VoxelSdf.VoxelGridDims - 1, mins.z, maxs.z );
		var center = mdf.Bounds.Center.WithZ( z );
		var tx = go.WorldTransform;

		overlay.Box( BBox.FromPositionAndSize( center, mdf.Bounds.Size.WithZ( 1f ) ), color: Color.White.WithAlpha( 0.15f ), transform: tx );
		overlay.Line( center.WithX( mins.x ), center.WithX( maxs.x ), color: Color.White.WithAlpha( 0.15f ), transform: tx );
		overlay.Line( center.WithY( mins.y ), center.WithY( maxs.y ), color: Color.White.WithAlpha( 0.15f ), transform: tx );
	}


	private static void DrawSliceVoxels( MeshDistanceField mdf, GameObject go, int slice )
	{
		var overlay = DebugOverlaySystem.Current;
		var tx = go.WorldTransform;

		for ( int y = 0; y < mdf.VoxelSdf.VoxelGridDims; y++ )
		{
			for ( int x = 0; x < mdf.VoxelSdf.VoxelGridDims; x++ )
			{
				var voxelLocalPos = mdf.VoxelToPositionCenter( new Vector3Int( x, y, slice ) );
				var bbox = BBox.FromPositionAndSize( voxelLocalPos, MeshDistanceSystem.VoxelSize );
				overlay.Box( bbox, color: Color.Green.WithAlpha( 0.15f ), transform: tx );
			}
		}
	}

	private static void DrawOctree( MeshDistanceField mdf, GameObject go )
	{
		var tx = go.WorldTransform;
		mdf.Octree.DebugDraw( tx );
	}

	private void PaintWindow()
	{
		if ( Mdf is null )
		{
			ImGui.Text( "No mesh distance field generated." );
			return;
		}
		ImGui.Text( $"Selected Mesh: {MeshContainer.Children[SelectedMeshIndex].Name}" );
		var previousMeshIndex = SelectedMeshIndex;
		if ( ImGui.Button( "Previous Mesh" ) )
		{
			SelectedMeshIndex--;
		}
		ImGui.SameLine();
		if ( ImGui.Button( "Next Mesh" ) )
		{
			SelectedMeshIndex++;
		}
		if ( previousMeshIndex != SelectedMeshIndex )
		{
			MeshDistanceSystem.Current.RemoveMdf( Mdf.Id );
		}
		ImGui.Text( "Draw Slice:" ); ImGui.SameLine();
		var drawSlicePlane = ShouldDrawSlicePlane;
		ImGui.Checkbox( "Plane", ref drawSlicePlane ); ImGui.SameLine();
		ShouldDrawSlicePlane = drawSlicePlane;
		var drawSliceVoxels = ShouldDrawSliceVoxels;
		ImGui.Checkbox( "Voxels", ref drawSliceVoxels );
		ShouldDrawSliceVoxels = drawSliceVoxels;
		ImGui.Text( "Draw Mesh:" ); ImGui.SameLine();
		var drawOctree = ShouldDrawOctree;
		ImGui.Checkbox( "Octree", ref drawOctree );
		ShouldDrawOctree = drawOctree;
		ImGui.Text( $"Voxel Size: {MeshDistanceSystem.VoxelSize:F3}" );
		ImGui.Text( $"Mouse Distance: {_sdf:F3}" );
		ImGui.Text( $"Mouse Direction: {_dirToSurface.x:F2},{_dirToSurface.y:F2},{_dirToSurface.z:F2}" );

		if ( ImGui.Button( "Regenerate MDF" ) )
		{
			MeshDistanceSystem.Current.RemoveMdf( Mdf.Id );
		}
	}
}
