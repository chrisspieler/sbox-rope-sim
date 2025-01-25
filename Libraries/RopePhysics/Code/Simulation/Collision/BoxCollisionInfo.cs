namespace Duccsoft;

public struct BoxCollisionInfo : IGpuCollider<GpuBoxCollisionInfo>
{
	public int Id;
	public Vector3 Size;
	public Transform Transform;

	public GpuBoxCollisionInfo AsGpu()
		=> new()
		{
			Size = Size,
			Scale = Transform.Scale,
			LocalToWorld = Transform.GetLocalToWorld(),
			WorldToLocal = Transform.GetWorldToLocal(),
		};
}

public struct GpuBoxCollisionInfo
{
	public Vector3 Size;
	public Vector3 Scale;
	public Matrix LocalToWorld;
	public Matrix WorldToLocal;
}
