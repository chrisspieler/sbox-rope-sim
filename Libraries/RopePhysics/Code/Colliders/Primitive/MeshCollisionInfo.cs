namespace Duccsoft;

public struct MeshCollisionInfo
{
	public int Id;
	public SignedDistanceField Sdf;
	public Transform Transform;
	public List<int> CollidingPoints;

	public MeshCollisionInfo()
	{
		CollidingPoints = new();
	}
}
