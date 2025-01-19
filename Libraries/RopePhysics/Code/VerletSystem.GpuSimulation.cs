﻿using Sandbox.Diagnostics;

namespace Duccsoft;

public partial class VerletSystem
{
	private enum VerletShapeType
	{
		Rope = 0,
		Cloth = 1,
	}

	private ComputeShader VerletIntegrationCs;
	private ComputeShader VerletRopeMeshCs;
	private ComputeShader VerletClothMeshCs;
	private ComputeShader MeshBoundsCs;
	private SceneCustomObject GpuSimulateSceneObject;
	private readonly HashSet<VerletComponent> GpuSimulateQueue = [];
	private GpuBuffer<VerletBounds> GpuReadbackBoundsBuffer;

	private List<double> PerSimGpuReadbackTimes = [];
	private List<double> PerSimGpuSimulateTimes = [];
	private List<double> PerSimGpuBuildMeshTimes = [];
	private List<double> PerSimGpuCalculateBoundsTimes = [];
	private List<double> PerSimGpuStorePointsTimes = [];

	private void InitializeGpu()
	{
		VerletIntegrationCs ??= new ComputeShader( "shaders/physics/verlet_integration_cs.shader" );
		VerletRopeMeshCs ??= new ComputeShader( "shaders/mesh/verlet_rope_mesh_cs.shader" );
		VerletClothMeshCs ??= new ComputeShader( "shaders/mesh/verlet_cloth_mesh_cs.shader" );
		MeshBoundsCs ??= new ComputeShader( "shaders/mesh/mesh_bounds_cs.shader" );
		GpuSimulateSceneObject ??= new SceneCustomObject( Scene.SceneWorld )
		{
			RenderingEnabled = true,
			RenderOverride = RenderGpuSimulate,
		};
	}

	private void EnsureGpuReadbackBuffer()
	{
		int elementCount = GpuSimulateQueue.Count;
		if ( !GpuReadbackBoundsBuffer.IsValid() || GpuReadbackBoundsBuffer.ElementCount != elementCount )
		{
			GpuReadbackBoundsBuffer = new( elementCount );
		}
	}

	private void RenderGpuSimulate( SceneObject so )
	{
		if ( GpuSimulateQueue.Count < 1 )
			return;

		EnsureGpuReadbackBuffer();
		foreach( (int i, VerletComponent verlet ) in GpuSimulateQueue.Index() )
		{
			GpuUpdateSingle( verlet, i );
		}
		GpuReadbackBounds();
		GpuSimulateQueue.Clear();
	}

	private void GpuUpdateSingle( VerletComponent verlet, int i )
	{
		SimulationData simData = verlet.SimData;
		if ( !verlet.IsValid() || simData is null )
			return;

		simData.Transform = verlet.WorldTransform;
		simData.LastTick ??= Time.Delta;

		GpuStorePoints( simData );
		GpuDispatchSimulate( simData, verlet.TimeStep, verlet.MaxTimeStepPerUpdate );
		if ( verlet is VerletRope rope )
		{
			GpuDispatchBuildRopeMesh( rope );
		}
		else if ( verlet is VerletCloth cloth )
		{
			GpuDispatchBuildClothMesh( cloth );
		}
		else
		{
			return;
		}

		GpuDispatchCalculateMeshBounds( simData, i );
		GpuPostSimCleanup( simData );
	}

	private void GpuStorePoints( SimulationData simData )
	{
		var timer = FastTimer.StartNew();
		if ( simData.CpuPointsAreDirty )
		{
			simData.StorePointsToGpu();
		}
		simData.GpuPointUpdates ??= new GpuBuffer<VerletPointUpdate>( 1024, GpuBuffer.UsageFlags.Structured );
		if ( simData.PendingPointUpdates > 0 )
		{
			VerletPointUpdate[] updateQueue = simData.PointUpdateQueue.Values.ToArray();
			simData.GpuPointUpdates.SetData( updateQueue );
		}
		PerSimGpuStorePointsTimes.Add( timer.ElapsedMilliSeconds );
	}

