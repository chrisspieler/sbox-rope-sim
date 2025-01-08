﻿namespace Duccsoft;

public partial class VerletRope
{
	public VerletRope( Vector3 startPos, Vector3 endPos, int pointCount )
	{
		StartPosition = startPos;
		EndPosition = endPos;
		PointCount = pointCount;

		Reset();
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
	public float GravityScale => 1f;
	public Vector3 StartPosition { get; set; } = Vector3.Zero;
	public Vector3 EndPosition { get; set; } = Vector3.Down * 100f;
	public bool FixedStart { get; set; }
	public bool FixedEnd { get; set; }
	public int PointCount { get; set; } = 32;
	public int Iterations { get; set; } = 80;
	public float SegmentLength { get; private set; }

	private Point[] _points;
	public IEnumerable<Point> Points => _points;

	public void Reset()
	{
		_points = new Point[PointCount];

		var ray = new Ray( StartPosition, (EndPosition - StartPosition).Normal );
		var distance = StartPosition.Distance( EndPosition );
		SegmentLength = PointCount > 1
			? distance / PointCount - 1
			: 0f;

		for ( int i = 0; i < PointCount; i++ )
		{
			var pos = ray.Project( SegmentLength * i );
			_points[i] = new Point( pos, pos );
		}
	}

	public void Simulate()
	{
		UpdatePoints( Time.Delta );
		CapturePossibleCollisions();
		for ( int i = 0; i < Iterations; i++ )
		{
			UpdateSegments();
			ResolveCollisions();
		}
	}

	private void UpdatePoints( float deltaTime )
	{
		var gravity = Physics.IsValid() ? Physics.Gravity : Gravity;

		for ( int i = 0; i < PointCount; i++ )
		{
			var point = _points[i];
			if ( FixedStart && i == 0 )
			{
				point.Position = StartPosition;
				point.LastPosition = StartPosition;
				_points[i] = point;
				continue;
			}
			else if ( FixedEnd && i == PointCount - 1 )
			{
				point.Position = EndPosition;
				point.LastPosition = EndPosition;
				_points[i] = point;
				continue;
			}

			var temp = point.Position;
			var delta = point.Position - point.LastPosition;
			// Apply damping
			delta *= 1f - ( 0.95f * deltaTime);
			point.Position += delta;
			// Apply gravity
			point.Position += gravity * ( deltaTime * deltaTime ) * GravityScale;
			point.LastPosition = temp;
			_points[i] = point;
		}
	}

	private void UpdateSegments()
	{
		for ( int i = 0; i < PointCount - 1; i++ )
		{
			var pointA = _points[i];
			var pointB = _points[i + 1];

			var distance = Vector3.DistanceBetween( pointA.Position, pointB.Position );
			float difference = 0;
			if ( distance > 0 )
			{
				difference = ( SegmentLength - distance ) / distance;
			}

			var translation = ( pointA.Position - pointB.Position ) * ( 0.5f * difference );
			pointA.Position += translation;
			_points[i] = pointA;
			pointB.Position -= translation;
			_points[i + 1] = pointB;

			if ( FixedStart && i == 0 )
			{
				_points[i] = pointA with { Position = StartPosition };
			}
			if ( FixedEnd && i == PointCount - 2 )
			{
				_points[i + 1] = pointB with { Position = EndPosition };
			}
		}
	}
}
