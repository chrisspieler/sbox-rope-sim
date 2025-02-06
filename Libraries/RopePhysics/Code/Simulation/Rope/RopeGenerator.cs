namespace Duccsoft;

public static class RopeGenerator
{
	public static VerletPoint[] Generate( Vector3 startPos, Vector3 endPos, int pointCount, out float segmentLength )
	{
		var points = new VerletPoint[pointCount];

		var ray = new Ray( startPos, (endPos - startPos).Normal );
		var distance = startPos.Distance( endPos );
		if ( pointCount < 2 )
		{
			segmentLength = 0f;
			return [];
		}
		
		segmentLength = distance / (pointCount - 1);

		for ( var i = 0; i < pointCount; i++ )
		{
			var pos = ray.Project( segmentLength * i );
			if ( i > 0 && i < pointCount - 1 )
			{
				var p1 = new Vector2( 0, startPos.z  );
				var p2 = new Vector2( distance, endPos.z );
				var x = (float)i / pointCount * distance;
				pos = pos.WithZ( GetCatenary( p1, p2, x, distance * 1.015f ) );
			}
			VerletPoint p = new( pos, pos );
			points[i] = p;
		}
		return points;

		float GetCatenary( Vector2 p1, Vector2 p2, float x, float l )
		{
			/*
			 * This implementation is based on a blog post by Alan Zucconi.
			 * Link: https://www.alanzucconi.com/2020/12/13/catenary-2/
			 */
			
			var h = p2.x - p1.x;
			var v = p2.y - p1.y;
			if ( p1.y > p2.y )
			{
				v = p1.y - p2.y;
			}

			var a = GetCatenaryScale( l, v, h, 10.0f, 0.01f, 100 );
			var p = ( p1.x + p2.x - a * MathF.Log( (l + v) / (l - v) ) ) / 2;
			var q = ( p1.y + p2.y - l * Coth( h / (a * 2) ) ) / 2;
			return a * MathF.Cosh( ( x - p ) / a ) + q;
		}

		float Coth( float x )
		{
			return MathF.Cosh( x ) / MathF.Sinh( x );
		}

		float GetCatenaryScale( float l, float v, float h, float roughStepSize, float precision, int numIterations )
		{
			/*
			 * To find the "a" variable in the equation of a catenary, we will
			 * estimate the point at which two functions, F1 and F2, intersect.
			 */
			
			// From this point, F1 is constant, so we can just keep it in a local.
			var f1 = MathF.Sqrt( l*l - v*v );
			
			// Both F1 and F2 must return positive numbers, so we know "a" is at least zero.
			var a = 0f;
			
			// Come up with a rough estimate for the value of "a", which will be refined later.
			do
			{
				// Inch our estimate for "a" upward in large steps until we know that the
				// difference between F1 and F2 is <= the specified step size.
				a += roughStepSize;
			} 
			// F2 depends on "a", so it gets reevaluated on each step.
			while ( f1 < F2() );
			
			// The value of "a" must be between the previous rough step and our current estimate.
			var min = a - roughStepSize;
			var max = a;
			
			// Perform a binary search to estimate a value of "a" that is within the specified precision. 
			for ( var i = 0; i < numIterations; i++ )
			{
				if ( max - min <= precision )
					break;
				
				// Find the midpoint between the possible values of "a".
				a = (min + max) / 2f;
				
				// If we haven't yet passed the point where the two curves meet...
				if ( f1 < F2() )
				{
					// ...we know that any value lower than our current estimate is too low.
					min = a;
				}
				// Otherwise, we've already reached or passed the point where the two curves meet...
				else
				{
					// ...and we know that any value greater than our current estimate is too high.
					max = a;
				}
			}
			return a;

			float F2() => 2 * a * MathF.Sinh( h / (2 * a) );
		}
	}
}
