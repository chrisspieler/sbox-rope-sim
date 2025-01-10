namespace Duccsoft;

public partial class VerletSystem
{
	private ComputeShader VerletComputeShader;

	private void InitializeGpuCollisions()
	{
		VerletComputeShader ??= new ComputeShader( "verlet_cloth_cs" );
	}

	private void GpuSimulate( VerletComponent verlet, float deltaTime )
	{
		var simData = verlet.SimData;

		var xThreads = verlet.SimData.PointGridDims.x;
		var yThreads = verlet.SimData.PointGridDims.y;

		using ( PerfLog.Scope( verlet.GetHashCode(), $"GPU Simulate {xThreads}x{yThreads}, {simData.GpuPoints.ElementCount} of size {simData.GpuPoints.ElementSize}"))
		{
			VerletComputeShader.Attributes.Set( "Points", simData.GpuPoints );
			VerletComputeShader.Attributes.Set( "Sticks", simData.GpuSticks );
			VerletComputeShader.Attributes.Set( "NumPoints", simData.Points.Length );
			VerletComputeShader.Attributes.Set( "NumColumns", simData.PointGridDims.y );
			VerletComputeShader.Attributes.Set( "DeltaTime", deltaTime );
			VerletComputeShader.Dispatch( xThreads, yThreads, 1 );
		}
	}

	private void CpuSimulate( VerletComponent verlet, float deltaTime )
	{
		ApplyForces( verlet.SimData, deltaTime );
		for ( int i = 0; i < verlet.SimData.Iterations; i++ )
		{
			ApplyConstraints( verlet.SimData );
			ResolveCollisions( verlet.SimData );
		}
	}

	private static void ApplyForces( SimulationData simData, float deltaTime )
	{
		var gravity = simData.Gravity;
		var points = simData.Points;

		for ( int y = 0; y < simData.PointGridDims.y; y++ )
		{
			for ( int x = 0; x < simData.PointGridDims.x; x++ )
			{
				var i = y * simData.PointGridDims.x + x;

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
	}

	private static void ApplyConstraints( SimulationData simData )
	{
		var points = simData.Points;
		var sticks = simData.Sticks;

		for ( int i = 0; i < sticks.Length; i++ )
		{
			VerletStickConstraint stick = sticks[i];
			VerletPoint pointA = points[stick.Point1];
			VerletPoint pointB = points[stick.Point2];

			Vector3 delta = pointA.Position - pointB.Position;
			float distance = delta.Length;
			float distanceFactor = distance > 0f
				? (stick.Length - distance) / distance * 0.5f
				: 0f;
			Vector3 offset = delta * distanceFactor;

			pointA.Position += offset;
			points[stick.Point1] = pointA;
			pointB.Position -= offset;
			points[stick.Point2] = pointB;
		}
	}
}
