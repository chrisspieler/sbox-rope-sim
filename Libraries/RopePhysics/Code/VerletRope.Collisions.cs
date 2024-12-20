namespace Duccsoft;

public partial class VerletRope
{
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
		public Vector3 Center;
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

	private struct GenericCollisionInfo
	{
		public struct Point
		{
			public int Id;
			public Vector3 HitPosition, Normal;
		}
		
		public int Id;
		public Transform Transform;
		public List<Point> CollidingPoints;

        public GenericCollisionInfo()
        {
			CollidingPoints = new();
        }
    }

	public PhysicsWorld Physics { get; init; }
	/// <summary>
	/// A factor applied to CollisionRadius when finding collisions. Higher values may
	/// reduce the chance of clipping at the cost of evaluating collisions more often.
	/// </summary>
	public float CollisionRadiusScale { get; set; } = 5f;
	/// <summary>
	/// A radius in units to search for PhysicsShapes around each point of the rope.
	/// </summary>
	public float CollisionRadius => SolidRadius * 2f + SegmentLength * 0.5f * CollisionRadiusScale;
	public float SolidRadius => 0.5f;

	private Dictionary<int, SphereCollisionInfo> _sphereColliders = new();
	private Dictionary<int, BoxCollisionInfo> _boxColliders = new();
	private Dictionary<int, CapsuleCollisionInfo> _capsuleColliders = new();
	private Dictionary<int, GenericCollisionInfo> _genericColliders = new();

	private bool _shouldUpdateCollisions;

	private void CapturePossibleCollisions()
	{
		if ( !Physics.IsValid() || !_shouldUpdateCollisions )
			return;

		_shouldUpdateCollisions = false;

		_sphereColliders.Clear();
		_boxColliders.Clear();
		_capsuleColliders.Clear();
		_genericColliders.Clear();

		for ( int i = 0; i < _points.Length; i++ )
		{
			var point = _points[i];
			// TODO: Filter trace using tags.
			var trs = Physics.Trace
				.Sphere( CollisionRadius, point.Position, point.Position )
				.RunAll();
			foreach ( var tr in trs )
			{
				// Spheres
				if ( (tr.Shape.IsHullShape || tr.Shape.IsSphereShape) && tr.Shape.Collider is SphereCollider sphere )
				{
					CaptureSphereCollision( i, sphere );
				}
				// Capsules
				else if ( tr.Shape.IsCapsuleShape && tr.Shape.Collider is CapsuleCollider capsule )
				{
					CaptureCapsuleCollision( i, capsule );
				}
				// Boxes
				else if ( tr.Shape.IsHullShape && tr.Shape.Collider is BoxCollider box )
				{
					CaptureBoxCollision( i, box );
				}
				// Meshes, Hulls, and Terrain
				else if ( tr.Shape.IsMeshShape || tr.Shape.IsHullShape || tr.Shape.IsHeightfieldShape )
				{
					CaptureGenericCollision( i, tr.Shape.Collider as Collider );
				}
			}
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
				Transform = box.WorldTransform,
				Center = box.Center,
				Size = box.Scale
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

	private void CaptureGenericCollision( int pointIndex, Collider collider )
	{
		var point = _points[pointIndex];
		var dir = (point.Position - point.LastPosition).Normal;

		var startPos = point.LastPosition + (-dir) * 5f;
		var distance = startPos.Distance( point.Position );
		var ray = new Ray( startPos, dir );
		var meshTrs = Physics.Trace
			.Ray( ray, distance )
			.RunAll();
		PhysicsTraceResult? meshTr = null;
		foreach ( var maybeTr in meshTrs )
		{
			if ( maybeTr.Hit && maybeTr.Shape?.Collider == collider )
			{
				meshTr = maybeTr;
				break;
			}
		}
		if ( !meshTr.HasValue )
			return;

		// If the start and end are coplanar with the surface, don't squiggle around.
		if ( MathF.Abs( meshTr.Value.Direction.Dot( meshTr.Value.Normal ) ) < 0.1f )
			return;

		var id = collider.GetHashCode();
		if ( !_genericColliders.TryGetValue( id, out var ci ) )
		{
			ci = new GenericCollisionInfo()
			{
				Id = id,
				Transform = collider.WorldTransform,
			};
			_genericColliders[id] = ci;
		}
		ci.CollidingPoints.Add( new GenericCollisionInfo.Point()
		{
			Id = pointIndex,
			Normal = collider.WorldTransform.NormalToLocal( meshTr.Value.Normal ),
			HitPosition = collider.WorldTransform.PointToLocal( meshTr.Value.HitPosition ),
		} );
	}

	private void ResolveCollisions()
	{
		if ( !Physics.IsValid() )
			return;

		ResolveSphereCollisions();
		ResolveBoxCollisions();
		ResolveCapsuleCollisions();
		ResolveGenericCollisions();

		_shouldUpdateCollisions = true;
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
				pointPos += ci.Center;
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

	private void ResolveGenericCollisions()
	{
		foreach( (_, var ci ) in _genericColliders )
		{
			foreach ( var collision in ci.CollidingPoints )
			{
				var point = _points[collision.Id];
				var currentPos = collision.HitPosition + collision.Normal * SolidRadius;
				currentPos = ci.Transform.PointToWorld( currentPos );
				_points[collision.Id] = point with { Position = currentPos };
			}
		}
	}
}
