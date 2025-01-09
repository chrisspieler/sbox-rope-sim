namespace Duccsoft;

public struct SphereCollisionInfo
{
	public int Id;
	public float Radius;
	public Vector3 Center;
	public Transform Transform;
	public List<int> CollidingPoints;

	public SphereCollisionInfo()
	{
		CollidingPoints = new();
	}
}
