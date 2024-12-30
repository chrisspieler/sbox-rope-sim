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
	public double AverageMilliseconds { get; private set; }
	public int RecordCounter { get; set; } = 0;


	public IDisposable RecordTime() => new JobTimerScope( this, FastTimer.StartNew() );

	public void PushTime( double elapsedMilliseconds )
	{
		LastMilliseconds = elapsedMilliseconds;
		TotalMilliseconds += LastMilliseconds;
		AverageMilliseconds = (AverageMilliseconds * RecordCounter + LastMilliseconds) / (RecordCounter + 1);
	}
}
