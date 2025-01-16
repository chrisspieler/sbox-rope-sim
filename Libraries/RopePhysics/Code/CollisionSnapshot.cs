namespace Duccsoft;

public class CollisionSnapshot
{
	public Dictionary<int, SphereCollisionInfo> SphereColliders { get; } = [];
	public Dictionary<int, BoxCollisionInfo> BoxColliders { get; } = [];
	public Dictionary<int, CapsuleCollisionInfo> CapsuleColliders { get; } = [];
	public Dictionary<int, MeshCollisionInfo> MeshColliders { get; } = [];
	public GpuBuffer<GpuSphereCollisionInfo> GpuSphereColliders { get; } = new GpuBuffer<GpuSphereCollisionInfo>( 16 );
	public GpuBuffer<GpuBoxCollisionInfo> GpuBoxColliders { get; } = new GpuBuffer<GpuBoxCollisionInfo>( 16 );
	public bool ShouldCaptureSnapshot { get; set; } = true;

	public void Clear()
	{
		SphereColliders.Clear();
		BoxColliders.Clear();
		CapsuleColliders.Clear();
		MeshColliders.Clear();
	}
}
