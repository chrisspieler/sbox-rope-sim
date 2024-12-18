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
		public Vector3 Mins;
		public Vector3 Maxs;
		public Transform Transform;
		public List<int> CollidingPoints;
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
					if ( !_sphereCollisions.TryGetValue( id, out var collisionInfo ) )
					{
						collisionInfo = new SphereCollisionInfo()
						{
							Id = id,
							Transform = sphere.WorldTransform,
							Center = sphere.Center,
							Radius = sphere.Radius,
						};
						_sphereCollisions[id] = collisionInfo;
					}
					collisionInfo.CollidingPoints.Add( i );
				}
				else if ( tr.Shape.IsCapsuleShape && tr.Shape.Collider is CapsuleCollider capsule )
				{
					var id = capsule.GetHashCode();
					// Log.Info( "capsule" );
				}
				else if ( tr.Shape.IsHullShape && tr.Shape.Collider is BoxCollider box )
				{
					var id = box.GetHashCode();
					// Log.Info( "box" );
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
				var pointPos = collisionInfo.Transform.PointToLocal( point.Position );
				var distance = Vector3.DistanceBetween( collisionInfo.Center, pointPos );
				if ( distance - radius > 0 )
					continue;

				var direction = ( pointPos - collisionInfo.Center ).Normal;
				var hitPosition = collisionInfo.Center + direction * radius;
				hitPosition = collisionInfo.Transform.PointToWorld( hitPosition );
				_points[pointId] = point with { Position = hitPosition };
			}
		}
	}
}
