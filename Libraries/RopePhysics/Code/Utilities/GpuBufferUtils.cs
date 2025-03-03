namespace Duccsoft;

public static class GpuBufferUtils
{
	public static void EnsureCount<T>(
		ref GpuBuffer<T> gpuBuffer,
		int elementCount,
		GpuBuffer.UsageFlags flags = GpuBuffer.UsageFlags.Structured
	)
		where T : unmanaged
	{
		if ( !gpuBuffer.IsValid() )
		{
			gpuBuffer = new GpuBuffer<T>( elementCount, flags );
			return;
		}

		if ( gpuBuffer.ElementCount == elementCount && gpuBuffer.Usage == flags )
			return;

		gpuBuffer?.Dispose();
		gpuBuffer = new GpuBuffer<T>( elementCount, flags );
	}

	public static void EnsureMinCount<T>(
		ref GpuBuffer<T> gpuBuffer,
		int minElementCount,
		GpuBuffer.UsageFlags flags = GpuBuffer.UsageFlags.Structured
	)
		where T : unmanaged
	{
		if ( !gpuBuffer.IsValid() )
		{
			gpuBuffer = new GpuBuffer<T>( minElementCount, flags );
			return;
		}

		if ( gpuBuffer.ElementCount >= minElementCount && gpuBuffer.Usage == flags )
			return;

		gpuBuffer?.Dispose();
		gpuBuffer = new GpuBuffer<T>( minElementCount, flags );
	}

	public static void EnsureDisposedAndNull<T>( ref GpuBuffer<T> gpuBuffer )
		where T : unmanaged
	{
		if ( gpuBuffer.IsValid() )
		{
			gpuBuffer.Dispose();
		}

		gpuBuffer = null;
	}
}
