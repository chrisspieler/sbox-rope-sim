namespace Duccsoft;

// TODO: Rename from VerletRopeSystem to VerletSystem and support cloth as well.
public partial class VerletRopeSystem : GameObjectSystem<VerletRopeSystem>
{
	public VerletRopeSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, UpdateAllRopes, "Update Verlet Ropes" );
		Listen( Stage.FinishFixedUpdate, 0, SetShouldCaptureSnapshot, "Set ShouldCaptureSnapshot" );
	}

	private void UpdateAllRopes()
	{
		// When playing a different scene in the editor, don't simulate this scene.
		if ( Game.IsPlaying && Scene.IsEditor )
			return;

		var ropes = Scene.GetAllComponents<VerletRope>();
		foreach( var rope in ropes )
		{
			UpdateRope( rope );
		}
	}

	private void UpdateRope( VerletRope rope )
	{
		if ( !rope.IsValid() || rope.SimData is null )
			return;

		var simData = rope.SimData;

		if ( simData.Collisions == null || simData.Collisions.ShouldCaptureSnapshot )
		{
			simData.Collisions = CaptureCollisionSnapshot( simData );
			simData.Collisions.ShouldCaptureSnapshot = false;
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
