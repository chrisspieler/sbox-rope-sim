using Sandbox.Utility;

namespace Duccsoft;

public class PerformanceSystem : GameObjectSystem<PerformanceSystem>
{
	public PerformanceSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, OnStartUpdate, "OnStartUpdate" );
	}

	public float CurrentFramerate => 1f / FrameTimes.Average();
	private readonly CircularBuffer<float> FrameTimes = new( 20 );
	public RealTimeSince TimeSinceStartUpdate { get; private set; } = 0;

	private void OnStartUpdate()
	{
		float frameTime = TimeSinceStartUpdate.Relative;
		TimeSinceStartUpdate = 0;

		FrameTimes.PushBack( frameTime );
	}
}
