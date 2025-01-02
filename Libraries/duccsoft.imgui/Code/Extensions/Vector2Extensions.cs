using System;

namespace Duccsoft.ImGui.Extensions;

public static class Vector2Extensions
{
	public static Vector2 RotateUV( this Vector2 uv, float rotationRadians )
	{
		// Taken from: https://discussions.unity.com/t/rotate-uv-of-object-by-90-degrees/396993/13

		float rotMatrix00 = MathF.Cos( rotationRadians );
		float rotMatrix01 = -MathF.Sin( rotationRadians );
		float rotMatrix10 = MathF.Sin( rotationRadians );
		float rotMatrix11 = MathF.Cos( rotationRadians );

		Vector2 halfVector = new( 0.5f, 0.5f );

		// Switch coordinates to be relative to center of the plane
		uv -= halfVector;
		// Apply the rotation matrix
		float u = rotMatrix00 * uv.x + rotMatrix01 * uv.y;
		float v = rotMatrix10 * uv.x + rotMatrix11 * uv.y;
		uv.x = u;
		uv.y = v;
		// Switch back coordinates to be relative to edge
		uv += halfVector;
		return uv;
	}
}
