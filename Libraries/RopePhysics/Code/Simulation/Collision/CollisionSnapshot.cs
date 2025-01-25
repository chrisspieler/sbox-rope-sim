namespace Duccsoft;

public class CollisionSnapshot : IDataSize
{
	public const int INITIAL_COLLIDER_BUFFER_SIZE = 4;

	public const int MAX_SPHERE_COLLIDERS = 256;
	public const int MAX_BOX_COLLIDERS = 256;
	public const int MAX_CAPSULE_COLLIDERS = 256;
	public const int MAX_MESH_COLLIDERS = 256;

	public Dictionary<int, SphereCollisionInfo> SphereColliders { get; } = [];
	public Dictionary<int, BoxCollisionInfo> BoxColliders { get; } = [];
	public Dictionary<int, CapsuleCollisionInfo> CapsuleColliders { get; } = [];
	public Dictionary<int, MeshCollisionInfo> MeshColliders { get; } = [];

	public GpuBuffer<GpuSphereCollisionInfo> GpuSphereColliders 
	{ 
		get
		{
			GpuBufferUtils.EnsureMinCount( ref _gpuSphereColliders, INITIAL_COLLIDER_BUFFER_SIZE );
			return _gpuSphereColliders;
		}
	}
	private GpuBuffer<GpuSphereCollisionInfo> _gpuSphereColliders;
	public GpuBuffer<GpuBoxCollisionInfo> GpuBoxColliders 
	{ 
		get
		{
			GpuBufferUtils.EnsureMinCount( ref _gpuBoxColliders, INITIAL_COLLIDER_BUFFER_SIZE );
			return _gpuBoxColliders;
		}
	}
	private GpuBuffer<GpuBoxCollisionInfo> _gpuBoxColliders;
	public GpuBuffer<GpuCapsuleCollisionInfo> GpuCapsuleColliders 
	{ 
		get
		{
			GpuBufferUtils.EnsureMinCount( ref _gpuCapsuleColliders, INITIAL_COLLIDER_BUFFER_SIZE );
			return _gpuCapsuleColliders;
		}
	}
	private GpuBuffer<GpuCapsuleCollisionInfo> _gpuCapsuleColliders;
	public GpuBuffer<GpuMeshCollisionInfo> GpuMeshColliders
	{
		get
		{
			GpuBufferUtils.EnsureMinCount( ref _gpuMeshColliders, INITIAL_COLLIDER_BUFFER_SIZE );
			return _gpuMeshColliders;
		}
	}
	private GpuBuffer<GpuMeshCollisionInfo> _gpuMeshColliders;
	public bool ShouldCaptureSnapshot { get; set; } = true;

	public long DataSize
	{
		get
		{
			long dataSize = 0;

			if ( _gpuSphereColliders.IsValid() )
			{
				dataSize += _gpuSphereColliders.ElementCount * _gpuSphereColliders.ElementSize;
			}

			if ( _gpuBoxColliders.IsValid() )
			{
				dataSize += _gpuBoxColliders.ElementCount * _gpuBoxColliders.ElementSize;
			}

			if ( _gpuCapsuleColliders.IsValid() )
			{
				dataSize += _gpuCapsuleColliders.ElementCount * _gpuCapsuleColliders.ElementSize;
			}

			// SDF textures may be used by multiple different snapshots,
			// so we don't add their sizes to the collision snapshot size.
			if ( _gpuMeshColliders.IsValid() )
			{ 
				dataSize += _gpuMeshColliders.ElementCount * _gpuMeshColliders.ElementSize;
			}

			return dataSize;
		}
	}

	public void Clear()
	{
		SphereColliders.Clear();
		BoxColliders.Clear();
		CapsuleColliders.Clear();
		MeshColliders.Clear();

		// Don't clear the GPU buffers. We can just overwrite the data already there.
	}

	private static void ApplyColliderAttribute<T, U>( 
			RenderAttributes attributes, string numCollidersAttribute, string colliderBufferAttribute,
			Dictionary<int, U> cpuColliders, int maxColliders, ref GpuBuffer<T> gpuBuffer 
		) 
		where T : unmanaged 
		where U : IGpuCollider<T>
	{
		var gpuColliders = cpuColliders.Values
			.Take( maxColliders )
			.Select( c => c.AsGpu() )
			.ToArray();
		var colliderCount = gpuColliders.Length;
		attributes.Set( numCollidersAttribute, colliderCount );
		if ( colliderCount > 0 )
		{
			GpuBufferUtils.EnsureCount( ref gpuBuffer, colliderCount );
			gpuBuffer.SetData( gpuColliders, 0 );
			attributes.Set( colliderBufferAttribute, gpuBuffer );
		}
	}

	public void ApplyColliderAttributes( RenderAttributes attributes )
	{
		ApplyColliderAttribute( attributes, "NumSphereColliders", "SphereColliders", SphereColliders, MAX_SPHERE_COLLIDERS, ref _gpuSphereColliders );
		ApplyColliderAttribute( attributes, "NumBoxColliders", "BoxColliders", BoxColliders, MAX_BOX_COLLIDERS, ref _gpuBoxColliders );
		ApplyColliderAttribute( attributes, "NumCapsuleColliders", "CapsuleColliders", CapsuleColliders, MAX_CAPSULE_COLLIDERS, ref _gpuCapsuleColliders );
		ApplyColliderAttribute( attributes, "NumMeshColliders", "MeshColliders", MeshColliders, MAX_MESH_COLLIDERS, ref _gpuMeshColliders );
	}
}
