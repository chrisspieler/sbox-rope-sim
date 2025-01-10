MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"

	struct VerletPoint
	{
		float3 Position;
		int Flags;
		float3 LastPosition;
		int Padding;

		bool IsAnchor() { return ( Flags & 1 ) == 1; }
	};

	struct VerletStickConstraint
	{
		int Point1;
		int Point2;
		float Length;
		int Padding;
	};

	RWStructuredBuffer<VerletPoint> Points < Attribute( "Points" ); >;
	RWStructuredBuffer<VerletStickConstraint> Sticks < Attribute( "Sticks" ); >;
	float3 StartPosition < Attribute( "StartPosition" ); >;
	float3 EndPosition < Attribute( "EndPosition" ); >;
	int NumPoints < Attribute( "NumPoints" ); >;
	int NumSticks < Attribute( "NumSticks" ); >;
	int NumColumns < Attribute( "NumColumns" ); >;
	int Iterations < Attribute( "Iterations" ); >;
	float DeltaTime < Attribute( "DeltaTime" ); >;



	int Index2DTo1D( uint2 i )
	{
		return i.y * NumColumns + i.x;
	}

	void ApplyForces( int pIndex )
	{
		VerletPoint p = Points[pIndex];

		if ( p.IsAnchor() )
		{
			if ( pIndex == 0 )
			{
				p.Position = StartPosition;
				p.LastPosition = StartPosition;
			}
			else if ( pIndex == NumPoints - 1 )
			{
				p.Position = EndPosition;
				p.LastPosition = EndPosition;
			}
			Points[pIndex] = p;
			return;
		}

		float3 temp = p.Position;
		float3 delta = p.Position - p.LastPosition;
		delta *= 1 - (0.95 * DeltaTime);
		p.Position += delta;
		// Gravity
		p.Position += float3( 0, 0, -800 ) * ( DeltaTime * DeltaTime );
		p.LastPosition = temp;
		Points[pIndex] = p;
	}

	void ApplyStickConstraints( int pIndex )
	{
		for( int i = 0; i < NumSticks; i++ )
		{
			VerletStickConstraint c = Sticks[i];
			if ( c.Point1 != pIndex && c.Point2 != pIndex )
				continue;

			VerletPoint pointA = Points[c.Point1];
			VerletPoint pointB = Points[c.Point2];

			float3 delta = pointA.Position - pointB.Position;
			float distance = length( delta );
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = ( c.Length - distance ) / distance * 0.5;
			}
			float3 offset = delta * distanceFactor;

			pointA.Position += offset;
			pointB.Position -= offset;

			if ( c.Point1 == pIndex )
			{
				Points[c.Point1] = pointA;
			}
			if ( c.Point2 == pIndex )
			{
				Points[c.Point2] = pointB;
			}
		}
	}

	void ApplyConstraints( int pIndex )
	{
		ApplyStickConstraints( pIndex );
	}

	void ResolveCollisions( int pIndex )
	{
		return;
	}

	[numthreads( 512, 1, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		int pIndex = Index2DTo1D( id.xy );
		ApplyForces( pIndex );

		if ( Points[pIndex].IsAnchor() )
			return;

		for ( int i = 0; i < Iterations; i++ )
		{
			ApplyConstraints( pIndex );
			ResolveCollisions( pIndex );
		}
	}
}