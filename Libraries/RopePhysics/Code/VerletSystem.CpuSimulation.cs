namespace Duccsoft;

public partial class VerletSystem
{
	private void CpuSimulate( VerletComponent verlet )
	{
		SimulationData simData = verlet.SimData;
		simData.Transform = verlet.WorldTransform;
		// If we're simulating on the CPU, we aren't going to need to transfer any points to the GPU.
		simData.ClearPendingPointUpdates();

		CpuApplyTransform( verlet );

		float totalTime = Time.Delta;
		totalTime = MathF.Min( totalTime, verlet.FixedTimeStep );
		while ( totalTime >= 0 )
		{
			var deltaTime = MathF.Min( verlet.FixedTimeStep, totalTime );
			CpuApplyForces( simData, deltaTime );
			for ( int i = 0; i < simData.Iterations; i++ )
			{
				CpuApplyConstraints( simData );
				if ( verlet.EnableCollision )
				{
					ResolveCollisions( simData );
				}
			}
			totalTime -= verlet.FixedTimeStep;
		}

		simData.LastTransform = simData.Transform;

		verlet.UpdateCpuVertexBuffer( simData.CpuPoints );
		simData.RecalculateCpuPointBounds();

	}

	private static void CpuApplyTransform( VerletComponent verlet )
	{
		var simData = verlet.SimData;
		var points = simData.CpuPoints;
		for ( int i = 0; i < points.Length; i++ )
		{
			VerletPoint p = points[i];
			if ( !p.IsRopeLocal )
				continue;

			p.Position += simData.Translation;
			points[i] = p;
		}
	}

	private static void CpuApplyForces( SimulationData simData, float deltaTime )
	{
		var gravity = simData.Gravity;
		var points = simData.CpuPoints;


		for ( int y = 0; y < simData.PointGridDims.y; y++ )
		{
			for ( int x = 0; x < simData.PointGridDims.x; x++ )
			{
				var i = y * simData.PointGridDims.x + x;

				var point = points[i];
				if ( point.IsAnchor )
					continue;

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
	}

	private static void CpuApplyConstraints( SimulationData simData )
	{
		var points = simData.CpuPoints;

		void ApplySegmentConstraint( int pIndex, int qIndex )
		{
			VerletPoint p = points[pIndex];
			VerletPoint q = points[qIndex];

			Vector3 delta = p.Position - q.Position;
			float distance = delta.Length;
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = (simData.SegmentLength - distance) / distance * 0.5f;
			}
			Vector3 offset = delta * distanceFactor;

			if ( !p.IsAnchor )
			{
				p.Position += offset;
				points[pIndex] = p;
			}
			if ( !q.IsAnchor )
			{
				q.Position -= offset;
				points[qIndex] = q;
			}
		}

		for ( int pIndex = 0; pIndex < points.Length - 1; pIndex++ )
		{
			int xSize = simData.PointGridDims.x;
			if ( pIndex % xSize < xSize - 1 )
			{
				ApplySegmentConstraint( pIndex, pIndex + 1 );
			}
			int ySize = simData.PointGridDims.y;
			if ( pIndex / ySize < ySize - 1 )
			{
				ApplySegmentConstraint( pIndex, pIndex + ySize );
			}
		}
	}
}
