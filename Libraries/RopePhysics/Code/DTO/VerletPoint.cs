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

	public bool IsRopeLocal
	{
		get => Flags.HasFlag( VerletPointFlags.RopeLocal );
		set => Flags = Flags.WithFlag( VerletPointFlags.RopeLocal, value );
	}

	public readonly VerletVertex AsRopeVertex( VerletRope rope )
	{
		return new VerletVertex()
		{
			Position = new Vector4( Position, 0 ),
			TexCoord0 = new Vector4( rope.EffectiveRadius * rope.RenderWidthScale, 1f, 0, 0 ),
			Normal = new Vector4( 0, 0, 1, 0 ),
			Tangent0 = new Vector4( Position - LastPosition, 0 ),
			TexCoord1 = rope.Color,
			Color0 = Vector4.One,
		};
	}
}

[Flags]
public enum VerletPointFlags : int
{
	None			= 0,
	Anchor			= 1 << 0,
	RopeLocal		= 1 << 1,
}
