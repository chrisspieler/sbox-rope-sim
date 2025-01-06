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
			if ( MeshContainer.IsValid() )
			{
				value = value.UnsignedMod( MeshContainer.Children.Count );
			}
			var changed = value != _selectedMeshIndex;
			_selectedMeshIndex = value;

			if ( !Game.IsPlaying )
				return;

			for ( int i = 0; i < MeshContainer.Children.Count; i++ )
			{
				var child = MeshContainer.Children[i];
				child.Enabled = _selectedMeshIndex == i;
			}
			if ( changed )
			{
				_waitForMesh = true;
				// TODO: Create an MDF cache with a reference counter so I don't have to delete this.
				MeshDistanceSystem.Current.RemoveMdf( Mdf.Id );
				OnSelectedGameObjectChanged();
			}
		}
	}
	private int _selectedMeshIndex = 0;
	[Property] public MdfModelViewer ModelViewer { get; set; }
	[Property] public bool ShouldRotate { get; set; } = false;
	[Property] public float RotationSpeed { get; set; } = 0f;

	public GameObject SelectedMeshGameObject => MeshContainer?.Children[SelectedMeshIndex];
	public MeshDistanceField Mdf { get; private set; }
	private bool _waitForMesh = true;

	protected override void OnStart()
	{
		OnSelectedGameObjectChanged();
	}

	protected override void OnUpdate()
	{
		UpdateWaitForMesh();
		UpdateRotation();
		UpdateCamera();
		UpdateUI();
	}

	private Angles _cameraAngles;

	private void UpdateWaitForMesh()
	{
		// TODO: Use a Job callback instead.
		if ( Mdf?.IsMeshBuilt == true && _waitForMesh )
		{
			_waitForMesh = false;
			LookAtMesh();
		}
	}

	private void UpdateRotation()
	{
		if ( !SelectedMeshGameObject.IsValid() || !ShouldRotate )
			return;

		SelectedMeshGameObject.WorldRotation *= Rotation.FromYaw( RotationSpeed * Time.Delta );
	}

	private void OnSelectedGameObjectChanged()
	{
		Log.Info( $"Selected gameobject changed" );
		if ( !SelectedMeshGameObject.IsValid() || (!TryUpdateMdfFromPhysics() && !TryUpdateMdfFromModel()) )
		{
			ClearMdf();
		}
	}

	private void ClearMdf()
	{
		Mdf = null;
		ModelViewer.Mdf = null;
		ModelViewer.MdfGameObject = null;
	}

	private bool TryUpdateMdfFromPhysics()
	{
		if ( !SelectedMeshGameObject.Components.TryGet<Collider>( out var collider ) )
			return false;

		var physicsShape = collider?.KeyframeBody?.Shapes?.FirstOrDefault();
		if ( !physicsShape.IsValid() )
			return false;

		Mdf = MeshDistanceSystem.Current.GetMdf( physicsShape );
		ModelViewer.Mdf = Mdf;
		ModelViewer.MdfGameObject = SelectedMeshGameObject;
		return true;
	}

	private bool TryUpdateMdfFromModel()
	{
		if ( !SelectedMeshGameObject.Components.TryGet<ModelRenderer>( out var renderer ) )
			return false;

		var model = renderer.Model;
		if ( model is null )
			return false;

		Mdf = MeshDistanceSystem.Current.GetMdf( model );
		ModelViewer.Mdf = Mdf;
		ModelViewer.MdfGameObject = SelectedMeshGameObject;
		return true;
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
		if ( ImGui.Button( "Previous Mesh" ) )
		{
			SelectedMeshIndex--;
		}
		ImGui.SameLine();
		if ( ImGui.Button( "Next Mesh" ) )
		{
			SelectedMeshIndex++;
		}
		ImGui.Text( "Rotate Speed:" ); ImGui.SameLine();
		var shouldRotate = ShouldRotate;
		ImGui.Checkbox( "Rotate", ref shouldRotate );
		ShouldRotate = shouldRotate;
		if ( shouldRotate )
		{
			var rotSpeed = RotationSpeed;
			ImGui.SliderFloat( nameof( rotSpeed ), ref rotSpeed, -360f, 360f );
			RotationSpeed = rotSpeed;
		}
	}
}
