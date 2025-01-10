namespace Duccsoft;

public struct VerletPoint
{
	public VerletPoint( Vector3 position, Vector3 lastPosition )
	{
		Position = position;
		LastPosition = lastPosition;
	}

	public Vector3 Position;
	public int Flags;
	public Vector3 LastPosition;
}
