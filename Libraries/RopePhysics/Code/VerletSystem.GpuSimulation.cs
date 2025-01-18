using Sandbox.Diagnostics;

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
	private List<double> PerSimGpuReadbackTimes = [];
	private List<double> PerSimGpuSimulateTimes = [];
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

	private void RenderGpuSimulate( SceneObject so )
	{
		foreach( var verlet in GpuSimulateQueue )
		{
			GpuUpdateSingle( verlet );
		}
		GpuSimulateQueue.Clear();
	}

	private void GpuUpdateSingle( VerletComponent verlet )
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

		GpuDispatchCalculateMeshBounds( simData );
		// GpuReadbackBounds( simData );
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
		// Forces
		attributes.Set( "Gravity", simData.Gravity );
		// Delta
		attributes.Set( "DeltaTime", simData.LastTick.Value.Relative );
		attributes.Set( "TimeStepSize", timeStep );
		attributes.Set( "MaxTimeStepPerUpdate", maxTotalTimeSteps );
		attributes.Set( "Translation", simData.Translation );
		attributes.Set( "PointWidth", simData.Radius );
		// Colliders
		var sphereColliders = simData.Collisions.SphereColliders.Values.Take( 16 ).Select( c => c.AsGpu() ).ToArray();
		attributes.Set( "NumSphereColliders", sphereColliders.Length );
		if ( sphereColliders.Length > 0 )
		{
			simData.Collisions.GpuSphereColliders.SetData( sphereColliders, 0 );
			attributes.Set( "SphereColliders", simData.Collisions.GpuSphereColliders );
		}
		var boxColliders = simData.Collisions.BoxColliders.Values.Take( 16 ).Select( c => c.AsGpu() ).ToArray();
		attributes.Set( "NumBoxColliders", boxColliders.Length );
		if ( boxColliders.Length > 0 )
		{
			simData.Collisions.GpuBoxColliders.SetData( boxColliders, 0 );
			attributes.Set( "BoxColliders", simData.Collisions.GpuBoxColliders );
		}
		var capsuleColliders = simData.Collisions.CapsuleColliders.Values.Take( 16 ).Select( c => c.AsGpu() ).ToArray();
		attributes.Set( "NumCapsuleColliders", capsuleColliders.Length );
		if ( capsuleColliders.Length > 0 )
		{
			simData.Collisions.GpuCapsuleColliders.SetData( capsuleColliders, 0 );
			attributes.Set( "CapsuleColliders", simData.Collisions.GpuCapsuleColliders );
		}
		var meshColliders = simData.Collisions.MeshColliders.Values.Take( 16 ).Select( c => c.AsGpu() ).ToArray();
		attributes.Set( "NumMeshColliders", meshColliders.Length );
		if ( meshColliders.Length > 0 )
		{
			simData.Collisions.GpuMeshColliders.SetData( meshColliders, 0 );
			attributes.Set( "MeshColliders", simData.Collisions.GpuMeshColliders );
		}
		VerletIntegrationCs.DispatchWithAttributes( attributes, xThreads, yThreads, 1 );

		PerSimGpuSimulateTimes.Add( simTimer.ElapsedMilliSeconds );
	}

	private void GpuDispatchBuildRopeMesh( VerletRope rope )
	{
		SimulationData simData = rope.SimData;

		RenderAttributes attributes = new();

		attributes.Set( "RenderWidth", rope.EffectiveRadius * rope.RenderWidthScale );
		attributes.Set( "TextureCoord", 0f );
		attributes.Set( "Tint", rope.Color );
		attributes.Set( "OutputVertices", simData.ReadbackVertices );
		attributes.Set( "OutputIndices", simData.ReadbackIndices );
	}

	private void GpuDispatchBuildClothMesh( VerletCloth cloth )
	{
		SimulationData simData = cloth.SimData;

		RenderAttributes attributes = new();

		attributes.Set( "OutputVertices", simData.ReadbackVertices );
		attributes.Set( "OutputIndices", simData.ReadbackIndices );
	}

	private void GpuDispatchCalculateMeshBounds( SimulationData simData )
	{
		RenderAttributes attributes = new();

		attributes.Set( "BoundsWs", simData.ReadbackBounds.SwapToBack() );
	}

	private void GpuReadbackBounds( SimulationData simData )
	{
		var timer = FastTimer.StartNew();
		simData.ClearPendingPointUpdates();
		simData.Collisions.Clear();
		simData.LoadBoundsFromGpu();
		simData.LastTransform = simData.Transform;
		simData.LastTick = 0;
		PerSimGpuReadbackTimes.Add( timer.ElapsedMilliSeconds );
	}
}
