namespace Duccsoft;

public partial class VerletSystem
{
	private void SetShouldCaptureSnapshot()
	{
		var verletComponents = Scene.GetAllComponents<VerletComponent>();
		foreach( var verlet in verletComponents )
		{
			if ( verlet.SimData?.Collisions is null )
				continue;

			verlet.SimData.Collisions.ShouldCaptureSnapshot = true;
		}
	}
	public static CollisionSnapshot CaptureCollisionSnapshot( SimulationData simData )
	{
		if ( simData is null || !simData.Physics.IsValid() || simData?.CpuPoints?.Length < 1 )
			return new();

		// TODO: Use a collection of bounds so that very long ropes don't have massive bounding boxes.
		var collisionBounds = simData.Bounds.Grow( 32f );

		// Find possible collisions in the scene.
		var trs = simData.Physics.Trace
			.Box(collisionBounds.Size, collisionBounds.Center, collisionBounds.Center)
			.WithoutTags(simData.CollisionExclude)
			.WithAnyTags(simData.CollisionInclude)
			.RunAll();

		simData.Collisions ??= new CollisionSnapshot();
		CollisionSnapshot snapshot = simData.Collisions;

		var mdfs = MeshDistanceSystem.FindInBox(collisionBounds);
		foreach ( var tr in trs )
		{
			var collisionType = tr.ClassifyColliderType();
			CaptureConvexCollision( snapshot, collisionType, tr.Shape );
		}
		foreach ( var mdf in mdfs )
		{
			var useMdf = SimulationData.UseMeshDistanceFields && !mdf.GameObject.Tags.Has( "mdf_disable" );
			if ( useMdf )
			{
				CaptureMdfCollision( snapshot.MeshColliders, mdf.GameObject.WorldTransform, mdf.Mdf );
			}
		}

		simData.Bounds = collisionBounds;
		return snapshot;
	}

	private static void CaptureConvexCollision(CollisionSnapshot snapshot, RopeColliderType colliderType, PhysicsShape shape)
	{
		switch (colliderType)
		{
			case RopeColliderType.None:
				return;
			case RopeColliderType.UniformSphere:
			case RopeColliderType.HullSphere:
				CaptureSphereCollision(snapshot.SphereColliders, shape.Collider as SphereCollider);
				break;
			case RopeColliderType.Capsule:
				CaptureCapsuleCollision(snapshot.CapsuleColliders, shape.Collider as CapsuleCollider);
				break;
			case RopeColliderType.Box:
				CaptureBoxCollision(snapshot.BoxColliders, shape.Collider as BoxCollider);
				break;
			case RopeColliderType.Plane:
				CapturePlaneCollision(snapshot.BoxColliders, shape.Collider as PlaneCollider);
				break;
			default:
				break;
		}
	}

	private static void CaptureSphereCollision( Dictionary<int, SphereCollisionInfo> sphereColliders, SphereCollider sphere)
	{
		var colliderId = sphere.GetHashCode();
		if ( !sphereColliders.ContainsKey(colliderId) )
		{
			SphereCollisionInfo ci = new()
			{
				Id = colliderId,
				Transform = sphere.WorldTransform,
				Center = sphere.Center,
				Radius = sphere.Radius,
			};
			sphereColliders[colliderId] = ci;
		}
	}

	private static void CaptureBoxCollision(Dictionary<int, BoxCollisionInfo> boxColliders, BoxCollider box)
	{
		var colliderId = box.GetHashCode();
		if ( !boxColliders.ContainsKey(colliderId) )
		{
			BoxCollisionInfo ci = new()
			{
				Id = colliderId,
				Transform = box.WorldTransform.WithPosition(box.WorldPosition + box.Center),
				Size = box.Scale
			};
			boxColliders[colliderId] = ci;
		}
	}

	/// <summary>
	/// All PlaneColliders are treated as BoxColliders with of this thickness.
	/// If the thickness is too low, the rope particles may clip through planes.
	/// </summary>
	[ConVar("rope_collision_plane_thickness")]
	public static float PlaneColliderThickness { get; set; } = 16f;

	private static void CapturePlaneCollision(Dictionary<int, BoxCollisionInfo> boxColliders, PlaneCollider plane)
	{
		var colliderId = plane.GetHashCode();
		if ( !boxColliders.ContainsKey(colliderId) )
		{
			var center = new Vector3(plane.Center.x, plane.Center.y, -PlaneColliderThickness * 0.5f);

			BoxCollisionInfo ci = new()
			{
				Id = colliderId,
				Transform = plane.WorldTransform.WithPosition(plane.WorldPosition + center),
				Size = new Vector3(plane.Scale.x, plane.Scale.y, PlaneColliderThickness)
			};
			boxColliders[colliderId] = ci;
		}
	}

	private static void CaptureCapsuleCollision(Dictionary<int, CapsuleCollisionInfo> capsuleColliders, CapsuleCollider capsule)
	{
		var colliderId = capsule.GetHashCode();
		if ( !capsuleColliders.ContainsKey(colliderId) )
		{
			CapsuleCollisionInfo ci = new()
			{
				Id = colliderId,
				Transform = capsule.WorldTransform,
				Start = capsule.Start,
				End = capsule.End,
				Radius = capsule.Radius,
			};
			capsuleColliders[colliderId] = ci;
		}
	}

	private static void CaptureMdfCollision(Dictionary<int, MeshCollisionInfo> meshColliders, Transform gameObjectTx, MeshDistanceField mdf)
	{
		if (!mdf.IsOctreeBuilt)
			return;

		foreach( var sdf in mdf.GetAllVoxels() )
		{
			if ( sdf.Data is null )
				continue;

			var voxel = sdf.Position;
			var size = sdf.Data.Bounds.Size.x;
			var voxelTx = new Transform()
			{
				Position = gameObjectTx.PointToWorld( mdf.VoxelToLocalCenter( voxel ) - size * 0.5f ),
				Rotation = gameObjectTx.Rotation,
				Scale = gameObjectTx.Scale,
			};

			// DebugOverlaySystem.Current.Box( size * 0.5f, size, transform: voxelTx );
			var id = HashCode.Combine( mdf.Id, voxel );
			if ( !meshColliders.ContainsKey( id ) )
			{
				MeshCollisionInfo ci = new()
				{
					Id = id,
					Sdf = sdf.Data,
					Transform = voxelTx,
				};
				meshColliders[id] = ci;
			}
		}
		
	}
}
