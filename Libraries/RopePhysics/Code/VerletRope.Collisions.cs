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

	private Dictionary<int, SphereCollisionInfo> _sphereCollisions = new();
	private Dictionary<int, BoxCollisionInfo> _boxCollisions = new();
	private Dictionary<int, GenericCollisionInfo> _genericCollisions = new();

	private bool _shouldUpdateCollisions;

	private void UpdateCollisions()
	{
		if ( !Physics.IsValid() || !_shouldUpdateCollisions )
			return;

		_shouldUpdateCollisions = false;

		_sphereCollisions.Clear();
		_boxCollisions.Clear();
		_genericCollisions.Clear();

		for ( int i = 0; i < _points.Length; i++ )
		{
			var point = _points[i];
			var trs = Physics.Trace
				.Sphere( CollisionRadius, point.Position, point.Position )
				.RunAll();
			foreach ( var tr in trs )
			{
				if ( tr.Shape.IsSphereShape && tr.Shape.Collider is SphereCollider sphere )
				{
					var id = sphere.GetHashCode();
					if ( !_sphereCollisions.TryGetValue( id, out var ci ) )
					{
						ci = new SphereCollisionInfo()
						{
							Id = id,
							Transform = sphere.WorldTransform,
							Center = sphere.Center,
							Radius = sphere.Radius,
						};
						_sphereCollisions[id] = ci;
					}
					ci.CollidingPoints.Add( i );
				}
				else if ( tr.Shape.IsCapsuleShape && tr.Shape.Collider is CapsuleCollider capsule )
				{
					var id = capsule.GetHashCode();
					// Log.Info( "capsule" );
				}
				else if ( tr.Shape.IsHullShape && tr.Shape.Collider is BoxCollider box )
				{
					var id = box.GetHashCode();
					if ( !_boxCollisions.TryGetValue( id, out var ci ) )
					{
						ci = new BoxCollisionInfo()
						{
							Id = id,
							Transform = box.WorldTransform,
							Center = box.Center,
							Size = box.Scale
						};
						_boxCollisions[id] = ci;
					}
					ci.CollidingPoints.Add( i );
				}
				else if ( tr.Shape.IsHullShape && tr.Shape.Collider is HullCollider hull )
				{
					// Log.Info( "Hull collider!" );
				}
				else if ( tr.Shape.IsMeshShape && tr.Shape.Collider is MeshComponent mesh )
				{
					var dir = (point.Position - point.LastPosition).Normal;
					
					var startPos = point.LastPosition + (-dir) * 5f;
					var distance = startPos.Distance( point.Position );
					var ray = new Ray( startPos, dir );
					var meshTrs = Physics.Trace
						.Ray( ray, distance )
						.RunAll();
					PhysicsTraceResult? meshTr = null;
					foreach( var maybeTr in meshTrs )
					{
						if ( maybeTr.Hit && maybeTr.Shape?.Collider == mesh )
						{
							meshTr = maybeTr;
							break;
						}
					}
					if ( !meshTr.HasValue )
						continue;

					// If the start and end are coplanar with the surface, don't squiggle around.
					if ( MathF.Abs( meshTr.Value.Direction.Dot( meshTr.Value.Normal ) ) < 0.1f )
						continue;

					var id = mesh.GetHashCode();
					if ( !_genericCollisions.TryGetValue( id, out var ci ) )
					{
						ci = new GenericCollisionInfo()
						{
							Id = id,
							Transform = mesh.WorldTransform,
						};
						_genericCollisions[id] = ci;
					}
					ci.CollidingPoints.Add( new GenericCollisionInfo.Point()
					{
						Id			= i,
						Normal		= mesh.WorldTransform.NormalToLocal( meshTr.Value.Normal ),
						HitPosition = mesh.WorldTransform.PointToLocal( meshTr.Value.HitPosition ),
					});
				}
			}
		}
	}

	private void ResolveCollisions()
	{
		if ( !Physics.IsValid() )
			return;

		ResolveSphereCollisions();
		ResolveBoxCollisions();
		ResolveGenericCollisions();

		_shouldUpdateCollisions = true;
	}

	private void ResolveSphereCollisions()
	{
		foreach ( (_, var ci) in _sphereCollisions )
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
		foreach ( (_, var ci) in _boxCollisions )
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

	private void ResolveGenericCollisions()
	{
		foreach( (_, var ci ) in _genericCollisions )
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
