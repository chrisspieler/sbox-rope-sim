namespace Duccsoft;

public struct VerletPoint
{
	public VerletPoint( Vector3 position, Vector3 lastPosition )
	{
		Position = position;
		LastPosition = lastPosition;
	}

	public Vector3 Position;
	public VerletPointFlags Flags;
	public Vector3 LastPosition;
	public int Padding;

	public readonly bool IsAnchor => Flags.HasFlag( VerletPointFlags.Anchor );
}

[Flags]
public enum VerletPointFlags : int
{
	None	= 0,
	Anchor	= 1 << 0
}
