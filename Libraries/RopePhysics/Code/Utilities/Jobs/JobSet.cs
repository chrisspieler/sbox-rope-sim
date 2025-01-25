namespace Duccsoft;

internal class JobSet<TInput, TOutput> 
	: Job<IEnumerable<Job<TInput, TOutput>>, Dictionary<int, TOutput>>
{
	public JobSet(int id, IEnumerable<Job<TInput, TOutput>> inputData, double runTimeLimit = 1_000 ) : base( id, inputData )
	{
		RunTimeLimit = runTimeLimit;
		MuteDebugLog = true;
	}

	public double RunTimeLimit { get; set; } = 1_000;

	protected override bool RunInternal( out Dictionary<int, TOutput> result )
	{
		result = [];

		double remainingTime = RunTimeLimit;
		bool anyJobsSuspended = false;
		foreach ( var job in Input )
		{
			// If the job completed, add it to the result list.
			if ( job.Run( out TOutput jobResult ) )
			{
				result[job.Id] = jobResult;
			}
			// If the job is still in progress, don't add it to the result list.
			else
			{
				anyJobsSuspended = true;
			}
			remainingTime -= job.TimerStats.LastMilliseconds;
			// Check whether we ran out of time processing this job.
			if ( remainingTime < 0 )
				return false;
		}
		// If any jobs were still in progress, then this job is still in progress.
		return !anyJobsSuspended;
	}
}
