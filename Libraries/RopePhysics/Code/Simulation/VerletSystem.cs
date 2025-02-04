using Sandbox.Diagnostics;
using Sandbox.Utility;

namespace Duccsoft;

public partial class VerletSystem : GameObjectSystem<VerletSystem>
{
	public enum SimulationScope
	{
		None,
		SimulationSet,
		All,
	}

	public SimulationScope SceneSimulationScope { get; set; } = SimulationScope.SimulationSet;
	public HashSet<VerletComponent> SimulationSet { get; set; } = [];

	public VerletSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, TickPhysics, "Verlet Tick Physics" );
		Listen( Stage.FinishFixedUpdate, 0, UpdateStats, "Update Verlet Perf Stats" );
		Listen( Stage.FinishFixedUpdate, 100, SetShouldCaptureSnapshot, "Verlet Set ShouldCaptureSnapshot" );
	}

	public double AverageTotalSimulationCPUTime => SimulationFrameTimes.Average();
	private readonly CircularBuffer<double> SimulationFrameTimes = new CircularBuffer<double>( 30 );

	public double AverageTotalCaptureSnapshotTime => CaptureSnapshotFrameTimes.Average();
	private readonly CircularBuffer<double> CaptureSnapshotFrameTimes = new( 30 );
	public double LastFrameCaptureSnapshotTime => PerSimCaptureSnapshotTimes.Sum();

	public double AverageTotalGpuStorePointsTime => GpuStorePointsFrameTimes.Average();
	public CircularBuffer<double> GpuStorePointsFrameTimes = new CircularBuffer<double>( 30 );
	public double LastFrameTotalGpuStorePointsTime => PerSimGpuStorePointsTimes.Sum();

	public double AverageTotalGpuSimulationTime => GpuSimulationFrameTimes.Average();
	private readonly CircularBuffer<double> GpuSimulationFrameTimes = new CircularBuffer<double>( 30 );
	public double LastFrameTotalGpuSimulationTime => PerSimGpuSimulateTimes.Sum();

	public double AverageTotalGpuBuildMeshTimes => GpuBuildMeshFrameTimes.Average();
	private readonly CircularBuffer<double> GpuBuildMeshFrameTimes = new CircularBuffer<double>( 30 );
	public double LastFrameTotalGpuBuildMeshTime => PerSimGpuBuildMeshTimes.Sum();

	public double AverageTotalGpuCalculateBoundsTimes => GpuCalculateBoundsTimes.Average();
	private readonly CircularBuffer<double> GpuCalculateBoundsTimes = new CircularBuffer<double>( 30 );
	public double LastFrameTotalGpuCalculateBoundsTime => GpuCalculateBoundsTimes.Sum();

	public double AverageTotalGpuReadbackTime => GpuReadbackFrameTimes.Average();
	private readonly CircularBuffer<double> GpuReadbackFrameTimes = new CircularBuffer<double>( 30 );
	public double LastFrameTotalGpuReadbackTime => PerSimGpuReadbackTimes.Sum();

	private void UpdateStats()
	{
		// CPU
		CaptureSnapshotFrameTimes.PushBack( LastFrameCaptureSnapshotTime );
		PerSimCaptureSnapshotTimes.Clear();

		// GPU
		GpuStorePointsFrameTimes.PushBack( LastFrameTotalGpuStorePointsTime );
		GpuSimulationFrameTimes.PushBack( LastFrameTotalGpuSimulationTime );
		GpuBuildMeshFrameTimes.PushBack( LastFrameTotalGpuBuildMeshTime );
		GpuCalculateBoundsTimes.PushBack( LastFrameTotalGpuCalculateBoundsTime );
		GpuReadbackFrameTimes.PushBack( LastFrameTotalGpuReadbackTime );
		PerSimGpuStorePointsTimes.Clear();
		PerSimGpuSimulateTimes.Clear();
		PerSimGpuBuildMeshTimes.Clear();
		PerSimGpuCalculateBoundsTimes.Clear();
		PerSimGpuReadbackTimes.Clear();
	}

	private void TickPhysics()
	{
		InitializeGpu();

		double totalMilliseconds = 0;
		var verletComponents = Scene.GetAllComponents<VerletComponent>();
		foreach( var verlet in verletComponents )
		{
			var timer = FastTimer.StartNew();
			TickSingle( verlet );
			double elapsedMilliseconds = timer.ElapsedMilliSeconds;
			totalMilliseconds += elapsedMilliseconds;
			verlet.PushDebugTime( elapsedMilliseconds );
		}
		SimulationFrameTimes.PushBack( totalMilliseconds );
	}

	private bool ShouldSimulate( VerletComponent sim )
	{
		if ( sim is null || sim.SimData is null || !sim.GpuData.GpuPoints.IsValid() )
			return false;

		// If we are playing a scene...
		if ( Game.IsPlaying )
		{
			// ...don't simulate any editor scenes.
			return !Scene.IsEditor;
		}

		return SceneSimulationScope switch
		{
			SimulationScope.All => true,
			SimulationScope.SimulationSet => SimulationSet.Contains( sim ),
			_ => false,
		};
	}

	private void TickSingle( VerletComponent verlet )
	{
		var simData = verlet.SimData;

		if ( simData.Collisions == null || simData.Collisions.ShouldCaptureSnapshot )
		{
			simData.Collisions = CaptureCollisionSnapshot( simData );
			simData.Collisions.ShouldCaptureSnapshot = false;
		}

		if ( verlet.SimulateOnGPU )
		{
			GpuSimulateQueue.Add( verlet );
		}
		else
		{
			CpuSimulate( verlet );
		}
	}
}
