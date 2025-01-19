namespace Duccsoft;

public class CollisionSnapshot
{
	public Dictionary<int, SphereCollisionInfo> SphereColliders { get; } = [];
	public Dictionary<int, BoxCollisionInfo> BoxColliders { get; } = [];
	public Dictionary<int, CapsuleCollisionInfo> CapsuleColliders { get; } = [];
	public Dictionary<int, MeshCollisionInfo> MeshColliders { get; } = [];
	public GpuBuffer<GpuSphereCollisionInfo> GpuSphereColliders { get; } = new GpuBuffer<GpuSphereCollisionInfo>( 16 );
	public GpuBuffer<GpuBoxCollisionInfo> GpuBoxColliders { get; } = new GpuBuffer<GpuBoxCollisionInfo>( 16 );
	public GpuBuffer<GpuCapsuleCollisionInfo> GpuCapsuleColliders { get; } = new GpuBuffer<GpuCapsuleCollisionInfo>( 16 );
	public GpuBuffer<GpuMeshCollisionInfo> GpuMeshColliders { get; } = new GpuBuffer<GpuMeshCollisionInfo>( 256 );
	public bool ShouldCaptureSnapshot { get; set; } = true;

	public void Clear()
	{
		SphereColliders.Clear();
		BoxColliders.Clear();
		CapsuleColliders.Clear();
		MeshColliders.Clear();
		
	}

	public void ApplyColliderAttributes( RenderAttributes attributes )
	{
		var sphereColliders = SphereColliders.Values.Take( 16 ).Select( c => c.AsGpu() ).ToArray();
		attributes.Set( "NumSphereColliders", sphereColliders.Length );
		if ( sphereColliders.Length > 0 )
		{
			GpuSphereColliders.SetData( sphereColliders, 0 );
			attributes.Set( "SphereColliders", GpuSphereColliders );
		}
		var boxColliders = BoxColliders.Values.Take( 16 ).Select( c => c.AsGpu() ).ToArray();
		attributes.Set( "NumBoxColliders", boxColliders.Length );
		if ( boxColliders.Length > 0 )
		{
			GpuBoxColliders.SetData( boxColliders, 0 );
			attributes.Set( "BoxColliders", GpuBoxColliders );
		}
		var capsuleColliders = CapsuleColliders.Values.Take( 16 ).Select( c => c.AsGpu() ).ToArray();
		attributes.Set( "NumCapsuleColliders", capsuleColliders.Length );
		if ( capsuleColliders.Length > 0 )
		{
			GpuCapsuleColliders.SetData( capsuleColliders, 0 );
			attributes.Set( "CapsuleColliders", GpuCapsuleColliders );
		}
		var meshColliders = MeshColliders.Values.Take( 256 ).Select( c => c.AsGpu() ).ToArray();
		attributes.Set( "NumMeshColliders", meshColliders.Length );
		if ( meshColliders.Length > 0 )
		{
			GpuMeshColliders.SetData( meshColliders, 0 );
			attributes.Set( "MeshColliders", GpuMeshColliders );
		}
	}
}
