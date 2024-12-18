namespace Duccsoft;

public partial class VerletRope
{
	private struct SphereCollisionInfo
	{
		public int Id;
		public float Radius;
		public Vector3 Center;
		public List<int> CollidingPoints;

		public SphereCollisionInfo()
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

	private void UpdateCollisions()
	{
		if ( !Physics.IsValid() )
			return;

		_sphereCollisions.Clear();

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
					if ( !_sphereCollisions.TryGetValue( id, out var collisionInfo ) )
					{
						collisionInfo = new SphereCollisionInfo()
						{
							Id = id,
							Center = sphere.WorldPosition,
							Radius = sphere.Radius,
						};
						_sphereCollisions[id] = collisionInfo;
					}
					collisionInfo.CollidingPoints.Add( i );
				}
			}
		}
	}

	private void ResolveCollisions()
	{
		if ( !Physics.IsValid() )
			return;

		foreach( (_, var collisionInfo ) in _sphereCollisions )
		{
			var radius = collisionInfo.Radius;

			foreach( var pointId in collisionInfo.CollidingPoints )
			{
				var point = _points[pointId];
				var distance = Vector3.DistanceBetween( collisionInfo.Center, point.Position );
				if ( distance - radius > 0 )
					continue;

				var direction = ( point.Position - collisionInfo.Center ).Normal;
				var hitPosition = collisionInfo.Center + direction * radius;
				_points[pointId] = point with { Position = hitPosition };
			}
		}
	}
}
