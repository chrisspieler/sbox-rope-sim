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
	private bool _waitForMesh = true;

	protected override void OnUpdate()
	{
		UpdateMdf();
		UpdateCamera();
		UpdateUI();
	}

	private Angles _cameraAngles;

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

		if ( Mdf?.IsMeshBuilt == true && _waitForMesh )
		{
			_waitForMesh = false;
			LookAtMesh();
		}
	}

	private void LookAtMesh()
	{
		var camera = Scene.Camera;
		var tx = SelectedMeshGameObject.WorldTransform;
		var worldCenter = tx.PointToWorld( Mdf.Bounds.Center );
		
		camera.WorldPosition = tx.Position + new Vector3( Mdf.Bounds.Size.x * -4f, 0f, Mdf.Bounds.Size.z * 1.5f );
		_cameraAngles = Rotation.LookAt( Vector3.Direction( camera.WorldPosition, worldCenter ) );
	}

	private float FreecamSpeed => 75f;

	private void UpdateCamera()
	{
		if ( Input.Down( "attack2" ) )
		{
			// For some reason, Input.AnalogMove doesn't work. A problem with ImGui?
			var mouseDelta = new Angles( Mouse.Delta.y, -Mouse.Delta.x, 0f );
			_cameraAngles += mouseDelta * Time.Delta * Preferences.Sensitivity;
		}

		var camera = Scene.Camera;
		if ( !camera.IsValid() )
			return;

		var speed = FreecamSpeed;
		if ( Input.Down( "duck" ) )
			speed *= 0.5f;
		else if ( Input.Down( "run" ) )
			speed *= 2.5f;

		camera.WorldPosition += Input.AnalogMove * _cameraAngles * speed * Time.Delta;
		camera.WorldRotation = _cameraAngles;
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
			_waitForMesh = true;
			// TODO: Create an MDF cache with a reference counter so I don't have to delete this.
			system.RemoveMdf( Mdf.Id );
		}
	}
}
