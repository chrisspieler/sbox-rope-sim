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

	public PhysicsWorld Physics { get; init; }
	/// <summary>
	/// A factor applied to CollisionRadius when finding collisions. Higher values may
	/// reduce the chance of clipping at the cost of evaluating collisions more often.
	/// </summary>
	public float CollisionRadiusScale { get; set; } = 5f;
	/// <summary>
	/// A radius in units to search for PhysicsShapes around each point of the rope.
	/// </summary>
	public float CollisionRadius => SegmentLength * 0.5f * CollisionRadiusScale;
	private Dictionary<int, SphereCollisionInfo> _sphereCollisions = new();
	private Dictionary<int, BoxCollisionInfo> _boxCollisions = new();

	private void UpdateCollisions()
	{
		if ( !Physics.IsValid() )
			return;

		_sphereCollisions.Clear();
		_boxCollisions.Clear();

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
			}
		}
	}

	private void ResolveCollisions()
	{
		if ( !Physics.IsValid() )
			return;

		foreach( (_, var ci ) in _sphereCollisions )
		{
			var radius = ci.Radius;

			foreach( var pointId in ci.CollidingPoints )
			{
				var point = _points[pointId];
				var pointPos = ci.Transform.PointToLocal( point.Position );
				var distance = Vector3.DistanceBetween( ci.Center, pointPos );
				if ( distance - radius > 0 )
					continue;

				var direction = ( pointPos - ci.Center ).Normal;
				var hitPosition = ci.Center + direction * radius;
				hitPosition = ci.Transform.PointToWorld( hitPosition );
				_points[pointId] = point with { Position = hitPosition };
			}
		}
		foreach( (_, var ci ) in _boxCollisions )
		{
			foreach( var pointId in ci.CollidingPoints )
			{
				var point = _points[pointId];
				var pointPos = ci.Transform.PointToLocal( point.Position );

				var halfSize = ci.Size * 0.5f;
				var scale = ci.Transform.Scale;
				var pointAbs = halfSize - pointPos.Abs();
				if ( pointAbs.x <= 0 || pointAbs.y <= 0 || pointAbs.z <= 0 )
					continue;

				var pointScaled = pointAbs * scale;
				if ( pointScaled.x < pointScaled.y )
				{
					if ( pointScaled.x < pointScaled.z )
						pointPos.x = halfSize.x * MathF.Sign( pointPos.x );
					else
						pointPos.z = halfSize.z * MathF.Sign( pointPos.z );
				}
				else
				{
					pointPos.y = halfSize.y * MathF.Sign( pointPos.y );
				}

				var hitPos = ci.Transform.PointToWorld( pointPos );
				_points[pointId] = point with { Position = hitPos };
			}
		}
	}
}
