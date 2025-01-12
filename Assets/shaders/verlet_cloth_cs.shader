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
	float3 Gravity < Attribute( "Gravity" ); Default3( 0, 0, -800.0 ); >;
	float RopeWidth < Attribute( "RopeWidth" ); Default( 1.0 ); >;
	float RopeRenderWidth < Attribute( "RopeRenderWidth" ); Default( 1.0 ); >;
	float RopeTextureCoord < Attribute( "RopeTextureCoord" ); Default( 1.0 ); >;
	float4 RopeTint < Attribute( "RopeTint"); Default4( 0.0, 0.0, 0.0, 1.0 ); >;
	float4x4 MatWorldToLocal < Attribute( "MatWorldToLocal" ); >;

	RWStructuredBuffer<Vertex> OutputVertices < Attribute( "OutputVertices" ); >;

	DynamicCombo( D_SHAPE_TYPE, 0..1, Sys( All ) );

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
		p.Position += Gravity * ( DeltaTime * DeltaTime );
		p.LastPosition = temp;
		Points[pIndex] = p;
	}


	void ApplyRopeSegmentConstraint( int pIndex )
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

	void ApplyRopeConstraints( int pIndex )
	{
		ApplyRopeSegmentConstraint( pIndex );
	}

	void ApplyClothSegmentConstraint( int pIndex )
	{

	}

	void ApplyClothConstraints( int pIndex )
	{
		ApplyClothSegmentConstraint( pIndex );
	}

	void ResolveCollisions( int pIndex )
	{
		return;
	}

	void OutputRopeVertex( int pIndex )
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

	void OutputClothVertex( uint2 pos, int pIndex )
	{
		VerletPoint p = Points[pIndex];
		float3 vPositionWs = p.Position;
		float3 delta = vPositionWs - p.LastPosition;
		float2 uv = float2( (float)pos.x / NumColumns, (float)pos.y / NumColumns );
		Vertex v;
		v.Position = vPositionWs;
		v.TexCoord0 = float4( uv.x, uv.y, 0, 0 );
		v.Normal = float4( 0, 0, 1, 0 );
		v.Tangent0 = float4( delta.xyz, 0 );
		v.TexCoord1 = RopeTint;
		v.Color0 = float4( 1, 1, 1, 1 );
		OutputVertices[pIndex] = v;

		// TODO: Output indices???
		// int idx = pos.y * NumColumns + pos.x;
	}

	#if D_SHAPE_TYPE == 1
		[numthreads( 32, 32, 1 )]
	#else
		[numthreads( 1024, 1, 1 )]
	#endif
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		int pIndex = Index2DTo1D( id.xy );
		VerletPoint p = Points[pIndex];

		ApplyForces( pIndex );

		if ( !p.IsAnchor() )
		{
			for ( int i = 0; i < Iterations; i++ )
			{
				#if D_SHAPE_TYPE == 0
					ApplyRopeConstraints( pIndex );
				#elif D_SHAPE_TYPE == 1
					ApplyClothConstraints( pIndex );
				#endif

				ResolveCollisions( pIndex );
			}
		}

		#if D_SHAPE_TYPE == 0
			OutputRopeVertex( pIndex );
		#elif D_SHAPE_TYPE == 1
			OutputClothVertex( id.xy, pIndex );
		#endif
	}
}