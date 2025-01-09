namespace Duccsoft;

public struct CapsuleCollisionInfo
{
	public int Id;
	public Vector3 Start, End;
	public float Radius;
	public Transform Transform;
	public List<int> CollidingPoints;

	public CapsuleCollisionInfo()
	{
		CollidingPoints = new();
	}
}
