namespace Duccsoft;

public class GpuDoubleBuffer<T> : IDisposable, IValid
	where T : unmanaged
{
	public GpuDoubleBuffer( GpuBuffer<T> gpuBuffer, int swapOffset = 0 )
	{
		BackBuffer = gpuBuffer;
		FrontBuffer = new GpuBuffer<T>( gpuBuffer.ElementCount, gpuBuffer.Usage );
		Timer = new();
		WritesSinceSwap = swapOffset.Clamp( 0, SwapInterval );
	}

	public ITimerStats TimingStats => Timer;
	public bool EnableFrontCache { get; set; } = true;
	public int SwapInterval { get; set; } = 1;
	private GpuBuffer<T> FrontBuffer { get; set; }
	private GpuBuffer<T> BackBuffer { get; set; }
	private MultiTimer Timer { get; }
	private int WritesSinceSwap = 0;
	private bool NeedCopyFrontBufferToCache = true;
	private T[] FrontCache;

	public bool IsValid => FrontBuffer.IsValid() && BackBuffer.IsValid();

	public GpuBuffer<T> SwapToBack()
	{
		WritesSinceSwap++;
		if ( WritesSinceSwap >= SwapInterval )
		{
			(FrontBuffer, BackBuffer) = (BackBuffer, FrontBuffer);
			WritesSinceSwap = 0;
			NeedCopyFrontBufferToCache = true;
		}
		return BackBuffer;
	}

	private void EnsureFrontCache()
	{
		if ( FrontCache is null || FrontCache.Length != FrontBuffer.ElementCount )
		{
			FrontCache = new T[FrontBuffer.ElementCount];
		}
	}

	private void ReadCached( Span<T> output )
	{
		EnsureFrontCache();
		if ( NeedCopyFrontBufferToCache )
		{
			FrontBuffer.GetData( FrontCache );
			NeedCopyFrontBufferToCache = false;
		}
		FrontCache.CopyTo( output );
	}

	public void ReadData( Span<T> output )
	{
		using var timer = Timer.RecordTime();

		if ( EnableFrontCache )
		{
			// Until async readback from GpuBuffers is supported, call GpuBuffer<T>.GetData
			// only once every SwapInterval frames, and cache the results.
			// Relevant feature request: https://github.com/Facepunch/sbox-issues/issues/7270
			ReadCached( output );
		}
		else
		{
			FrontBuffer.GetData( output );
			return;
		}
	}

	public void Dispose()
	{
		FrontBuffer?.Dispose();
		BackBuffer?.Dispose();
		GC.SuppressFinalize( this );
	}
}
