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

	public GpuCapsuleCollisionInfo AsGpu()
		=> new()
		{
			Start = Start,
			Radius = Radius,
			End = End,
			LocalToWorld = Transform.GetLocalToWorld(),
			WorldToLocal = Transform.GetWorldToLocal(),
		};
}

public struct GpuCapsuleCollisionInfo()
{
	public Vector3 Start;
	public float Radius;
	public Vector3 End;
	int Padding;
	public Matrix LocalToWorld;
	public Matrix WorldToLocal;
}
