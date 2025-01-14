using Sandbox.Diagnostics;
using Sandbox.Utility;

namespace Duccsoft;

public partial class VerletSystem : GameObjectSystem<VerletSystem>
{
	public VerletSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, TickPhysics, "Verlet Tick Physics" );
		Listen( Stage.FinishFixedUpdate, 0, SetShouldCaptureSnapshot, "Verlet Set ShouldCaptureSnapshot" );
	}

	public double AverageTotalSimulationCPUTime => SimulationFrameTimes.Average();
	private readonly CircularBuffer<double> SimulationFrameTimes = new CircularBuffer<double>( 30 );
	public double AverageTotalGpuReadbackTime => GpuReadbackFrameTimes.Average();
	private readonly CircularBuffer<double> GpuReadbackFrameTimes = new CircularBuffer<double>( 30 );
	public double LastFrameTotalGpuReadbackTime => PerSimGpuReadbackTimes.Sum();

	private void TickPhysics()
	{
		GpuReadbackFrameTimes.PushBack( LastFrameTotalGpuReadbackTime );
		PerSimGpuReadbackTimes.Clear();

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

		simData.Transform = verlet.WorldTransform;

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

		simData.LastTransform = simData.Transform;

		elapsedMilliseconds = timer.ElapsedMilliSeconds;
		verlet.PushDebugTime( elapsedMilliseconds );

	}
}
