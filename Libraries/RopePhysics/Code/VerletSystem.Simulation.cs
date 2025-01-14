using Sandbox.Diagnostics;
using Sandbox.Utility;

namespace Duccsoft;

public partial class VerletSystem
{
	private enum VerletShapeType
	{
		Rope = 0,
		Cloth = 1,
	}

	private ComputeShader VerletComputeShader;

	private void InitializeGpu()
	{
		VerletComputeShader ??= new ComputeShader( "verlet_cloth_cs" );
		GpuSimulateSceneObject ??= new SceneCustomObject( Scene.SceneWorld )
		{
			RenderingEnabled = true,
			RenderOverride = RenderGpuSimulate,
		};
	}

	private void RenderGpuSimulate( SceneObject so )
	{
		foreach( var verlet in GpuSimulateQueue )
		{
			GpuSimulate( verlet );
		}
		GpuSimulateQueue.Clear();
	}

	private SceneCustomObject GpuSimulateSceneObject;
	private readonly HashSet<VerletComponent> GpuSimulateQueue = [];
	private List<double> PerSimGpuReadbackTimes = [];

	private void GpuSimulate( VerletComponent verlet )
	{
		var simData = verlet.SimData;
		if ( simData is null )
			return;

		simData.Transform = verlet.WorldTransform;

		if ( simData.CpuPointsAreDirty )
		{
			simData.StorePointsToGpu();
		}

		var xThreads = verlet.SimData.PointGridDims.x;
		var yThreads = verlet.SimData.PointGridDims.y;
		var shapeType = yThreads > 1 ? VerletShapeType.Cloth : VerletShapeType.Rope;

		// using var scope = PerfLog.Scope( $"GPU Simulate {xThreads}x{yThreads}, {simData.GpuPoints.ElementCount} of size {simData.GpuPoints.ElementSize}, sticks {simData.GpuSticks.ElementCount} of size {simData.GpuSticks.ElementSize}" );

		// Choose rope or cloth
		VerletComputeShader.Attributes.SetComboEnum( "D_SHAPE_TYPE", shapeType );
		// Layout
		VerletComputeShader.Attributes.Set( "Points", simData.GpuPoints );
		VerletComputeShader.Attributes.Set( "NumPoints", simData.CpuPoints.Length );
		VerletComputeShader.Attributes.Set( "NumColumns", simData.PointGridDims.y );
		VerletComputeShader.Attributes.Set( "SegmentLength", simData.SegmentLength );
		// Simulation
		VerletComputeShader.Attributes.Set( "Iterations", simData.Iterations );
		// Forces
		VerletComputeShader.Attributes.Set( "Gravity", simData.Gravity );
		// Delta
		VerletComputeShader.Attributes.Set( "DeltaTime", Time.Delta );
		VerletComputeShader.Attributes.Set( "TimeStepSize", verlet.TimeStep );
		VerletComputeShader.Attributes.Set( "MaxTimeStepPerUpdate", verlet.MaxTimeStepPerUpdate );
		VerletComputeShader.Attributes.Set( "Translation", simData.Translation );
		// TODO: Allow subtypes of VerletComponent to set these.
		if ( verlet is VerletRope rope )
		{
			VerletComputeShader.Attributes.Set( "RopeWidth", rope.EffectiveRadius );
			VerletComputeShader.Attributes.Set( "RopeRenderWidth", rope.EffectiveRadius * rope.RenderWidthScale );
			VerletComputeShader.Attributes.Set( "RopeTextureCoord", 0f );
			VerletComputeShader.Attributes.Set( "RopeTint", rope.Color );
		}
		// Output
		VerletComputeShader.Attributes.Set( "OutputVertices", simData.ReadbackVertices );
		VerletComputeShader.Attributes.Set( "BoundsWs", simData.ReadbackBounds.SwapToBack() );

		VerletComputeShader.Dispatch( xThreads, yThreads, 1 );

		var timer = FastTimer.StartNew();
		simData.LoadBoundsFromGpu();
		simData.LastTransform = simData.Transform;
		PerSimGpuReadbackTimes.Add( timer.ElapsedMilliSeconds );
	}

	private void CpuSimulate( VerletComponent verlet )
	{
		SimulationData simData = verlet.SimData;
		simData.Transform = verlet.WorldTransform;

		ApplyTransform( verlet );

		float totalTime = Time.Delta;
		totalTime = MathF.Min( totalTime, verlet.MaxTimeStepPerUpdate );
		while ( totalTime >= 0 )
		{
			var deltaTime = MathF.Min( verlet.TimeStep, totalTime );
			ApplyForces( simData, deltaTime );
			for ( int i = 0; i < simData.Iterations; i++ )
			{
				ApplyConstraints( simData );
				if ( verlet.EnableCollision )
				{
					ResolveCollisions( simData );
				}
			}
			totalTime -= verlet.TimeStep;
		}

		simData.LastTransform = simData.Transform;

		verlet.UpdateCpuVertexBuffer( simData.CpuPoints );
		simData.RecalculateCpuPointBounds();

	}

	private static void ApplyTransform( VerletComponent verlet )
	{
		var simData = verlet.SimData;
		var points = simData.CpuPoints;
		for ( int i = 0; i < points.Length ; i++ )
		{
			VerletPoint p = points[i];
			if ( !p.IsRopeLocal )
				continue;

			p.Position += simData.Translation;
			points[i] = p;
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

	private static void ApplyConstraints( SimulationData simData )
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
