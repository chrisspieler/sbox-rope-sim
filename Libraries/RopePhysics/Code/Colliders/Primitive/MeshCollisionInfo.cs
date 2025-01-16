namespace Duccsoft;

public struct MeshCollisionInfo
{
	public int Id;
	public SignedDistanceField Sdf;
	public Transform Transform;
	public List<int> CollidingPoints;

	public MeshCollisionInfo()
	{
		CollidingPoints = [];
	}

	public GpuMeshCollisionInfo AsGpu()
		=> new()
		{
			LocalToWorld = Transform.GetLocalToWorld(),
			WorldToLocal = Transform.GetWorldToLocal(),
			SdfTextureIndex = Sdf.DataTexture.Index,
			MinsWs = Sdf.Bounds.Mins,
			TextureSize = Sdf.TextureSize,
			MaxsWs = Sdf.Bounds.Maxs,
		};
}

public struct GpuMeshCollisionInfo
{
	public Matrix LocalToWorld;
	public Matrix WorldToLocal;
	public int SdfTextureIndex;
	public Vector3 MinsWs;
	public int TextureSize;
	public Vector3 MaxsWs;
}