	private void GpuDispatchSimulate( SimulationData simData, float timeStep, float maxTotalTimeSteps )
	{
		var xThreads = simData.PointGridDims.x;
		var yThreads = simData.PointGridDims.y;
		
		using var scope = PerfLog.Scope( $"GPU Simulate {xThreads}x{yThreads}, {simData.GpuPoints.ElementCount} of size {simData.GpuPoints.ElementSize}, updates {simData.GpuPointUpdates.ElementCount} of size {simData.GpuPointUpdates.ElementSize}" );
		FastTimer simTimer = FastTimer.StartNew();

		RenderAttributes attributes = new();

		var shapeType = yThreads > 1 ? VerletShapeType.Cloth : VerletShapeType.Rope;

		// Choose rope or cloth
		attributes.SetComboEnum( "D_SHAPE_TYPE", shapeType );
		// Choose whether the rope/cloth should collide with things
		attributes.SetCombo( "D_COLLISION", true );

		// Layout
		attributes.Set( "NumPoints", simData.CpuPoints.Length );
		attributes.Set( "NumColumns", simData.PointGridDims.y );
		attributes.Set( "SegmentLength", simData.SegmentLength );
		attributes.Set( "Points", simData.GpuPoints );
		// Updates
		attributes.Set( "NumPointUpdates", simData.PendingPointUpdates );
		attributes.Set( "PointUpdates", simData.GpuPointUpdates );
		// Simulation
		attributes.Set( "Iterations", simData.Iterations );
		attributes.Set( "PointRadius", simData.Radius );
		// Forces
		attributes.Set( "Gravity", simData.Gravity );
		// Delta
		attributes.Set( "DeltaTime", simData.LastTick.Value.Relative );
		attributes.Set( "TimeStepSize", timeStep );
		attributes.Set( "MaxTimeStepPerUpdate", maxTotalTimeSteps );
		attributes.Set( "Translation", simData.Translation );
		// Colliders
		simData.Collisions.ApplyColliderAttributes( attributes );

		VerletIntegrationCs.DispatchWithAttributes( attributes, xThreads, yThreads, 1 );

		PerSimGpuSimulateTimes.Add( simTimer.ElapsedMilliSeconds );
	}

	private void GpuDispatchBuildRopeMesh( VerletRope rope )
	{
		var timer = FastTimer.StartNew();

		SimulationData simData = rope.SimData;
		RenderAttributes attributes = new();

		attributes.Set( "NumPoints", simData.CpuPoints.Length );
		attributes.Set( "Points", simData.GpuPoints );
		attributes.Set( "RenderWidth", rope.EffectiveRadius * rope.RenderWidthScale );
		attributes.Set( "TextureCoord", 0f );
		attributes.Set( "Tint", rope.Color );
		attributes.Set( "OutputVertices", simData.ReadbackVertices );
		attributes.Set( "OutputIndices", simData.ReadbackIndices );

		VerletRopeMeshCs.DispatchWithAttributes( attributes, simData.CpuPoints.Length, 1, 1 );

		PerSimGpuBuildMeshTimes.Add( timer.ElapsedMilliSeconds );
	}

	private void GpuDispatchBuildClothMesh( VerletCloth cloth )
	{
		var timer = FastTimer.StartNew();

		SimulationData simData = cloth.SimData;
		RenderAttributes attributes = new();

		attributes.Set( "NumPoints", simData.CpuPoints.Length );
		attributes.Set( "NumColumns", simData.PointGridDims.y );
		attributes.Set( "Points", simData.GpuPoints );
		attributes.Set( "Tint", Color.White );
		attributes.Set( "OutputVertices", simData.ReadbackVertices );
		attributes.Set( "OutputIndices", simData.ReadbackIndices );

		VerletClothMeshCs.DispatchWithAttributes( attributes, simData.PointGridDims.x, simData.PointGridDims.y, 1 );

		PerSimGpuBuildMeshTimes.Add( timer.ElapsedMilliSeconds );
	}

	private void GpuDispatchCalculateMeshBounds( SimulationData simData, int boundsIndex )
	{
		var timer = FastTimer.StartNew();

		RenderAttributes attributes = new();

		attributes.Set( "NumPoints", simData.CpuPoints.Length );
		attributes.Set( "Points", simData.GpuPoints );
		attributes.Set( "SkinSize", 1f );

		attributes.Set( "BoundsIndex", boundsIndex );
		attributes.Set( "Bounds", GpuReadbackBoundsBuffer );

		MeshBoundsCs.DispatchWithAttributes( attributes, simData.CpuPoints.Length );

		PerSimGpuCalculateBoundsTimes.Add( timer.ElapsedMilliSeconds );
	}

	private void GpuReadbackBounds()
	{
		var timer = FastTimer.StartNew();

		var readbackBounds = new VerletBounds[GpuReadbackBoundsBuffer.ElementCount];
		GpuReadbackBoundsBuffer.GetData( readbackBounds );

		foreach( ( int i, VerletComponent verlet ) in GpuSimulateQueue.Index() )
		{
			if ( verlet?.SimData is null )
				continue;

			verlet.SimData.Bounds = readbackBounds[i].AsBBox();
		}
		
		PerSimGpuReadbackTimes.Add( timer.ElapsedMilliSeconds );
	}

	private void GpuPostSimCleanup( SimulationData simData )
	{
		simData.ClearPendingPointUpdates();
		simData.Collisions.Clear();
		simData.LastTransform = simData.Transform;
		simData.LastTick = 0;
	}
}
