using Duccsoft;
using Duccsoft.ImGui;

namespace Sandbox;

public class ClothDemo : Component
{
	[Property] public VerletCloth Cloth { get; set; }
	[Property] public Freecam Freecam { get; set; }
	[Property] public float OrbitCamDistance = 75f;
	[Property, Range( 1f, 32f )] public float OrbitCamPositionExpDecay = 8f;
	[Property, Range( 0f, 1f )] public float OrbitCamRotationSmoothTime = 0.5f;

	Angles OrbitAngle;
	bool UnlockResolution = false;
	float CurrentOrbitCamPositionExpDecay;
	float CurrentOrbitCamRotationSmoothTime;
	Vector3 OrbitCamRotationVelocity;
	TimeSince Attack2Pressed;
	TimeSince Attack2Released;

	protected override void OnStart()
	{
		OrbitAngle = Scene.Camera.WorldRotation.Inverse;
		CurrentOrbitCamPositionExpDecay = OrbitCamPositionExpDecay;
		CurrentOrbitCamRotationSmoothTime = OrbitCamRotationSmoothTime;
	}

	protected override void OnPreRender()
	{
		UpdateOrbitCamera();
	}

	private void UpdateOrbitCamera()
	{
		if ( Freecam.Enabled )
			return;

		var cam = Scene.Camera;
		
		var orbitCenter = Cloth.SimData.Bounds.Center;
		if ( Input.Pressed( "attack2" ) )
		{
			Attack2Pressed = 0;
		}
		if ( Input.Released( "attack2" ) )
		{
			Attack2Released = 0;
		}
		if ( Input.Down( "attack2" ) )
		{
			var mouseDelta = new Angles( -Mouse.Delta.y, -Mouse.Delta.x, 0f );
			OrbitAngle += mouseDelta * Time.Delta * Preferences.Sensitivity;
			OrbitAngle = OrbitAngle.WithPitch( OrbitAngle.pitch.Clamp( -89f, 89f ) );
		}
		if ( Input.MouseWheel.y != 0 )
		{
			OrbitCamDistance += -Input.MouseWheel.y * 2000f * Time.Delta;
			OrbitCamDistance = OrbitCamDistance.Clamp( 25f, 500f );
		}
		Ray orbitRay = new( orbitCenter, OrbitAngle.Forward );
		if ( Input.Down( "attack2" ) )
		{
			CurrentOrbitCamPositionExpDecay = CurrentOrbitCamPositionExpDecay.ExpDecayTo( 32f, 2f );
		}
		else
		{
			CurrentOrbitCamPositionExpDecay = CurrentOrbitCamPositionExpDecay.ExpDecayTo( OrbitCamPositionExpDecay, 4f );
		}
		cam.WorldPosition = cam.WorldPosition.ExpDecayTo( orbitRay.Project( OrbitCamDistance ), CurrentOrbitCamPositionExpDecay );
		var targetRot = Rotation.LookAt( (Cloth.SimData.Bounds.Center - cam.WorldPosition).Normal );
		if ( Input.Down( "attack2" ) )
		{
			CurrentOrbitCamRotationSmoothTime = CurrentOrbitCamRotationSmoothTime.ExpDecayTo( 0f, 4f );
		}
		else
		{
			CurrentOrbitCamRotationSmoothTime = CurrentOrbitCamRotationSmoothTime.ExpDecayTo( OrbitCamRotationSmoothTime, 2f );
		}
		cam.WorldRotation = Rotation.SmoothDamp( cam.WorldRotation, targetRot, ref OrbitCamRotationVelocity, CurrentOrbitCamRotationSmoothTime, Time.Delta );
	}

	protected override void OnUpdate()
	{
		if ( ImGui.Begin( "Cloth" ) )
		{
			PaintClothDemoWindow();
		}
		ImGui.End();
	}

	private void PaintClothDemoWindow()
	{
		bool enableFreecam = Freecam.Enabled;
		if ( ImGui.Checkbox( "Enable Freecam", ref enableFreecam ) )
		{
			Freecam.Enabled = enableFreecam;
			if ( enableFreecam )
			{
				Freecam.CameraAngles = Scene.Camera.WorldRotation;
			}
		}
		ImGui.Text( "Position:" ); ImGui.SameLine();
		float position = -Cloth.WorldPosition.y;
		if ( ImGui.SliderFloat( "Position", ref position, -200, 200 ) )
		{
			Cloth.WorldPosition = Cloth.WorldPosition.WithY( -position );
		}
		ImGui.NewLine();
		bool unlockResolution = UnlockResolution;
		if ( ImGui.Checkbox( "Unlock Resolution", ref unlockResolution ) )
		{
			UnlockResolution = unlockResolution;
		}
		if ( UnlockResolution )
		{
			ImGui.Text( "WARNING: Cloth resolutions above 32x32 may be unstable!" );
		}
		ImGui.Text( "Resolution:" ); ImGui.SameLine();
		int resolution = Cloth.ClothResolution;
		int maxRes = UnlockResolution ? 256 : 32;
		if ( ImGui.SliderInt( "Resolution", ref resolution, 4, maxRes ) )
		{
			Cloth.ClothResolution = resolution;
		}
		ImGui.Text( "Iterations:" ); ImGui.SameLine();
		int iterations = Cloth.Iterations;
		if ( ImGui.SliderInt( "Iterations", ref iterations, 1, 80 ) )
		{
			Cloth.Iterations = iterations;
		}

	}
}
