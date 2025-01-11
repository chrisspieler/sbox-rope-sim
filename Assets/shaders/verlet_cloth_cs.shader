MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"

	struct Vertex 
	{
		float3 Position;
		float4 TexCoord0;
		float4 Normal;
		float4 Tangent0;
		float4 TexCoord1;
		float4 Color0;
	};

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
	float3 StartPosition < Attribute( "StartPosition" ); >;
	float3 EndPosition < Attribute( "EndPosition" ); >;
	int NumPoints < Attribute( "NumPoints" ); >;
	int NumColumns < Attribute( "NumColumns" ); >;
	int Iterations < Attribute( "Iterations" ); >;
	float SegmentLength < Attribute( "SegmentLength" ); Default( 1.0 ); >;
	float DeltaTime < Attribute( "DeltaTime" ); >;
	float RopeWidth < Attribute( "RopeWidth" ); Default( 1.0 ); >;
	float RopeRenderWidth < Attribute( "RopeRenderWidth" ); Default( 1.0 ); >;
	float RopeTextureCoord < Attribute( "RopeTextureCoord" ); Default( 1.0 ); >;
	float4 RopeTint < Attribute( "RopeTint"); Default4( 0.0, 0.0, 0.0, 1.0 ); >;

	RWStructuredBuffer<Vertex> OutputVertices < Attribute( "OutputVertices" ); >;


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


	void ApplyRopeConstraints( int pIndex )
	{
		VerletPoint pCurr = Points[pIndex];

		if ( pIndex < NumPoints - 1 )
		{
			VerletPoint pNext = Points[pIndex + 1];

			float3 delta = pCurr.Position - pNext.Position;
			float distance = length( delta );
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = ( SegmentLength - distance ) / distance * 0.5;
			}
			float3 offset = delta * distanceFactor;

			pCurr.Position += offset;
			Points[pIndex] = pCurr;
		}

		if  ( pIndex > 0 )
		{
			VerletPoint pPrev = Points[pIndex - 1];

			float3 delta = pPrev.Position - pCurr.Position;
			float distance = length( delta );
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = ( SegmentLength - distance ) / distance * 0.5;
			}
			float3 offset = delta * distanceFactor;

			pCurr.Position -= offset;
			Points[pIndex] = pCurr;
		}
	}

	void ApplyConstraints( int pIndex )
	{
		ApplyRopeConstraints( pIndex );
	}

	void ResolveCollisions( int pIndex )
	{
		return;
	}

	void OutputVertex( int pIndex )
	{
		VerletPoint p = Points[pIndex];
		float3 delta = p.Position - p.LastPosition;
		Vertex v;
		v.Position = p.Position;
		v.TexCoord0 = float4( RopeRenderWidth, RopeTextureCoord, 0, 0 );
		v.Normal = float4( 0, 0, 1, 0 );
		v.Tangent0 = float4( delta.xyz, 0 );
		v.TexCoord1 = RopeTint;
		v.Color0 = float4( 1, 1, 1, 1 );
		if ( pIndex == 0 )
		{
			OutputVertices[0] = v;
		}
		OutputVertices[pIndex + 1] = v;
		if ( pIndex == NumPoints - 1 )
		{
			OutputVertices[NumPoints + 1] = v;
		}
	}

	[numthreads( 1024, 1, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		int pIndex = Index2DTo1D( id.xy );
		VerletPoint p = Points[pIndex];

		ApplyForces( pIndex );

		if ( !p.IsAnchor() )
		{
			for ( int i = 0; i < Iterations; i++ )
			{
				ApplyConstraints( pIndex );
				ResolveCollisions( pIndex );
			}
		}

		OutputVertex( pIndex );
	}
}