namespace Duccsoft;

public class GpuDoubleBuffer<T> : IDisposable, IValid
	where T : unmanaged
{
	public GpuDoubleBuffer( GpuBuffer<T> gpuBuffer )
	{
		BackBuffer = gpuBuffer;
		FrontBuffer = new GpuBuffer<T>( gpuBuffer.ElementCount, gpuBuffer.Usage );
		Timer = new();
	}

	public ITimerStats TimingStats => Timer;
	public int WritesUntilSwap { get; set; } = 1;
	private GpuBuffer<T> FrontBuffer { get; set; }
	private GpuBuffer<T> BackBuffer { get; set; }
	private MultiTimer Timer { get; }
	private int _writes = 0;

	public bool IsValid => FrontBuffer.IsValid() && BackBuffer.IsValid();

	public GpuBuffer<T> SwapToBack()
	{
		_writes++;
		if ( _writes >= WritesUntilSwap )
		{
			(FrontBuffer, BackBuffer) = (BackBuffer, FrontBuffer);
			_writes = 0;
		}
		return BackBuffer;
	}

	public void ReadData( Span<T> output )
	{
		using var timer = Timer.RecordTime();
		FrontBuffer.GetData( output );
	}

	public void ReadData( Span<T> output, int start, int count )
	{
		using var timer = Timer.RecordTime();
		FrontBuffer.GetData( output, start, count );
	}

	public void Dispose()
	{
		FrontBuffer?.Dispose();
		BackBuffer?.Dispose();
		GC.SuppressFinalize( this );
	}
}
