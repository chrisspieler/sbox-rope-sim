namespace Duccsoft;

public interface IGpuCollider<T> where T : unmanaged
{
	T AsGpu();
}
