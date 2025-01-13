namespace Sandbox;

public class Rotator : Component
{
	[Property] public Angles RotationPerSecond { get; set; } = new Angles( 0f, 90f, 0f );

	protected override void OnFixedUpdate()
	{
		WorldRotation *= RotationPerSecond * Time.Delta;
	}
}
