MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"
	#include "common/classes/Bindless.hlsl"

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
	float TimeStepSize < Attribute( "TimeStepSize"); Default( 0.01 ); >;
	float MaxTimeStepPerUpdate < Attribute( "MaxTimeStepPerUpdate" ); Default( 0.1 ); >;
	float3 Gravity < Attribute( "Gravity" ); Default3( 0, 0, -800.0 ); >;
	float RopeWidth < Attribute( "RopeWidth" ); Default( 1.0 ); >;
	float RopeRenderWidth < Attribute( "RopeRenderWidth" ); Default( 1.0 ); >;
	float RopeTextureCoord < Attribute( "RopeTextureCoord" ); Default( 1.0 ); >;
	float4 RopeTint < Attribute( "RopeTint"); Default4( 0.0, 0.0, 0.0, 1.0 ); >;
	float4x4 MatWorldToLocal < Attribute( "MatWorldToLocal" ); >;

	RWStructuredBuffer<Vertex> OutputVertices < Attribute( "OutputVertices" ); >;
	RWStructuredBuffer<float4> BoundsWs < Attribute( "BoundsWs" ); >;

	DynamicCombo( D_SHAPE_TYPE, 0..1, Sys( All ) );

	int Index2DTo1D( uint2 i )
	{
		return i.y * NumColumns + i.x;
	}

	void ApplyForces( int pIndex, float deltaTime )
	{
		VerletPoint p = Points[pIndex];

		if ( p.IsAnchor() )
		{
			Points[pIndex] = p;
			return;
		}

		float3 temp = p.Position;
		float3 delta = p.Position - p.LastPosition;
		delta *= 1 - (0.95 * deltaTime);
		p.Position += delta;
		// Gravity
		p.Position += Gravity * ( deltaTime * deltaTime );
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
		VerletPoint pCurr = Points[pIndex];
		int x = pIndex % NumColumns;
		int xSize = NumColumns;
		int y = pIndex / NumColumns;
		int ySize = NumColumns;

		if ( x < xSize - 1 )
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

		if  ( x > 0 )
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

		if ( y < ySize - 1 )
		{
			VerletPoint pNext = Points[pIndex + xSize];

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

		if ( y > 0 )
		{
			VerletPoint pPrev = Points[pIndex - xSize];

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

	void ApplyClothConstraints( int pIndex )
	{
		ApplyClothSegmentConstraint( pIndex );
	}

	void ResolveCollisions( int pIndex )
	{
		return;
	}

	groupshared float3 g_vMins[1024];
	groupshared float3 g_vMaxs[1024];

	// Perform parallel reduction to find bounds, but it's very unreliable if the rope has more points than threads.
	void UpdateBounds( int pIndex )
	{
		if ( pIndex >= NumPoints )
		{
			g_vMins[pIndex] = 1e20;
			g_vMaxs[pIndex] = -1e20;
		}
		else 
		{
			VerletPoint p = Points[pIndex];
			g_vMins[pIndex] = p.Position;
			g_vMaxs[pIndex] = p.Position;
		}
		
		GroupMemoryBarrierWithGroupSync();

		for ( int stride = 512; stride > 1; stride /= 2 )
		{
			if ( pIndex >= stride )
				return;
			
			float3 mn1 = g_vMins[pIndex];
			float3 mn2 = g_vMins[pIndex + stride];	
			g_vMins[pIndex] = min( mn1, mn2 );

			float3 mx1 = g_vMaxs[pIndex];
			float3 mx2 = g_vMaxs[pIndex + stride];
			g_vMaxs[pIndex] = max( mx1, mx2 );
			GroupMemoryBarrierWithGroupSync();
		}

		if ( pIndex == 0 )
		{
			float3 mins = g_vMins[0] - 4;
			float3 maxs = g_vMaxs[0] + 4;
			BoundsWs[0] = float4( mins.xyz, 1 );
			BoundsWs[1] = float4( maxs.xyz, 1 );
		}
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

	// From: https://www.shadertoy.com/view/llGcDm
	int Hilbert( int2 p, int level )
	{
		int d = 0;
		for ( int k = 0; k < level; k++ )
		{
			int n = level-k-1;
			int2 r = ( p >> n ) & 1;
			d += ( ( 3 * r.x ) ^ r.y ) << ( 2 * n );
			if ( r.y == 0 ) { if ( r.x == 1 ) { p = ( (int)1 << n ) - 1 - p; } p = p.yx; }
		}
		return d;
	}

	void OutputClothVertex( int pIndex )
	{
		VerletPoint p = Points[pIndex];
		int x = pIndex % NumColumns;
		int y = pIndex / NumColumns;
		float3 vPositionWs = p.Position;
		float3 delta = vPositionWs - p.LastPosition;
		float2 uv = float2( (float)x / NumColumns, (float)y / NumColumns );

		Vertex v;
		v.Position = vPositionWs;
		v.TexCoord0 = float4( uv.x, uv.y, 0, 0 );
		v.Normal = float4( 0, 0, 1, 0 );
		v.Tangent0 = float4( delta.xyz, 0 );
		v.TexCoord1 = RopeTint;
		v.Color0 = float4( 1, 1, 1, 1 );
		
		int iOut = Hilbert( int2( x, y ), 8 );
		OutputVertices[iOut] = v;
	}

	void Simulate( int pIndex, float deltaTime )
	{
		VerletPoint p = Points[pIndex];

		ApplyForces( pIndex, deltaTime );

		if ( !p.IsAnchor() )
		{
			for ( int i = 0; i < Iterations; i++ )
			{
				#if D_SHAPE_TYPE == 0
					ApplyRopeConstraints( pIndex );
				#elif D_SHAPE_TYPE == 1
					ApplyClothConstraints( pIndex );
				#endif
				
				GroupMemoryBarrierWithGroupSync();
				ResolveCollisions( pIndex );
			}
		}
	}

	#if D_SHAPE_TYPE == 1
		[numthreads( 32, 32, 1 )]
	#else
		[numthreads( 1024, 1, 1 )]
	#endif
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		int pIndex = Index2DTo1D( id.xy );

		float totalTime = min( DeltaTime, MaxTimeStepPerUpdate );
		for( int i = 0; i < 50; i++ )
		{
			float deltaTime = min( TimeStepSize, totalTime );
			Simulate( pIndex, deltaTime );
			totalTime -= TimeStepSize;

			if ( totalTime < 0 )
				break;
		}

		UpdateBounds( pIndex );

		#if D_SHAPE_TYPE == 0
			OutputRopeVertex( pIndex );
		#elif D_SHAPE_TYPE == 1
			OutputClothVertex( pIndex );
		#endif
	}
}