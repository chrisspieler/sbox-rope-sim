﻿namespace Duccsoft;

public partial class VerletSystem
{
	private ComputeShader VerletComputeShader;

	private void InitializeGpuCollisions()
	{
		VerletComputeShader ??= new ComputeShader( "verlet_cloth_cs" );
	}

	private enum VerletShapeType
	{
		Rope = 0,
		Cloth = 1,
	}

	private void GpuSimulate( VerletComponent verlet, float deltaTime )
	{
		var simData = verlet.SimData;

		var xThreads = verlet.SimData.PointGridDims.x;
		var yThreads = verlet.SimData.PointGridDims.y;
		var shapeType = yThreads > 1 ? VerletShapeType.Cloth : VerletShapeType.Rope;

		// using var scope = PerfLog.Scope( $"GPU Simulate {xThreads}x{yThreads}, {simData.GpuPoints.ElementCount} of size {simData.GpuPoints.ElementSize}, sticks {simData.GpuSticks.ElementCount} of size {simData.GpuSticks.ElementSize}" );

		VerletComputeShader.Attributes.SetComboEnum( "D_SHAPE_TYPE", shapeType );
		VerletComputeShader.Attributes.Set( "Points", simData.GpuPoints );
		VerletComputeShader.Attributes.Set( "StartPosition", verlet.StartPosition );
		VerletComputeShader.Attributes.Set( "EndPosition", verlet.EndPosition );
		VerletComputeShader.Attributes.Set( "NumPoints", simData.CpuPoints.Length );
		VerletComputeShader.Attributes.Set( "NumColumns", simData.PointGridDims.y );
		VerletComputeShader.Attributes.Set( "SegmentLength", simData.SegmentLength );
		VerletComputeShader.Attributes.Set( "Iterations", simData.Iterations );
		VerletComputeShader.Attributes.Set( "DeltaTime", deltaTime );
		VerletComputeShader.Attributes.Set( "Gravity", simData.Gravity );
		// TODO: Allow subtypes of VerletComponent to set these.
		if ( verlet is VerletRope rope )
		{
			VerletComputeShader.Attributes.Set( "RopeWidth", rope.EffectiveRadius );
			VerletComputeShader.Attributes.Set( "RopeRenderWidth", rope.EffectiveRadius * rope.RenderWidthScale );
			VerletComputeShader.Attributes.Set( "RopeTextureCoord", 0f );
			VerletComputeShader.Attributes.Set( "RopeTint", rope.Color );
		}
		VerletComputeShader.Attributes.Set( "OutputVertices", simData.ReadbackVertices );
		VerletComputeShader.Dispatch( xThreads, yThreads, 1 );
	}

	private void CpuSimulate( VerletComponent verlet, float deltaTime )
	{
		ApplyForces( verlet.SimData, deltaTime );
		for ( int i = 0; i < verlet.SimData.Iterations; i++ )
		{
			ApplyConstraints( verlet.SimData );
			if ( verlet.EnableCollision )
			{
				ResolveCollisions( verlet.SimData );
			}
		}
	}

	private static void ApplyForces( SimulationData simData, float deltaTime )
	{
		var gravity = simData.Gravity;
		var points = simData.CpuPoints;

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
		var points = simData.CpuPoints;

		for ( int pIndex = 0; pIndex < points.Length - 1; pIndex++ )
		{
			VerletPoint pCurr = points[pIndex];
			VerletPoint pNext = points[pIndex + 1];

			Vector3 delta = pCurr.Position - pNext.Position;
			float distance = delta.Length;
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = (simData.SegmentLength - distance) / distance * 0.5f;
			}
			Vector3 offset = delta * distanceFactor;

			if ( !pCurr.IsAnchor )
			{
				pCurr.Position += offset;
				points[pIndex] = pCurr;
			}
			if ( !pNext.IsAnchor )
			{
				pNext.Position -= offset;
				points[pIndex + 1] = pNext;
			}
		}
	}
}
