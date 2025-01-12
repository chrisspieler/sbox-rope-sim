namespace Duccsoft;

public struct VerletPoint
{
	public VerletPoint( Vector3 position, Vector3 lastPosition, VerletPointFlags flags = VerletPointFlags.None )
	{
		Position = position;
		LastPosition = lastPosition;
		Flags = flags;
	}

	public Vector3 Position;
	public VerletPointFlags Flags;
	public Vector3 LastPosition;
	public int Padding;

	public bool IsAnchor
	{
		get => Flags.HasFlag( VerletPointFlags.Anchor );
		set => Flags = Flags.WithFlag( VerletPointFlags.Anchor, value );
	}
}

[Flags]
public enum VerletPointFlags : int
{
	None	= 0,
	Anchor	= 1 << 0
}
