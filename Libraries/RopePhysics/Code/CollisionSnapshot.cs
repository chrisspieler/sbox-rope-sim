﻿namespace Duccsoft;

public class CollisionSnapshot : IDataSize
{
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
			if ( !_gpuSphereColliders.IsValid() )
			{
				_gpuSphereColliders = new( MAX_SPHERE_COLLIDERS );
			}
			return _gpuSphereColliders;
		}
	}
	private GpuBuffer<GpuSphereCollisionInfo> _gpuSphereColliders;
	public GpuBuffer<GpuBoxCollisionInfo> GpuBoxColliders 
	{ 
		get
		{
			if ( !_gpuBoxColliders.IsValid() )
			{
				_gpuBoxColliders = new( MAX_BOX_COLLIDERS );
			}
			return _gpuBoxColliders;
		}
	}
	private GpuBuffer<GpuBoxCollisionInfo> _gpuBoxColliders;
	public GpuBuffer<GpuCapsuleCollisionInfo> GpuCapsuleColliders 
	{ 
		get
		{
			if ( !_gpuCapsuleColliders.IsValid() )
			{
				_gpuCapsuleColliders = new( MAX_CAPSULE_COLLIDERS );
			}
			return _gpuCapsuleColliders;
		}
	}
	private GpuBuffer<GpuCapsuleCollisionInfo> _gpuCapsuleColliders;
	public GpuBuffer<GpuMeshCollisionInfo> GpuMeshColliders
	{
		get
		{
			if ( !_gpuMeshColliders.IsValid() )
			{
				_gpuMeshColliders = new( MAX_MESH_COLLIDERS );
			}
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

			if ( _gpuSphereColliders is not null )
			{
				dataSize += _gpuSphereColliders.ElementCount * _gpuSphereColliders.ElementSize;
			}

			if ( _gpuBoxColliders is not null )
			{
				dataSize += _gpuBoxColliders.ElementCount * _gpuBoxColliders.ElementSize;
			}

			if ( _gpuCapsuleColliders is not null )
			{
				dataSize += _gpuCapsuleColliders.ElementCount * _gpuCapsuleColliders.ElementSize;
			}

			// SDF textures may be used by multiple different snapshots,
			// so we don't add their sizes to the collision snapshot size.
			if ( _gpuMeshColliders is not null )
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
	}

	public void ApplyColliderAttributes( RenderAttributes attributes )
	{
		var sphereColliders = SphereColliders.Values.Take( MAX_SPHERE_COLLIDERS )
			.Select( c => c.AsGpu() )
			.ToArray();
		attributes.Set( "NumSphereColliders", sphereColliders.Length );
		if ( sphereColliders.Length > 0 )
		{
			GpuSphereColliders.SetData( sphereColliders, 0 );
			attributes.Set( "SphereColliders", GpuSphereColliders );
		}
		var boxColliders = BoxColliders.Values.Take( MAX_BOX_COLLIDERS )
			.Select( c => c.AsGpu() )
			.ToArray();
		attributes.Set( "NumBoxColliders", boxColliders.Length );
		if ( boxColliders.Length > 0 )
		{
			GpuBoxColliders.SetData( boxColliders, 0 );
			attributes.Set( "BoxColliders", GpuBoxColliders );
		}
		var capsuleColliders = CapsuleColliders.Values.Take( MAX_CAPSULE_COLLIDERS )
			.Select( c => c.AsGpu() )
			.ToArray();
		attributes.Set( "NumCapsuleColliders", capsuleColliders.Length );
		if ( capsuleColliders.Length > 0 )
		{
			GpuCapsuleColliders.SetData( capsuleColliders, 0 );
			attributes.Set( "CapsuleColliders", GpuCapsuleColliders );
		}
		var meshColliders = MeshColliders.Values.Take( MAX_MESH_COLLIDERS )
			.Select( c =>
				{
					c.Sdf.DataTexture.MarkUsed();
					return c.AsGpu();
				}
			).ToArray();
		attributes.Set( "NumMeshColliders", meshColliders.Length );
		GpuMeshColliders.SetData( meshColliders, 0 );
		attributes.Set( "MeshColliders", GpuMeshColliders );
	}
}
