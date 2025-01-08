namespace Duccsoft;

public partial class VerletRope
{
	#region DTO
	private struct SphereCollisionInfo
	{
		public int Id;
		public float Radius;
		public Vector3 Center;
		public Transform Transform;
		public List<int> CollidingPoints;

		public SphereCollisionInfo()
		{
			CollidingPoints = new();
		}
	}

	private struct BoxCollisionInfo
	{
		public int Id;
		public Vector3 Size;
		public Transform Transform;
		public List<int> CollidingPoints;

        public BoxCollisionInfo()
        {
			CollidingPoints = new();
        }
    }

	private struct CapsuleCollisionInfo
	{
		public int Id;
		public Vector3 Start, End;
		public float Radius;
		public Transform Transform;
		public List<int> CollidingPoints;

		public CapsuleCollisionInfo()
		{
			CollidingPoints = new();
		}
	}

	private struct MeshCollisionInfo
	{
		public int Id;
		public SignedDistanceField Sdf;
		public Transform Transform;
		public List<int> CollidingPoints;

		public MeshCollisionInfo()
		{
			CollidingPoints = new();
		}
	}
	#endregion

	[ConVar( "rope_collision_mdf_enable" )]
	public static bool UseMeshDistanceFields { get; set; } = true;

	public PhysicsWorld Physics { get; init; }
	public TagSet CollisionInclude { get; set; } = [];
	public TagSet CollisionExclude { get; set; } = [ "noblockrope" ];
	/// <summary>
	/// A bias applied to CollisionRadius that is used when finding colliders that are near to
	/// a rope node. Higher values will reduce the chance of clipping.
	/// </summary>
	public float CollisionSearchRadius { get; set; } = 10f;
	/// <summary>
	/// The total area to search for collisions, encompassing the entire rpoe.
	/// </summary>
	public BBox CollisionBounds { get; set; }
	/// <summary>
	/// A radius in units that defines the contribution of each node to CollisionBounds.
	/// </summary>
	public float CollisionRadius => SolidRadius * 2f + SegmentLength * 0.5f + CollisionSearchRadius;
	public float SolidRadius => 1f;

	public int SphereColliderCount => _sphereColliders.Count;
	private readonly Dictionary<int, SphereCollisionInfo> _sphereColliders = [];
	public int BoxColliderCount => _boxColliders.Count;
	private readonly Dictionary<int, BoxCollisionInfo> _boxColliders = [];
	public int CapsuleColliderCount => _capsuleColliders.Count;
	private readonly Dictionary<int, CapsuleCollisionInfo> _capsuleColliders = [];
	public int MeshColliderCount => _meshColliders.Count;
	private readonly Dictionary<int, MeshCollisionInfo> _meshColliders = [];

	#region Broad Phase
	private void CapturePossibleCollisions()
	{
		if ( !Physics.IsValid() )
			return;

		_sphereColliders.Clear();
		_boxColliders.Clear();
		_capsuleColliders.Clear();
		_meshColliders.Clear();

		if ( _points.Length < 1 )
			return;

		CollisionBounds = default;

		for ( int i = 0; i < _points.Length; i++ )
		{
			var point = _points[i];
			var pointBounds = BBox.FromPositionAndSize( point.Position, CollisionRadius * 2 );
			if ( i == 0 )
			{
				CollisionBounds = pointBounds;
			}
			else
			{
				CollisionBounds = CollisionBounds.AddBBox( pointBounds );
			}
		}

		var trs = Physics.Trace
			.Box( CollisionBounds.Size, CollisionBounds.Center, CollisionBounds.Center )
			.WithoutTags( CollisionExclude )
			.WithAnyTags( CollisionInclude )
			.RunAll();

		var mdfs = MeshDistanceSystem.FindInBox( CollisionBounds );
		for ( int i = 0; i < _points.Length; i++ )
		{
			foreach ( var tr in trs )
			{
				var collisionType = tr.ClassifyColliderType();
				CaptureConvexCollision( i, collisionType, tr.Shape );
			}
			foreach( var mdf in mdfs )
			{
				var useMdf = UseMeshDistanceFields && !mdf.GameObject.Tags.Has( "mdf_disable" );
				if ( useMdf )
				{
					CaptureMdfCollision( i, mdf.GameObject.WorldTransform, mdf.Mdf );
				}
			}
		}
	}

