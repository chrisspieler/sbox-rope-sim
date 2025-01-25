using Sandbox.Diagnostics;

namespace Duccsoft;

public class PerfLog : IDisposable
{
	[ConVar( "rope_perf_log" )]
	public static bool EnablePerfLog { get; set; } = false;

	private PerfLog() { }
	private int Id = -1;
	private string Label;
	private FastTimer Stopwatch;

	public static PerfLog Scope( string label )
	{
		var perflog = new PerfLog();
		perflog.Id = -1;
		perflog.Label = label;
		perflog.Stopwatch = FastTimer.StartNew();
		return perflog;
	}

	public static PerfLog Scope( int id, string label )
	{
		var perflog = new PerfLog();
		perflog.Id = id;
		perflog.Label = label;
		perflog.Stopwatch = FastTimer.StartNew();
		return perflog;
	}

	public void Dispose()
	{
		if ( !EnablePerfLog )
			return;

		var mdfId = Id >= 0 ? $"MDF {Id}" : "";
		Log.Info( $"{mdfId} {Stopwatch.ElapsedMilliSeconds:F3}ms: {Label}" );
	}
}
