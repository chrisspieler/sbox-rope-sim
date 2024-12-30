using Sandbox.Diagnostics;

namespace Duccsoft;

internal interface ITimerStats
{
	double TotalMilliseconds { get; }
	double LastMilliseconds { get; }
}

internal class MultiTimer : ITimerStats
{
	private record JobTimerScope( MultiTimer Timer, FastTimer Stopwatch ) : IDisposable
	{
		public void Dispose() => Timer.PushTime( Stopwatch.ElapsedMilliSeconds );
	}

	public double TotalMilliseconds { get; private set; }
	public double LastMilliseconds { get; private set; }

	public IDisposable RecordTime() => new JobTimerScope( this, FastTimer.StartNew() );

	public void PushTime( double elapsedMilliseconds )
	{
		LastMilliseconds = elapsedMilliseconds;
		TotalMilliseconds += LastMilliseconds;
	}
}
