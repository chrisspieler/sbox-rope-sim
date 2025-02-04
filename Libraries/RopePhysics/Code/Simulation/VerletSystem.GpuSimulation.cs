using Sandbox.Diagnostics;

namespace Duccsoft;

public partial class VerletSystem
{
	private SceneCustomObject GpuSimulateSceneObject;
	private readonly HashSet<VerletComponent> GpuSimulateQueue = [];
	private GpuBuffer<VerletBounds> _gpuReadbackBoundsBuffer;

	internal List<double> PerSimGpuReadbackTimes = [];
	internal List<double> PerSimGpuSimulateTimes = [];
	internal List<double> PerSimGpuBuildMeshTimes = [];
	internal List<double> PerSimGpuCalculateBoundsTimes = [];
	internal List<double> PerSimGpuStorePointsTimes = [];

	public long TotalGpuDataSize { get; private set; }

	private void InitializeGpu()
	{
		GpuSimulateSceneObject ??= new SceneCustomObject( Scene.SceneWorld )
		{
			RenderingEnabled = true,
			RenderOverride = RenderGpuSimulate,
		};
	}

	private void RenderGpuSimulate( SceneObject so )
	{
		int simCount = GpuSimulateQueue.Count;
		if ( simCount < 1 )
			return;

		GpuBufferUtils.EnsureCount( ref _gpuReadbackBoundsBuffer, simCount );

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

		bool shouldSimulate = ShouldSimulate( verlet ) && simData.LastTick >= verlet.FixedTimeStep;

		if ( shouldSimulate )
		{
			GpuDispatchSimulate( gpuData, verlet.FixedTimeStep );
			gpuData.PostSimulationCleanup();
		}

		if ( !gpuData.IsMeshBuilt || ( verlet.EnableRendering && shouldSimulate) )
		{
			verlet.UpdateGpuMesh();
			var timer = FastTimer.StartNew();
			gpuData.DispatchCalculateMeshBounds( _gpuReadbackBoundsBuffer, 1f );
			PerSimGpuCalculateBoundsTimes.Add( timer.ElapsedMilliSeconds );
		}
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

	private void GpuReadbackBounds()
	{
		var timer = FastTimer.StartNew();

		var readbackBounds = new VerletBounds[_gpuReadbackBoundsBuffer.ElementCount];
		_gpuReadbackBoundsBuffer.GetData( readbackBounds );

		foreach( VerletComponent verlet in GpuSimulateQueue )
		{
			if ( verlet?.SimData is null )
				continue;

			verlet.SimData.Bounds = readbackBounds[verlet.SimData.RopeIndex].AsBBox();
		}
		
		PerSimGpuReadbackTimes.Add( timer.ElapsedMilliSeconds );
	}
}
