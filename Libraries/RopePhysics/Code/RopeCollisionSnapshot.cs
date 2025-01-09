namespace Duccsoft;

public class RopeCollisionSnapshot
{
	public Dictionary<int, SphereCollisionInfo> SphereColliders { get; } = [];
	public Dictionary<int, BoxCollisionInfo> BoxColliders { get; } = [];
	public Dictionary<int, CapsuleCollisionInfo> CapsuleColliders { get; } = [];
	public Dictionary<int, MeshCollisionInfo> MeshColliders { get; } = [];
	public bool ShouldCaptureSnapshot { get; set; } = true;
}
