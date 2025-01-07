namespace Duccsoft;

public static class IntExtensions
{
	public static int NearestPowerOf2( this int v )
	{
		v--;
		v |= v >> 1;
		v |= v >> 2;
		v |= v >> 4;
		v |= v >> 8;
		v |= v >> 16;
		v++;
		return v;
	}
}
