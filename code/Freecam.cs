namespace Sandbox;

public class Freecam : Component
{
	[Property] public Angles CameraAngles { get; set; }
	[Property] public float FreecamSpeed { get; set; } = 75f;
	[Property] public float FreecamDuckFactor { get; set; } = 0.5f;
	[Property] public float FreecamRunFactor { get; set; } = 2.5f;

	protected override void OnUpdate()
	{
		UpdateCamera();
	}

	private void UpdateCamera()
	{
		if ( Input.Down( "attack2" ) )
		{
			// For some reason, Input.AnalogMove doesn't work. A problem with ImGui?
			var mouseDelta = new Angles( Mouse.Delta.y, -Mouse.Delta.x, 0f );
			CameraAngles += mouseDelta * Time.Delta * Preferences.Sensitivity;
		}

		var camera = Scene.Camera;
		if ( !camera.IsValid() )
			return;

		var speed = FreecamSpeed;
		if ( Input.Down( "duck" ) )
			speed *= FreecamDuckFactor;
		else if ( Input.Down( "run" ) )
			speed *= FreecamRunFactor;

		camera.WorldPosition += Input.AnalogMove * CameraAngles * speed * Time.Delta;
		camera.WorldRotation = CameraAngles;
	}
}
