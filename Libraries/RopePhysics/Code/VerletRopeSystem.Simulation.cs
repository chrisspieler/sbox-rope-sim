namespace Duccsoft;

public partial class VerletRopeSystem
{
	private static void ApplyForces( RopeSimulationData simData, float deltaTime )
	{
		var gravity = simData.Gravity;

		var points = simData.Points;

		for ( int i = 0; i < points.Length; i++ )
		{
			var point = points[i];
			if ( simData.FixedFirstPosition is Vector3 firstPos && i == 0 )
			{
				point.Position = firstPos;
				point.LastPosition = firstPos;
				points[i] = point;
				continue;
			}
			else if ( simData.FixedLastPosition is Vector3 lastPos && i == points.Length - 1 )
			{
				point.Position = lastPos;
				point.LastPosition = lastPos;
				points[i] = point;
				continue;
			}

			var temp = point.Position;
			var delta = point.Position - point.LastPosition;
			// Apply damping
			delta *= 1f - (0.95f * deltaTime);
			point.Position += delta;
			// Apply gravity
			point.Position += gravity * (deltaTime * deltaTime);
			point.LastPosition = temp;
			points[i] = point;
		}
	}

	private static void ApplyConstraints( RopeSimulationData simData )
	{
		var points = simData.Points;

		for ( int i = 0; i < points.Length - 1; i++ )
		{
			var pointA = points[i];
			var pointB = points[i + 1];

			var distance = Vector3.DistanceBetween( pointA.Position, pointB.Position );
			float difference = 0;
			if ( distance > 0 )
			{
				difference = (simData.SegmentLength - distance) / distance;
			}

			var translation = (pointA.Position - pointB.Position) * (0.5f * difference);
			pointA.Position += translation;
			points[i] = pointA;
			pointB.Position -= translation;
			points[i + 1] = pointB;

			if ( simData.FixedFirstPosition is Vector3 firstPos && i == 0 )
			{
				points[i] = pointA with { Position = firstPos };
			}
			if ( simData.FixedLastPosition is Vector3 lastPos && i == points.Length - 2 )
			{
				points[i + 1] = pointB with { Position = lastPos };
			}
		}
	}
}
