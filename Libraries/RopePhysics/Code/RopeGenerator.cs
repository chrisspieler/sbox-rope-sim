﻿namespace Duccsoft;

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

		for ( int i = 0; i < pointCount; i++ )
		{
			VerletPointFlags flags = VerletPointFlags.None;
			if ( i == 0 || i == pointCount - 1 )
			{
				flags = VerletPointFlags.Anchor;
			}
			Vector3 pos = ray.Project( segmentLength * i );
			VerletPoint p = new( pos, pos, flags );
			points[i] = p;
		}
		return points;
	}
}
