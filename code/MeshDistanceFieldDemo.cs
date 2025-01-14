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
				if ( Mdf is not null )
				{
					// TODO: Create an MDF cache with a reference counter so I don't have to delete this.
					MeshDistanceSystem.Current.RemoveMdf( Mdf.Id );
				}
				OnSelectedGameObjectChanged();
			}
		}
	}
	private int _selectedMeshIndex = 0;
	[Property] public MdfModelViewer ModelViewer { get; set; }
	[Property] public bool ShouldRotate { get; set; } = false;
	[Property] public float RotationSpeed { get; set; } = 0f;
	[Property] public Freecam Freecam { get; set; }

	public GameObject SelectedMeshGameObject => MeshContainer?.Children[SelectedMeshIndex];
	public MeshDistanceField Mdf { get; private set; }
	private bool _waitForMesh = true;

	protected override void OnStart()
	{
		OnSelectedGameObjectChanged();
		Freecam = Scene.GetAllComponents<Freecam>().FirstOrDefault()
			?? Components.Create<Freecam>();
	}

	protected override void OnUpdate()
	{
		UpdateWaitForMesh();
		UpdateRotation();
		UpdateUI();
	}

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
		if ( !SelectedMeshGameObject.IsValid() || !TryUpdateMdf() )
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

	private bool TryUpdateMdf()
	{
		if ( !SelectedMeshGameObject.Components.TryGet( out MeshDistanceConfig config ) )
		{
			return false;
		}
		
		Mdf = MeshDistanceSystem.Current.GetOrCreate( config );
		ModelViewer.Mdf = Mdf;
		ModelViewer.MdfGameObject = SelectedMeshGameObject;
		return true;
	}

	private void LookAtMesh()
	{
		var camera = Scene.Camera;
		var tx = SelectedMeshGameObject.WorldTransform;
		var worldCenter = tx.PointToWorld( Mdf.Bounds.Center );
		
		camera.WorldPosition = tx.Position + new Vector3( Mdf.Bounds.Size.x * -3f, 0f, Mdf.Bounds.Size.z * 1.5f );
		Freecam.CameraAngles = Rotation.LookAt( Vector3.Direction( camera.WorldPosition, worldCenter ) );
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

		ImGui.Text( "Use RMB and WASD to move the camera!" );
		ImGui.NewLine();
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
