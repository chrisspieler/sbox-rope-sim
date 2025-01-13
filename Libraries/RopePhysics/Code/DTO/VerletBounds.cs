namespace Duccsoft;

public struct VerletBounds
{
	public Vector4 Mins;
	public Vector4 Maxs;

	public readonly BBox AsBBox() => new( Mins, Maxs );
	public static implicit operator BBox( VerletBounds bounds ) => bounds.AsBBox();
}
