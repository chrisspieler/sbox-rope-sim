namespace Duccsoft;

public struct CapsuleCollisionInfo : IGpuCollider<GpuCapsuleCollisionInfo>
{
	public int Id;
	public Vector3 Start, End;
	public float Radius;
	public Transform Transform;

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
	public float Radius;
	public Vector3 Start;
	public Vector3 End;
	public Matrix LocalToWorld;
	public Matrix WorldToLocal;
}
