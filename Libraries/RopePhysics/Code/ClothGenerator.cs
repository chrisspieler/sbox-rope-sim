namespace Duccsoft;

public static class ClothGenerator
{
	public static VerletPoint[] Generate( Vector3 startPos, Vector3 endPos, int dims, out float spacing )
	{
		var points = new VerletPoint[dims * dims];

		var xDir = (endPos - startPos).Normal;
		var yDir = xDir.Cross( Vector3.Down );
		var yRay = new Ray( startPos, yDir );

		spacing = startPos.Distance( endPos ) / dims;
		
		for ( int y = 0; y < dims; y++ )
		{
			for( int x = 0; x < dims; x++ )
			{
				Vector3 xPos = yRay.Project( spacing * y );
				Ray xRay = new( xPos, xDir );
				Vector3 pos = xRay.Project( spacing * x );
				VerletPoint p = new( pos, pos );
				points[ y * dims + x ] = p;
			}
		}
		return points;
	}
}
