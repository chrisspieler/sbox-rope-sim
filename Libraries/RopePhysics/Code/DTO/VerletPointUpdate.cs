namespace Duccsoft;

public struct VerletPointUpdate
{
	public VerletPointUpdate()
	{
		
	}

	public Vector3 PointPosition;
	public int PointIndex;
	public VerletPointUpdateFlags UpdateFlags;
	public VerletPointFlags PointFlags;

	public static VerletPointUpdate All( int index, Vector3 position, VerletPointFlags pointFlags )
	{
		return new VerletPointUpdate
		{
			PointIndex = index,
			UpdateFlags = VerletPointUpdateFlags.All,
			PointPosition = position,
			PointFlags = pointFlags,
		};
	}

	public static VerletPointUpdate Position( int index, Vector3 position )
	{
		return new VerletPointUpdate
		{
			PointIndex = index,
			UpdateFlags = VerletPointUpdateFlags.Position,
			PointPosition = position,
		};
	}

	public static VerletPointUpdate Flags( int index, VerletPointFlags pointFlags )
	{
		return new VerletPointUpdate
		{
			PointIndex = index,
			UpdateFlags = VerletPointUpdateFlags.Flags,
			PointFlags = pointFlags,
		};
	}
}

public enum VerletPointUpdateFlags : int
{
	None		= 0,
	Position	= 1 << 0,
	Flags		= 2 << 0,
	All			= Position | Flags
}
