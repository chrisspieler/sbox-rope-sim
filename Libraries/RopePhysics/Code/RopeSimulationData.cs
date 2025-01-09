namespace Duccsoft;

public partial class RopeSimulationData
{
	public RopeSimulationData( PhysicsWorld physics, Vector3 startPos, Vector3 endPos, int pointCount )
	{
		Physics = physics;
		PointCount = pointCount;

		Reset( startPos, endPos );
	}

	public struct Point
	{
		public Point( Vector3 position, Vector3 lastPosition )
		{
			Position = position;
			LastPosition = lastPosition;
		}

		public Vector3 Position { get; set; }
		public Vector3 LastPosition { get; set; }
	}

	public Vector3 Gravity { get; set; } = Vector3.Down * 800f;
	public Vector3? FixedFirstPosition { get; set; }
	public Vector3? FixedLastPosition { get; set; }
	public Vector3 FirstPosition
	{
		get
		{
			if ( Points.Length > 0 )
				return Points[0].Position;

			return Vector3.Zero;
		}
	}
	public Vector3 LastPosition
	{
		get
		{
			if ( Points.Length > 0 )
				return Points[^1].Position;

			return Vector3.Down * 32f;
		}
	}
	public int PointCount { get; set; } = 32;
	public int Iterations { get; set; } = 80;
	public float SegmentLength { get; private set; }
	public float Radius { get; set; } = 1f;

	public Point[] Points { get; private set; }
	[ConVar( "rope_collision_mdf_enable" )]
	public static bool UseMeshDistanceFields { get; set; } = true;

	public PhysicsWorld Physics { get; init; }
	public TagSet CollisionInclude { get; set; } = [];
	public TagSet CollisionExclude { get; set; } = ["noblockrope"];
	/// <summary>
	/// A bias applied to CollisionRadius that is used when finding colliders that are near to
	/// a rope node. Higher values will reduce the chance of clipping.
	/// </summary>
	public float CollisionSearchRadius { get; set; } = 10f;
	/// <summary>
	/// The total area to search for collisions, encompassing the entire rpoe.
	/// </summary>
	public BBox CollisionBounds { get; set; }
	public RopeCollisionSnapshot Collisions { get; set; } = new();

	public void Reset( Vector3 startPos, Vector3 endPos )
	{
		Points = new Point[PointCount];

		var ray = new Ray( startPos, (endPos - startPos).Normal );
		var distance = startPos.Distance( endPos );
		SegmentLength = PointCount > 1
			? distance / PointCount - 1
			: 0f;

		for ( int i = 0; i < PointCount; i++ )
		{
			var pos = ray.Project( SegmentLength * i );
			Points[i] = new Point( pos, pos );
		}
	}

	public void RecalculatePointBounds()
	{
		BBox collisionBounds = default;

		for ( int i = 0; i < Points.Length; i++ )
		{
			var point = Points[i];
			var pointBounds = BBox.FromPositionAndSize( point.Position, CollisionSearchRadius );
			if ( i == 0 )
			{
				collisionBounds = pointBounds;
			}
			else
			{
				collisionBounds = collisionBounds.AddBBox( pointBounds );
			}
		}

		CollisionBounds = collisionBounds;
	}
}
