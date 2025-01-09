namespace Duccsoft;

public partial class VerletRopeSystem : GameObjectSystem<VerletRopeSystem>
{
	public VerletRopeSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, UpdateAllRopes, "Update Verlet Ropes" );
		Listen( Stage.FinishFixedUpdate, 0, () => ShouldCaptureSnapshot = true, "Set ShouldCaptureSnapshot" );
	}

	private bool ShouldCaptureSnapshot { get; set; } = true;

	private void UpdateAllRopes()
	{
		if ( !Game.IsPlaying )
			return;

		var ropes = Scene.GetAllComponents<RopePhysics>();
		foreach( var rope in ropes )
		{
			UpdateRope( rope );
		}
	}

	private void UpdateRope( RopePhysics rope )
	{
		if ( !rope.IsValid() || rope.SimData is null )
			return;

		var simData = rope.SimData;

		if ( ShouldCaptureSnapshot )
		{
			simData.Collisions = CaptureCollisionSnapshot( simData );
			ShouldCaptureSnapshot = false;
		}

		float totalTime = Time.Delta;
		totalTime = MathF.Min( totalTime, rope.MaxTimeStepPerUpdate );
		while( totalTime >= 0 )
		{
			var deltaTime = MathF.Min( rope.TimeStep, totalTime );
			CpuSimulate( simData, deltaTime );
			totalTime -= rope.TimeStep;
		}

		simData.RecalculatePointBounds();
	}

	private void CpuSimulate( RopeSimulationData simData, float deltaTime )
	{
		ApplyForces( simData, deltaTime );
		for ( int i = 0; i < simData.Iterations; i++ )
		{
			ApplyConstraints( simData );
			ResolveCollisions( simData );
		}
	}
}
