namespace Duccsoft;

public static class TransformExtensions
{
	public static Matrix GetLocalToWorld( this Transform tx )
	{
		return Matrix.CreateScale( tx.Scale )
		* Matrix.CreateRotation( tx.Rotation )
		* Matrix.CreateTranslation( tx.Position );
	}

	public static Matrix GetWorldToLocal( this Transform tx )
	{
		return tx.GetLocalToWorld().Inverted;
	}
}
