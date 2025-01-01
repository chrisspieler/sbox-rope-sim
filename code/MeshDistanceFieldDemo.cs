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
	[Property] public MdfModelViewer ModelViewer { get; set; }

	public GameObject SelectedMeshGameObject => MeshContainer?.Children[SelectedMeshIndex];
	public MeshDistanceField Mdf { get; private set; }
	
	protected override void OnUpdate()
	{
		UpdateMdf();
		UpdateInput();
		UpdateUI();
	}

	private Angles _cameraAngles => new Angles( -25f, -180f, 0 );
	private float _cameraDistance = 200;

	private void UpdateMdf()
	{
		var mq = MeshDistanceField.FindInSphere( Vector3.Zero, 100f );
		if ( !mq.Hit )
		{
			Mdf = null;
			ModelViewer.MdfGameObject = null;
		}

		Mdf = mq.Mdf;
		ModelViewer.Mdf = mq.Mdf;
		ModelViewer.MdfGameObject = mq.GameObject;
	}

	private void UpdateInput()
	{
		var camera = Scene.Camera;
		var tx = SelectedMeshGameObject.WorldTransform;
		_cameraDistance = Mdf.Bounds.Maxs.x * 8f;

		var worldCenter = tx.PointToWorld( Mdf.Bounds.Center );
		
		camera.WorldPosition = worldCenter + _cameraAngles.Forward * _cameraDistance;
		camera.WorldRotation = Rotation.LookAt( Vector3.Direction( camera.WorldPosition, worldCenter ) );
	}

	private void UpdateUI()
	{
		ImGui.SetNextWindowPos( new Vector2( 875, 50 ) * ImGuiStyle.UIScale );
		if ( ImGui.Begin( "Mesh Distance Field Demo" ) )
		{
			PaintWindow();
		}
		ImGui.End();
	}

	private void PaintWindow()
	{
		var system = MeshDistanceSystem.Current;

		ImGui.Text( $"MDF Count: {system.MdfCount}" ); ImGui.SameLine();
		ImGui.Text( $"Total Data Size: {system.MdfTotalDataSize.FormatBytes()}" );
		ImGui.Text( $"Selected GameObject: {SelectedMeshGameObject.Name}" );
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
			// TODO: Create an MDF cache with a reference counter so I don't have to delete this.
			system.RemoveMdf( Mdf.Id );
		}
	}
}
