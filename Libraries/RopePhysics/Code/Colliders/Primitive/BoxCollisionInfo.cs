namespace Duccsoft;

public struct BoxCollisionInfo
{
	public int Id;
	public Vector3 Size;
	public Transform Transform;
	public List<int> CollidingPoints;

    public BoxCollisionInfo()
    {
		CollidingPoints = new();
    }
}
