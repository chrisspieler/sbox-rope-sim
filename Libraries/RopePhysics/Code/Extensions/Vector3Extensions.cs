using System;

public static class Vector3Extensions
{
	public static Vector3 ExpDecayTo( this Vector3 from, Vector3 target, float decay )
	{
		return target + (from - target) * MathF.Exp( -decay * Time.Delta );
	}
}
