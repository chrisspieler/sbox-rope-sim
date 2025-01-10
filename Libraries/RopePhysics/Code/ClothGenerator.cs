namespace Duccsoft;

public static class ClothGenerator
{
	public static VerletPoint[] Generate( Vector3 startPos, Vector3 endPos, int dims, out float spacing )
	{
		var points = new VerletPoint[dims * dims];

		var xDir = (endPos - startPos).Normal;
		var yDir = xDir.Cross( Vector3.Up );
		var yRay = new Ray( startPos, yDir );

		spacing = startPos.Distance( endPos ) / dims;
		
		for ( int y = 0; y < dims; y++ )
		{
			for( int x = 0; x < dims; x++ )
			{
				var xPos = yRay.Project( spacing * y );
				var xRay = new Ray( xPos, xDir );
				var pos = xRay.Project( spacing * x );
				points[ y * dims + x ] = new VerletPoint( pos, pos );
			}
		}
		
		return points;
	}
}