	private void CaptureConvexCollision( int pointIndex, RopeColliderType colliderType, PhysicsShape shape )
	{
		switch( colliderType )
		{
			case RopeColliderType.None:
				return;
			case RopeColliderType.UniformSphere:
			case RopeColliderType.HullSphere:
				CaptureSphereCollision( pointIndex, shape.Collider as SphereCollider );
				break;
			case RopeColliderType.Capsule:
				CaptureCapsuleCollision( pointIndex, shape.Collider as CapsuleCollider );
				break;
			case RopeColliderType.Box:
				CaptureBoxCollision( pointIndex, shape.Collider as BoxCollider );
				break;
			case RopeColliderType.Plane:
				CapturePlaneCollision( pointIndex, shape.Collider as PlaneCollider );
				break;
			default:
				break;
		}
	}

	private void CaptureSphereCollision( int pointIndex, SphereCollider sphere )
	{
		var colliderId = sphere.GetHashCode();
		if ( !_sphereColliders.TryGetValue( colliderId, out var ci ) )
		{
			ci = new SphereCollisionInfo()
			{
				Id = colliderId,
				Transform = sphere.WorldTransform,
				Center = sphere.Center,
				Radius = sphere.Radius,
			};
			_sphereColliders[colliderId] = ci;
		}
		ci.CollidingPoints.Add( pointIndex );
	}

	private void CaptureBoxCollision( int pointIndex, BoxCollider box )
	{
		var colliderId = box.GetHashCode();
		if ( !_boxColliders.TryGetValue( colliderId, out var ci ) )
		{
			ci = new BoxCollisionInfo()
			{
				Id = colliderId,
				Transform = box.WorldTransform.WithPosition( box.WorldPosition + box.Center ),
				Size = box.Scale
			};
			_boxColliders[colliderId] = ci;
		}
		ci.CollidingPoints.Add( pointIndex );
	}

	/// <summary>
	/// All PlaneColliders are treated as BoxColliders with of this thickness.
	/// If the thickness is too low, the rope particles may clip through planes.
	/// </summary>
	[ConVar( "rope_collision_plane_thickness" )]
	public static float PlaneColliderThickness { get; set; } = 16f;

	private void CapturePlaneCollision( int pointIndex, PlaneCollider plane )
	{
		var colliderId = plane.GetHashCode();
		if ( !_boxColliders.TryGetValue( colliderId, out var ci ) )
		{
			var center = new Vector3( plane.Center.x, plane.Center.y, -PlaneColliderThickness * 0.5f );

			ci = new BoxCollisionInfo()
			{
				Id = colliderId,
				Transform = plane.WorldTransform.WithPosition( plane.WorldPosition + center ),
				Size = new Vector3( plane.Scale.x, plane.Scale.y, PlaneColliderThickness )
			};
			_boxColliders[colliderId] = ci;
		}
		ci.CollidingPoints.Add( pointIndex );
	}

	private void CaptureCapsuleCollision( int pointindex, CapsuleCollider capsule )
	{
		var colliderId = capsule.GetHashCode();
		if ( !_capsuleColliders.TryGetValue( colliderId, out var ci ) )
		{
			ci = new CapsuleCollisionInfo()
			{
				Id = colliderId,
				Transform = capsule.WorldTransform,
				Start = capsule.Start,
				End = capsule.End,
				Radius = capsule.Radius,
			};
			_capsuleColliders[colliderId] = ci;
		}
		ci.CollidingPoints.Add( pointindex );
	}

	private void CaptureMdfCollision( int pointIndex, Transform tx, MeshDistanceField mdf )
	{
		if ( !mdf.IsOctreeBuilt )
			return;

		var pointPos = _points[pointIndex].Position;
		var localPos = tx.PointToLocal( pointPos );
		var voxel = mdf.PositionToVoxel( localPos );
		var sdfTex = mdf.GetSdfTexture( voxel );
		if ( sdfTex is null )
			return;

		var closestPoint = sdfTex.Bounds.ClosestPoint( localPos );
		var distance = closestPoint.Distance( localPos );
		if ( distance > mdf.OctreeLeafDims * 0.5f )
			return;

		var id = HashCode.Combine( mdf.Id, voxel );
		if ( !_meshColliders.TryGetValue( id, out var ci ) )
		{
			ci = new MeshCollisionInfo()
			{
				Id = id,
				Sdf = sdfTex,
				Transform = tx,
			};
			_meshColliders[id] = ci;
		}
		ci.CollidingPoints.Add( pointIndex );
	}
	#endregion

	#region Collision Resolution
	private void ResolveCollisions()
	{
		if ( !Physics.IsValid() )
			return;

		ResolveSphereCollisions();
		ResolveBoxCollisions();
		ResolveCapsuleCollisions();
		ResolveMeshCollisions();
	}

