using Sandbox.Diagnostics;
using Sandbox.Utility;

namespace Duccsoft;

public partial class VerletSystem : GameObjectSystem<VerletSystem>
{
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
		// When playing a different scene in the editor, don't simulate this scene.
		if ( Game.IsPlaying && Scene.IsEditor )
			return;

		InitializeGpu();

		double totalMilliseconds = 0;
		var verletComponents = Scene.GetAllComponents<VerletComponent>();
		foreach( var verlet in verletComponents )
		{
			TickSingle( verlet, out double elapsedMilliseconds );
			totalMilliseconds += elapsedMilliseconds;
		}
		SimulationFrameTimes.PushBack( totalMilliseconds );
	}

	private void TickSingle( VerletComponent verlet, out double elapsedMilliseconds )
	{
		if ( !verlet.IsValid() || verlet.SimData is null )
		{
			elapsedMilliseconds = 0;
			return;
		}

		var timer = FastTimer.StartNew();
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

		elapsedMilliseconds = timer.ElapsedMilliSeconds;
		verlet.PushDebugTime( elapsedMilliseconds );

	}
}
