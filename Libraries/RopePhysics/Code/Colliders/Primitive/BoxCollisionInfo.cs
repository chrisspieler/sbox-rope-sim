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

	public GpuBoxCollisionInfo AsGpu()
		=> new()
		{
			Size = Size,
			LocalToWorld = Transform.GetLocalToWorld(),
			WorldToLocal = Transform.GetWorldToLocal(),
		};
}

public struct GpuBoxCollisionInfo
{
	public Vector3 Size;
	public int Padding;
	public Matrix LocalToWorld;
	public Matrix WorldToLocal;
}
