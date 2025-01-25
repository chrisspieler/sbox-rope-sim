namespace Duccsoft;

public struct VerletPoint
{
	public const int DATA_SIZE 
		= sizeof(int)			// Flags
		+ sizeof(float) * 3		// Position
		+ sizeof(float) * 3;	// LastPosition

	public VerletPoint( Vector3 position, Vector3 lastPosition, VerletPointFlags flags = VerletPointFlags.None )
	{
		Flags = flags;
		Position = position;
		LastPosition = lastPosition;
	}

	public VerletPointFlags Flags;
	public Vector3 Position;
	public Vector3 LastPosition;

	public bool IsAnchor
	{
		readonly get => Flags.HasFlag( VerletPointFlags.Anchor );
		set => Flags = Flags.WithFlag( VerletPointFlags.Anchor, value );
	}

	public bool IsRopeLocal
	{
		readonly get => Flags.HasFlag( VerletPointFlags.RopeLocal );
		set => Flags = Flags.WithFlag( VerletPointFlags.RopeLocal, value );
	}

	public readonly RopeVertex AsRopeVertex( VerletRope rope )
	{
		return new RopeVertex()
		{
			Position = new Vector4( Position, 0 ),
			TexCoord0 = new Vector4( rope.EffectiveRadius * 2 * rope.RenderWidthScale, 1f, 0, 0 ),
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
