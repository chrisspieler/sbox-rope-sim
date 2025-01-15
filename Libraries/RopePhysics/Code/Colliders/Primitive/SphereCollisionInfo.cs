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
	{
		Matrix localToWorld = Matrix.CreateScale( Transform.Scale )
			* Matrix.CreateRotation( Transform.Rotation )
			* Matrix.CreateTranslation( Transform.Position );
		Matrix worldToLocal = localToWorld.Inverted;
		return new GpuSphereCollisionInfo
		{
			Center = Center,
			Radius = Radius,
			LocalToWorld = localToWorld,
			WorldToLocal = worldToLocal,
		};
	}
}

public struct GpuSphereCollisionInfo
{
	public Vector3 Center;
	public float Radius;
	public Matrix LocalToWorld;
	public Matrix WorldToLocal;
}
