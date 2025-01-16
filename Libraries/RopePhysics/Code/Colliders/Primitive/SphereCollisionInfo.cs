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

	public GpuSphereCollisionInfo AsGpu()
		=> new()
		{
			Center = Center,
			Radius = Radius,
			LocalToWorld = Transform.GetLocalToWorld(),
			WorldToLocal = Transform.GetWorldToLocal(),
		};
}

public struct GpuSphereCollisionInfo
{
	public Vector3 Center;
	public float Radius;
	public Matrix LocalToWorld;
	public Matrix WorldToLocal;
}
