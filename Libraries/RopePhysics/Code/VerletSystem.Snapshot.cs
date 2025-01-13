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

		var collisionBounds = simData.Bounds;

		// Find possible collisions in the scene.
		var trs = simData.Physics.Trace
			.Box(collisionBounds.Size, collisionBounds.Center, collisionBounds.Center)
			.WithoutTags(simData.CollisionExclude)
			.WithAnyTags(simData.CollisionInclude)
			.RunAll();

		CollisionSnapshot snapshot = new();
		var points = simData.CpuPoints;

		var mdfs = MeshDistanceSystem.FindInBox(collisionBounds);
		for (int i = 0; i < points.Length; i++)
		{
			foreach (var tr in trs)
			{
				var collisionType = tr.ClassifyColliderType();
				CaptureConvexCollision(snapshot, i, collisionType, tr.Shape);
			}
			foreach (var mdf in mdfs)
			{
				var useMdf = SimulationData.UseMeshDistanceFields && !mdf.GameObject.Tags.Has("mdf_disable");
				if (useMdf)
				{
					var pointPos = points[i].Position;
					CaptureMdfCollision(snapshot.MeshColliders, i, pointPos, mdf.GameObject.WorldTransform, mdf.Mdf);
				}
			}
		}

		simData.Bounds = collisionBounds;
		return snapshot;
	}

	private static void CaptureConvexCollision(CollisionSnapshot snapshot, int pointIndex, RopeColliderType colliderType, PhysicsShape shape)
	{
		switch (colliderType)
		{
			case RopeColliderType.None:
				return;
			case RopeColliderType.UniformSphere:
			case RopeColliderType.HullSphere:
				CaptureSphereCollision(snapshot.SphereColliders, pointIndex, shape.Collider as SphereCollider);
				break;
			case RopeColliderType.Capsule:
				CaptureCapsuleCollision(snapshot.CapsuleColliders, pointIndex, shape.Collider as CapsuleCollider);
				break;
			case RopeColliderType.Box:
				CaptureBoxCollision(snapshot.BoxColliders, pointIndex, shape.Collider as BoxCollider);
				break;
			case RopeColliderType.Plane:
				CapturePlaneCollision(snapshot.BoxColliders, pointIndex, shape.Collider as PlaneCollider);
				break;
			default:
				break;
		}
	}

	private static void CaptureSphereCollision( Dictionary<int, SphereCollisionInfo> sphereColliders, int pointIndex, SphereCollider sphere)
	{
		var colliderId = sphere.GetHashCode();
		if ( !sphereColliders.TryGetValue(colliderId, out var ci) )
		{
			ci = new SphereCollisionInfo()
			{
				Id = colliderId,
				Transform = sphere.WorldTransform,
				Center = sphere.Center,
				Radius = sphere.Radius,
			};
			sphereColliders[colliderId] = ci;
		}
		ci.CollidingPoints.Add(pointIndex);
	}

	private static void CaptureBoxCollision(Dictionary<int, BoxCollisionInfo> boxColliders, int pointIndex, BoxCollider box)
	{
		var colliderId = box.GetHashCode();
		if (!boxColliders.TryGetValue(colliderId, out var ci))
		{
			ci = new BoxCollisionInfo()
			{
				Id = colliderId,
				Transform = box.WorldTransform.WithPosition(box.WorldPosition + box.Center),
				Size = box.Scale
			};
			boxColliders[colliderId] = ci;
		}
		ci.CollidingPoints.Add(pointIndex);
	}

	/// <summary>
	/// All PlaneColliders are treated as BoxColliders with of this thickness.
	/// If the thickness is too low, the rope particles may clip through planes.
	/// </summary>
	[ConVar("rope_collision_plane_thickness")]
	public static float PlaneColliderThickness { get; set; } = 16f;

	private static void CapturePlaneCollision(Dictionary<int, BoxCollisionInfo> boxColliders, int pointIndex, PlaneCollider plane)
	{
		var colliderId = plane.GetHashCode();
		if (!boxColliders.TryGetValue(colliderId, out var ci))
		{
			var center = new Vector3(plane.Center.x, plane.Center.y, -PlaneColliderThickness * 0.5f);

			ci = new BoxCollisionInfo()
			{
				Id = colliderId,
				Transform = plane.WorldTransform.WithPosition(plane.WorldPosition + center),
				Size = new Vector3(plane.Scale.x, plane.Scale.y, PlaneColliderThickness)
			};
			boxColliders[colliderId] = ci;
		}
		ci.CollidingPoints.Add(pointIndex);
	}

	private static void CaptureCapsuleCollision(Dictionary<int, CapsuleCollisionInfo> capsuleColliders, int pointindex, CapsuleCollider capsule)
	{
		var colliderId = capsule.GetHashCode();
		if (!capsuleColliders.TryGetValue(colliderId, out var ci))
		{
			ci = new CapsuleCollisionInfo()
			{
				Id = colliderId,
				Transform = capsule.WorldTransform,
				Start = capsule.Start,
				End = capsule.End,
				Radius = capsule.Radius,
			};
			capsuleColliders[colliderId] = ci;
		}
		ci.CollidingPoints.Add(pointindex);
	}

	private static void CaptureMdfCollision(Dictionary<int, MeshCollisionInfo> meshColliders, int pointIndex, Vector3 pointPos, Transform tx, MeshDistanceField mdf)
	{
		if (!mdf.IsOctreeBuilt)
			return;

		var localPos = tx.PointToLocal(pointPos);
		var voxel = mdf.PositionToVoxel(localPos);
		var sdfTex = mdf.GetSdfTexture(voxel);
		if (sdfTex is null)
			return;

		var closestPoint = sdfTex.Bounds.ClosestPoint(localPos);
		var distance = closestPoint.Distance(localPos);
		if (distance > mdf.OctreeLeafDims * 0.5f)
			return;

		var id = HashCode.Combine(mdf.Id, voxel);
		if (!meshColliders.TryGetValue(id, out var ci))
		{
			ci = new MeshCollisionInfo()
			{
				Id = id,
				Sdf = sdfTex,
				Transform = tx,
			};
			meshColliders[id] = ci;
		}
		ci.CollidingPoints.Add(pointIndex);
	}
}
