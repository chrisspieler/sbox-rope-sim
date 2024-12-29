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

	public GameObject SelectedMeshGameObject => MeshContainer?.Children[SelectedMeshIndex];

	public MeshDistanceField Mdf => TextureViewer?.Mdf;

	protected override void OnUpdate()
	{
		UpdateInput();
		UpdateUI();
	}

	private float _sdf;
	private Vector3 _dirToSurface;
	private Angles _cameraAngles => new Angles( -25f, -180f, 0 );
	private float _cameraDistance = 200;

	private void UpdateInput()
	{
		if ( Mdf is null )
			return;

		var tx = SelectedMeshGameObject.WorldTransform;

		var octreeTx = tx;
		DrawOctree( octreeTx, Mdf.Octree );

		var mins = Mdf.Bounds.Mins;
		var maxs = Mdf.Bounds.Maxs;
		var z = ((float)TextureViewer.TextureSlice).Remap( 0, Mdf.VoxelGridDims - 1, mins.z, maxs.z );
		var center = Mdf.Bounds.Center.WithZ( z );
		_cameraDistance = maxs.x * 8f;

		var camera = Scene.Camera;
		var worldCenter = tx.PointToWorld( center );
		for ( int y = 0; y < Mdf.VoxelGridDims; y++ )
		{
			for ( int x = 0; x < Mdf.VoxelGridDims; x++ )
			{
				var voxelLocalPos = Mdf.VoxelToPositionCenter( new Vector3Int( x, y, TextureViewer.TextureSlice ) );
				var bbox = BBox.FromPositionAndSize( voxelLocalPos, Mdf.VoxelSize );
				DebugOverlay.Box( bbox, color: Color.Green.WithAlpha( 0.15f ), transform: tx );
			}
		}
		camera.WorldPosition = worldCenter + _cameraAngles.Forward * _cameraDistance;
		camera.WorldRotation = Rotation.LookAt( Vector3.Direction( camera.WorldPosition, worldCenter ) );
		DebugOverlay.Box( BBox.FromPositionAndSize( center, Mdf.Bounds.Size.WithZ( 1f ) ), color: Color.White.WithAlpha( 0.15f ), transform: tx );
		DebugOverlay.Line( center.WithX( mins.x ), center.WithX( maxs.x ), color: Color.White.WithAlpha( 0.15f ), transform: tx );
		DebugOverlay.Line( center.WithY( mins.y ), center.WithY( maxs.y ), color: Color.White.WithAlpha( 0.15f ), transform: tx );

		var mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );
		var plane = new Plane( tx.PointToWorld( center ), Vector3.Up );
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
		var surfaceLocalPos = sample.SampleLocalPosition + _dirToSurface * distance;
		var surfaceWorldPos = SelectedMeshGameObject.WorldTransform.PointToWorld( surfaceLocalPos );
		DebugOverlay.Sphere( new Sphere( surfaceWorldPos, 0.5f ), color: Color.Blue, overlay: false );
		DebugOverlay.Line( surfaceWorldPos, surfaceWorldPos + tx.NormalToWorld( sample.SurfaceNormal ) * 3f, color: Color.Green, overlay: false );
	}

	private void DrawOctree( Transform tx, SparseVoxelOctree<int[]> octree )
	{
		void DrawChildren( SparseVoxelOctree<int[]>.OctreeNode node )
		{
			var pos = node.Position - octree.Size / 2;
			var bbox = new BBox( pos, pos + node.Size );
			var color = Color.Blue.WithAlpha( 0.15f );
			if ( node.IsLeaf )
			{
				color = Color.Yellow.WithAlpha( 0.35f );
			}
			DebugOverlay.Box( bbox, color, transform: tx );
			foreach( var child in node.Children )
			{
				if ( child is null )
					continue;

				DrawChildren( child );
			}
		}

		DrawChildren( octree.RootNode );
	}

	private void UpdateUI()
	{
		ImGui.SetNextWindowPos( new Vector2( 875, 50 ) * ImGuiStyle.UIScale );
		if ( ImGui.Begin( "Mesh Distance Field" ) )
		{
			DrawWindow();
		}
		ImGui.End();
	}

	private void DrawWindow()
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
		ImGui.Text( $"Mesh Mins: {Mdf.Bounds.Mins}" );
		ImGui.Text( $"Mesh Maxs: {Mdf.Bounds.Maxs}" );
		ImGui.Text( $"Voxel Size: {Mdf.VoxelSize:F3}" );
		ImGui.Text( $"Mouse Distance: {_sdf:F3}" );
		ImGui.Text( $"Mouse Direction: {_dirToSurface.x:F2},{_dirToSurface.y:F2},{_dirToSurface.z:F2}" );

		if ( ImGui.Button( "Regenerate MDF" ) )
		{
			MeshDistanceSystem.Current.RemoveMdf( Mdf.Id );
		}
	}
}
