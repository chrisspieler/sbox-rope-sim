using Sandbox.Diagnostics;

namespace Duccsoft;

public partial class VerletSystem
{
	private SceneCustomObject GpuSimulateSceneObject;
	private readonly HashSet<VerletComponent> GpuSimulateQueue = [];
	internal GpuBuffer<VerletBounds> GpuReadbackBoundsBuffer { get; private set; }

	private List<double> PerSimGpuReadbackTimes = [];
	private List<double> PerSimGpuSimulateTimes = [];
	private List<double> PerSimGpuBuildMeshTimes = [];
	private List<double> PerSimGpuCalculateBoundsTimes = [];
	private List<double> PerSimGpuStorePointsTimes = [];

	public long TotalGpuDataSize { get; private set; }

	private void InitializeGpu()
	{
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

		TotalGpuDataSize = 0;

		foreach( (int i, VerletComponent verlet ) in GpuSimulateQueue.Index() )
		{
			var simData = verlet?.SimData;
			if ( simData is null )
				continue;

			TotalGpuDataSize += simData.GpuData.DataSize;

			simData.RopeIndex = i;
			GpuUpdateSingle( verlet );
		}

		GpuReadbackBounds();
		GpuSimulateQueue.Clear();
	}

	private void GpuUpdateSingle( VerletComponent verlet )
	{
		SimulationData simData = verlet.SimData;
		GpuSimulationData gpuData = simData.GpuData;

		if ( !verlet.IsValid() || simData is null || !gpuData.GpuPoints.IsValid() )
			return;

		simData.Transform = verlet.WorldTransform;
		simData.LastTick ??= Time.Delta;

		if ( simData.LastTick < verlet.FixedTimeStep )
			return;

		GpuApplyUpdatesFromCpu( gpuData );
		GpuDispatchSimulate( gpuData, verlet.FixedTimeStep );

		if ( verlet.EnableRendering )
		{
			if ( verlet is VerletRope rope )
			{
				float width = rope.EffectiveRadius * 2 * rope.RenderWidthScale;
				Color tint = rope.Color;
				GpuDispatchBuildRopeMesh( gpuData, width, tint );
			}
			else if ( verlet is VerletCloth cloth )
			{
				Color tint = cloth.Tint;
				GpuDispatchBuildClothMesh( gpuData, tint );
			}
			float boundsSkin = 1f;
			GpuDispatchCalculateMeshBounds( gpuData, boundsSkin );
		}

		gpuData.PostSimulationCleanup();
	}

	private void GpuApplyUpdatesFromCpu( GpuSimulationData gpuData )
	{
		var timer = FastTimer.StartNew();
		gpuData.ApplyUpdatesFromCpu();
		PerSimGpuStorePointsTimes.Add( timer.ElapsedMilliSeconds );
	}

	private void GpuDispatchSimulate( GpuSimulationData gpuData, float fixedTimeStep )
	{				
		FastTimer simTimer = FastTimer.StartNew();
		gpuData.DispatchSimulate( fixedTimeStep );
		PerSimGpuSimulateTimes.Add( simTimer.ElapsedMilliSeconds );
	}

	private void GpuDispatchBuildRopeMesh( GpuSimulationData gpuData, float width, Color tint )
	{
		var timer = FastTimer.StartNew();
		gpuData.DispatchBuildRopeMesh( width, tint );
		PerSimGpuBuildMeshTimes.Add( timer.ElapsedMilliSeconds );
	}

	private void GpuDispatchBuildClothMesh( GpuSimulationData gpuData, Color tint )
	{
		var timer = FastTimer.StartNew();
		gpuData.DispatchBuildClothMesh( tint );
		PerSimGpuBuildMeshTimes.Add( timer.ElapsedMilliSeconds );
	}

	private void GpuDispatchCalculateMeshBounds( GpuSimulationData gpuData, float skinSize )
	{
		var timer = FastTimer.StartNew();
		gpuData.DispatchCalculateMeshBounds( GpuReadbackBoundsBuffer, skinSize );
		PerSimGpuCalculateBoundsTimes.Add( timer.ElapsedMilliSeconds );
	}

	private void GpuReadbackBounds()
	{
		var timer = FastTimer.StartNew();

		var readbackBounds = new VerletBounds[GpuReadbackBoundsBuffer.ElementCount];
		GpuReadbackBoundsBuffer.GetData( readbackBounds );

		foreach( VerletComponent verlet in GpuSimulateQueue )
		{
			if ( verlet?.SimData is null )
				continue;

			verlet.SimData.Bounds = readbackBounds[verlet.SimData.RopeIndex].AsBBox();
		}
		
		PerSimGpuReadbackTimes.Add( timer.ElapsedMilliSeconds );
	}
}