	private void ResolveSphereCollisions()
	{
		foreach ( (_, var ci) in _sphereColliders )
		{
			var radius = ci.Radius;

			foreach ( var pointId in ci.CollidingPoints )
			{
				var point = _points[pointId];
				var pointPos = ci.Transform.PointToLocal( point.Position );
				var distance = Vector3.DistanceBetween( ci.Center, pointPos );
				if ( distance - radius > SolidRadius )
					continue;

				var direction = (pointPos - ci.Center).Normal;
				var hitPosition = ci.Center + direction * (radius + SolidRadius);
				hitPosition = ci.Transform.PointToWorld( hitPosition );
				_points[pointId] = point with { Position = hitPosition };
			}
		}
	}

	private void ResolveBoxCollisions()
	{
		foreach ( (_, var ci) in _boxColliders )
		{
			foreach ( var pointId in ci.CollidingPoints )
			{
				var point = _points[pointId];
				var pointPos = ci.Transform.PointToLocal( point.Position );
				var halfSize = ci.Size * 0.5f;
				var scale = ci.Transform.Scale;
				// Figure out how deep the point is inside in the box.
				var pointDepth = halfSize - pointPos.Abs();
				// If the point is entirely outside one of the axes, continue.
				if ( pointDepth.x <= -SolidRadius || pointDepth.y <= -SolidRadius || pointDepth.z <= -SolidRadius )
					continue;

				var pointScaled = pointDepth * scale;
				var signs = new Vector3( MathF.Sign( pointPos.x ), MathF.Sign( pointPos.y ), MathF.Sign( pointPos.z ) );
				if ( pointScaled.x < pointScaled.y && pointScaled.x < pointScaled.z )
				{
					pointPos.x = halfSize.x * signs.x + SolidRadius * signs.x;
				}
				else if ( pointScaled.y < pointScaled.x && pointScaled.y < pointScaled.z )
				{
					pointPos.y = halfSize.y * signs.y + SolidRadius * signs.y;
				}
				else
				{
					pointPos.z = halfSize.z * signs.z + SolidRadius * signs.z;
				}

				var hitPos = ci.Transform.PointToWorld( pointPos );
				_points[pointId] = point with { Position = hitPos };
			}
		}
	}

	private void ResolveCapsuleCollisions()
	{
		foreach ( (_, var ci) in _capsuleColliders )
		{
			// Adapted from: https://iquilezles.org/articles/distfunctions/
			float CapsuleSdf( Vector3 p )
			{
				var pa = p - ci.Start;
				var ba = ci.End - ci.Start;
				var h = MathX.Clamp( pa.Dot( ba ) / ba.Dot( ba ), 0f, 1f );
				return (pa - ba * h).Length - ci.Radius;
			}

			foreach ( var collision in ci.CollidingPoints )
			{
				var point = _points[collision];
				var localPos = ci.Transform.PointToLocal( point.Position );
				var sdf = CapsuleSdf( localPos );
				if ( sdf > SolidRadius )
					continue;

				var xOffset = new Vector3( 0.0001f, 0f, 0f );
				var yOffset = new Vector3( 0f, 0.0001f, 0f );
				var zOffset = new Vector3( 0f, 0f, 0.0001f );
				var gradient = new Vector3()
				{
					x = CapsuleSdf( localPos + xOffset ) - CapsuleSdf( localPos - xOffset ),
					y = CapsuleSdf( localPos + yOffset ) - CapsuleSdf( localPos - yOffset ),
					z = CapsuleSdf( localPos + zOffset ) - CapsuleSdf( localPos - zOffset ),
				};
				gradient = gradient.Normal;
				var currentPos = localPos + gradient * ( -sdf + SolidRadius );
				currentPos = ci.Transform.PointToWorld( currentPos );
				_points[collision] = point with { Position = currentPos };
			}
		}
	}

	private void ResolveMeshCollisions()
	{
		foreach ( (_, var ci) in _meshColliders )
		{
			foreach ( var collision in ci.CollidingPoints )
			{
				var point = _points[collision];
				var currentPos = ci.Transform.PointToLocal( point.Position );
				var currentClosestPoint = ci.Sdf.Bounds.ClosestPoint( currentPos );
				var currentDistance = currentPos.Distance( currentClosestPoint );
				if ( currentDistance >= SolidRadius )
					continue;

				var texel = ci.Sdf.PositionToTexel( currentPos );
				var sdMesh = ci.Sdf[texel];
				if ( sdMesh >= SolidRadius )
					continue;

				var gradient = ci.Sdf.CalculateGradient( texel );
				currentPos += gradient * ( -sdMesh + SolidRadius );
				currentPos = ci.Transform.PointToWorld( currentPos );
				_points[collision] = point with { Position = currentPos };

			}
		}
	}
	#endregion
}
