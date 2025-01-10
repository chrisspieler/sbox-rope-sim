namespace Duccsoft;

public struct VerletStickConstraint
{
	public VerletStickConstraint( int point1, int point2, float length )
	{
		Point1 = point1;
		Point2 = point2;
		Length = length;
	}

	public int Point1; 
	public int Point2;
	public float Length;
	public int Padding;
}
