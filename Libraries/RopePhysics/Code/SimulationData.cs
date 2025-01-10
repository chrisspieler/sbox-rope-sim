﻿namespace Duccsoft;

public class SimulationData
{
	public SimulationData( PhysicsWorld physics, VerletPoint[] points, VerletStickConstraint[] sticks, int numColumns, float segmentLength )
	{
		Physics = physics;
		Points = points;
		Sticks = sticks;
		PointGridDims = new Vector2Int( points.Length / numColumns, numColumns );
		SegmentLength = segmentLength;
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

	public VerletPoint[] Points { get; }
	public VerletStickConstraint[] Sticks { get; }
	public float SegmentLength { get; }
	public Vector2Int PointGridDims { get; }

	public int Iterations { get; set; } = 80;
	public float Radius { get; set; } = 1f;

	[ConVar( "verlet_collision_mdf_enable" )]
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
	public CollisionSnapshot Collisions { get; set; } = new();

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
