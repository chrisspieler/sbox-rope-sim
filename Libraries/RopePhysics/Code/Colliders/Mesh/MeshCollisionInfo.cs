namespace Duccsoft;

public struct MeshCollisionInfo : IGpuCollider<GpuMeshCollisionInfo>
{
	public int Id;
	public SignedDistanceField Sdf;
	public Transform Transform;

	public GpuMeshCollisionInfo AsGpu()
		=> new()
		{
			SdfTextureIndex = Sdf.DataTexture.Index,
			TextureSize = Sdf.TextureSize,
			BoundsSizeOs = Sdf.Bounds.Size.x * Transform.Scale.x,
			LocalToWorld = Transform.GetLocalToWorld(),
			WorldToLocal = Transform.GetWorldToLocal(),
		};
}

public struct GpuMeshCollisionInfo
{
	public int SdfTextureIndex;
	public int TextureSize;
	public float BoundsSizeOs;
	public Matrix LocalToWorld;
	public Matrix WorldToLocal;
}
