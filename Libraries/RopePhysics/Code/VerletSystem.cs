using Sandbox.Diagnostics;

namespace Duccsoft;

public partial class VerletSystem : GameObjectSystem<VerletSystem>
{
	public VerletSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, TickPhysics, "Verlet Tick Physics" );
		Listen( Stage.FinishFixedUpdate, 0, SetShouldCaptureSnapshot, "Verlet Set ShouldCaptureSnapshot" );
	}

	private void TickPhysics()
	{
		// When playing a different scene in the editor, don't simulate this scene.
		if ( Game.IsPlaying && Scene.IsEditor )
			return;

		InitializeGpuCollisions();

		var verletComponents = Scene.GetAllComponents<VerletComponent>();
		foreach( var verlet in verletComponents )
		{
			TickSingle( verlet );
		}
	}

	private void TickSingle( VerletComponent verlet )
	{
		if ( !verlet.IsValid() || verlet.SimData is null )
			return;

		var timer = FastTimer.StartNew();

		var simData = verlet.SimData;
		simData.UpdateAnchors();

		if ( simData.Collisions == null || simData.Collisions.ShouldCaptureSnapshot )
		{
			simData.Collisions = CaptureCollisionSnapshot( simData );
			simData.Collisions.ShouldCaptureSnapshot = false;
		}

		float totalTime = Time.Delta;
		totalTime = MathF.Min( totalTime, verlet.MaxTimeStepPerUpdate );
		while ( totalTime >= 0 )
		{
			var deltaTime = MathF.Min( verlet.TimeStep, totalTime );
			if ( verlet.SimulateOnGPU )
			{
				GpuSimulate( verlet, deltaTime );
			}
			else
			{
				CpuSimulate( verlet, deltaTime );
			}
			totalTime -= verlet.TimeStep;
		}

		if ( verlet.SimulateOnGPU )
		{
			verlet.SimData.LoadPointsFromGpu();
		}

		simData.RecalculatePointBounds();

		verlet.PushDebugTime( timer.ElapsedMilliSeconds );
	}
}
