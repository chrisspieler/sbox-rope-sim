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
		UpdateUI();
	}

	private void UpdateMdf()
	{
		var tr = Scene.Trace
			.Sphere( 1000f, Vector3.Zero, Vector3.Zero )
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
			var mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );
			var plane = new Plane( MeshContainer.WorldPosition, Vector3.Direction( MeshContainer.WorldPosition, Scene.Camera.WorldPosition ) );
			var mouseWorldPos = plane.Trace( mouseRay, true );
			var mouseLocalPos = SelectedMeshGameObject.WorldTransform.PointToLocal( mouseWorldPos ?? float.PositiveInfinity );
			var sample = _mdf.Sample( mouseLocalPos );
			var sdf = sample.SignedDistance;
			ImGui.Text( $"Mouse Distance: {sdf:F3}" );
			var dirToSurface = sample.Direction;
			ImGui.Text( $"Mouse Direction: {dirToSurface.x:F2},{dirToSurface.y:F2},{dirToSurface.z:F2}" );

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
			_textureSlice = _textureSlice.Clamp( 0, voxelCount.z - 1 );
			var newSlice = _textureSlice;
			ImGui.SliderInt( nameof( _textureSlice ), ref newSlice, 0, voxelCount.z - 1 );
			if ( _copiedTex is null || newSlice != _textureSlice )
			{
				_textureSlice = newSlice.Clamp( 0, voxelCount.z - 1 );
				_copiedTex = CopyMdfTexture( _textureSlice );
			}
			ImGui.Image( _copiedTex, new Vector2( 400 ) * ImGuiStyle.UIScale, Color.Transparent, ImGui.GetColorU32( ImGuiCol.Border ) );
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
