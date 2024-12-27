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
	public GameObject SelectedMeshGameObject => MeshContainer?.Children[SelectedMeshIndex];
	

	private MeshDistanceField _mdf;
	protected override void OnUpdate()
	{
		UpdateMdf();
		if ( _mdf is null )
		{
			_copiedTex?.Dispose();
			_copiedTex = null;
		}
		UpdateInput();
		UpdateUI();
	}

	private void UpdateMdf()
	{
		var tr = Scene.Trace
			.Sphere( 1000f, Vector3.Zero, Vector3.Zero )
			.WithTag( "mdf_demo")
			.Run();
		if ( !tr.Hit || tr.Shape is null )
		{
			_mdf = null;
			return;
		}

		if ( !MeshDistanceSystem.Current.TryGetMdf( tr.Shape, out var mdf ) )
		{
			_mdf = null;
			return;
		}

		_mdf = mdf;
	}

	private float _sdf;
	private Vector3 _dirToSurface;
	private Angles _cameraAngles => new Angles( -25f, -180f, 0 );
	private float _cameraDistance = 200;

	private void UpdateInput()
	{
		if ( _mdf is null )
			return;

		var tx = SelectedMeshGameObject.WorldTransform;

		var mins = _mdf.Bounds.Mins;
		var maxs = _mdf.Bounds.Maxs;
		var z = ((float)_textureSlice).Remap( 0, _maxSlice, mins.z, maxs.z );
		var center = _mdf.Bounds.Center.WithZ( z );
		_cameraDistance = maxs.x * 8f;

		var camera = Scene.Camera;
		var worldCenter = tx.PointToWorld( center );
		camera.WorldPosition = worldCenter + _cameraAngles.Forward * _cameraDistance;
		camera.WorldRotation = Rotation.LookAt( Vector3.Direction( camera.WorldPosition, worldCenter ) );

		DebugOverlay.Box( BBox.FromPositionAndSize( center, ( _mdf.Bounds.Size * 1.5f ).WithZ( 1f ) ), color: Color.White.WithAlpha( 0.15f ), transform: tx );
		DebugOverlay.Line( center.WithX( mins.x ), center.WithX( maxs.x ), color: Color.White.WithAlpha( 0.15f ), transform: tx );
		DebugOverlay.Line( center.WithY( mins.y ), center.WithY( maxs.y ), color: Color.White.WithAlpha( 0.15f ), transform: tx );

		var mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );
		var plane = new Plane( tx.PointToWorld( center ), Vector3.Up );
		var tracePos = plane.Trace( mouseRay, true );
		if ( !tracePos.HasValue )
			return;

		var mouseWorldPos = tracePos.Value;
		var mouseLocalPos = tx.PointToLocal( mouseWorldPos );
		var sample = _mdf.Sample( mouseLocalPos );
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

	private int _maxSlice;
	private int _textureSlice = -1;
	private ComputeShader _textureSliceCs = new( "texture_slice_copy_cs" );
	private Texture _copiedTex;

	private void UpdateUI()
	{
		ImGui.SetNextWindowPos( new Vector2( 900, 50 ) * ImGuiStyle.UIScale );
		if ( ImGui.Begin( "Mesh Distance Field" ) )
		{
			if ( _mdf is null )
			{
				ImGui.Text( "No mesh distance field generated." );
				return;
			}
			ImGui.Text( $"Selected Mesh: {MeshContainer.Children[SelectedMeshIndex].Name}" );
			if ( ImGui.Button( "Previous Mesh" ) )
			{
				SelectedMeshIndex--;
			}
			ImGui.SameLine(); 
			if ( ImGui.Button( "Next Mesh" ) )
			{
				SelectedMeshIndex++;
			}
			ImGui.NewLine();

			ImGui.Text( $"Voxel Size: {_mdf.VoxelSize.x:F3},{_mdf.VoxelSize.y:F3},{_mdf.VoxelSize.z:F3}" );
			ImGui.Text( $"Mouse Distance: {_sdf:F3}" );
			ImGui.Text( $"Mouse Direction: {_dirToSurface.x:F2},{_dirToSurface.y:F2},{_dirToSurface.z:F2}" );

			if ( ImGui.Button( "Regenerate MDF" ) )
			{
				MeshDistanceSystem.Current.RemoveMdf( _mdf.Id );
			}
		}
		ImGui.End();

		ImGui.SetNextWindowPos( new Vector2( 50, 50 ) * ImGuiStyle.UIScale );
		if ( ImGui.Begin( "Volume Texture Viewer" ) )
		{
			var voxelCount = _mdf.Volume.VoxelCount;
			ImGui.Text( $"Size: {voxelCount.x}x{voxelCount.y}x{voxelCount.z}" );
			ImGui.Text( "Slice:" ); ImGui.SameLine();
			_maxSlice = voxelCount.z - 1;
			_textureSlice = _textureSlice.Clamp( 0, _maxSlice );
			var newSlice = _textureSlice;
			ImGui.SliderInt( nameof( _textureSlice ), ref newSlice, 0, _maxSlice );
			if ( _copiedTex is null || newSlice != _textureSlice )
			{
				_textureSlice = newSlice.Clamp( 0, _maxSlice );
				_copiedTex = CopyMdfTexture( _textureSlice );
			}
			ImGui.Image( _copiedTex, new Vector2( 400 ) * ImGuiStyle.UIScale, new Vector2(0, 0), new Vector2( 1, 1 ), Color.Transparent, ImGui.GetColorU32( ImGuiCol.Border ) );
		}
		ImGui.End();
	}

	private Texture CopyMdfTexture( int slice )
	{
		var input = _mdf.Volume.Texture;
		var output = Texture.Create( input.Width, input.Height, ImageFormat.RGBA32323232F )
			.WithUAVBinding()
			.Finish();

		_textureSliceCs.Attributes.Set( "InputTexture", input );
		_textureSliceCs.Attributes.Set( "OutputTexture", output );
		_textureSliceCs.Attributes.Set( "Slice", slice );
		_textureSliceCs.Dispatch( output.Width, output.Height );
		return output;
	}
}
