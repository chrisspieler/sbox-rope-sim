namespace Duccsoft;

internal abstract class Job
{
	[ConVar( "rope_job_debug" )]
	public static bool EnableDebugLog { get; set; }

	internal enum JobStatus
	{
		Pending,
		InProgress,
		Completed
	}

	protected Job( int id )
	{
		Id = id;
		Timer = new MultiTimer();
	}

	public int Id { get; }
	public ITimerStats TimerStats => Timer;
	private MultiTimer Timer { get; set; }
	public bool MuteDebugLog { get; set; } = false;

	public JobStatus Status { get; private set; } = JobStatus.Pending;
	public bool IsPending => Status == JobStatus.Pending;
	public bool IsInProgress => Status == JobStatus.InProgress;
	public bool IsCompleted => Status == JobStatus.Completed;
	public Action OnCompleted { get; set; }

	protected record RunResult<T>( bool Completed, T Output );

	protected bool Run<T>( out T output, Func<RunResult<T>> runner )
	{
		DebugLogStart();

		// Refuse to run jobs that were already completed.
		if ( Status == JobStatus.Completed )
			throw new InvalidOperationException( $"Job {GetType().Name} Id {Id} was already completed!" );

		bool completed;
		using ( Timer.RecordTime() )
		{
			(completed, output) = runner.Invoke();
		}
		Status = completed ? JobStatus.Completed : JobStatus.InProgress;

		DebugLogEnd();

		if ( completed )
		{
			OnCompleted?.Invoke();
		}

		return completed;
	}

	private void DebugLogStart()
	{
		if ( !EnableDebugLog || MuteDebugLog )
			return;

		var statusString = "BEGIN";
		if ( Status == JobStatus.InProgress )
		{
			statusString = "RESUME";
		}
		Log.Info( $"{statusString} {GetType().Name} # {Id}" );
	}

	private void DebugLogEnd()
	{
		if ( !EnableDebugLog || MuteDebugLog )
			return;

		var statusString = "COMPLETE";
		if ( Status == JobStatus.InProgress )
		{
			statusString = "SUSPEND";
		}
		var timeString = $"({TimerStats.LastMilliseconds:F3}ms)";
		Log.Info( $"{timeString} {statusString} {GetType().Name} # {Id} " );
	}
}

internal abstract class Job<TInput, TOutput> : Job
{
	public Job( int id, TInput inputData ) : base( id )
	{
		Input = inputData;
	}

	public TInput Input { get; }

	protected abstract bool RunInternal( out TOutput output );

	public bool Run( out TOutput output )
	{
		RunResult<TOutput> Runner()
		{
			var completed = RunInternal( out TOutput outData );
			return new RunResult<TOutput>( completed, outData );
		}

		return Run( out output, Runner );
	}
}

internal static class JobExtensions
{
	/// <summary>
	/// Returns true if the job is not null and hasn't yet completed.
	/// </summary>
	public static bool IsActive( this Job job )
	{
		if ( job is null )
			return false;

		return job.Status != Job.JobStatus.Completed;
	}
}
