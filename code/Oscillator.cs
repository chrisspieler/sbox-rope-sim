using System;

namespace Sandbox;

public class Oscillator : Component
{
	[Property, Range( 0f, 1024f)] public Vector3 Amplitude { get; set; } = new Vector3( 0f, 64f, 0f );
	[Property, Range( 0.01f, 10f)] public Vector3 Period { get; set; } = Vector3.One;

	protected override void OnFixedUpdate()
	{
		var frequency = 1f / Period.Clamp( 0.01f, 10f ) * MathF.PI;
		var progress = new Vector3
		{
			x = MathF.Sin( Time.Now * frequency.x ),
			y = MathF.Sin( Time.Now * frequency.y ),
			z = MathF.Sin( Time.Now * frequency.z ),
		};
		LocalPosition = Amplitude * progress;
	}
}
