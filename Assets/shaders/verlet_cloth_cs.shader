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
		bool IsRopeLocal() { return ( Flags & 2 ) == 2; }
	};

	struct VerletPointUpdate
	{
		float3 Position;
		int Index;
		int UpdateFlags;
		int PointFlags;
		int2 Padding;
		
		bool ShouldUpdatePosition() { return ( UpdateFlags & 1 ) == 1; }
		bool ShouldUpdateFlags() { return ( UpdateFlags & 2 ) == 2; }
	};

	struct VerletStickConstraint
	{
		int Point1;
		int Point2;
		float Length;
		int Padding;
	};

	
	// Layout
	int NumPoints < Attribute( "NumPoints" ); >;
	int NumColumns < Attribute( "NumColumns" ); >;
	float SegmentLength < Attribute( "SegmentLength" ); Default( 1.0 ); >;
	RWStructuredBuffer<VerletPoint> Points < Attribute( "Points" ); >;
	// Updates
	int NumPointUpdates < Attribute( "NumPointUpdates" ); Default( 0 ); >;
	RWStructuredBuffer<VerletPointUpdate> PointUpdates < Attribute ( "PointUpdates" ); >;
	// Simulation
	int Iterations < Attribute( "Iterations" ); >;
	// Forces
	float3 Gravity < Attribute( "Gravity" ); Default3( 0, 0, -800.0 ); >;
	// Rope rendering
	float RopeWidth < Attribute( "RopeWidth" ); Default( 2.0 ); >;
	float RopeRenderWidth < Attribute( "RopeRenderWidth" ); Default( 2.0 ); >;
	float RopeTextureCoord < Attribute( "RopeTextureCoord" ); Default( 1.0 ); >;
	float4 RopeTint < Attribute( "RopeTint"); Default4( 0.0, 0.0, 0.0, 1.0 ); >;
	// Delta
	float DeltaTime < Attribute( "DeltaTime" ); >;
	float TimeStepSize < Attribute( "TimeStepSize"); Default( 0.01 ); >;
	float MaxTimeStepPerUpdate < Attribute( "MaxTimeStepPerUpdate" ); Default( 0.1 ); >;
	float3 Translation < Attribute( "Translation" ); >;
	// Output
	RWStructuredBuffer<Vertex> OutputVertices < Attribute( "OutputVertices" ); >;
	RWStructuredBuffer<uint> OutputIndices < Attribute( "OutputIndices" ); >;
	RWStructuredBuffer<float4> BoundsWs < Attribute( "BoundsWs" ); >;

	struct SphereCollider
	{
		float3 Center;
		float Radius;
		float4x4 LocalToWorld;
		float4x4 WorldToLocal;

		void ResolveCollision( int pIndex )
		{
			VerletPoint p = Points[pIndex];
			float3 pPositionOs = mul( WorldToLocal, float4( p.Position.xyz, 1 ) ).xyz;
			float dist = distance( pPositionOs, Center );
			if ( dist - Radius > RopeWidth )
				return;

			float3 dir = normalize( pPositionOs - Center );
			float3 hitPositionOs = Center + dir * ( RopeWidth + Radius );
			float3 hitPositionWs = mul( LocalToWorld, float4( hitPositionOs.xyz, 1 ) ).xyz;
			p.Position = hitPositionWs;
			Points[pIndex] = p;
		}
	};

	// Colliders
	int NumSphereColliders < Attribute( "NumSphereColliders" ); >;
	RWStructuredBuffer<SphereCollider> SphereColliders < Attribute( "SphereColliders" ); >;

	DynamicCombo( D_SHAPE_TYPE, 0..1, Sys( All ) );
	int Index2DTo1D( uint2 i )
	{
		return i.y * NumColumns + i.x;
	}

	void ApplyUpdates( int DTid )
	{
		VerletPointUpdate update = PointUpdates[DTid];
		int pIndex = update.Index;
		VerletPoint p = Points[pIndex];
		if ( update.ShouldUpdatePosition() )
		{
			p.Position = update.Position;
			p.LastPosition = update.Position;
		}
		if ( update.ShouldUpdateFlags() )
		{
			p.Flags = update.PointFlags;
		}
		Points[pIndex] = p;
	}

	void ApplyTransform( int pIndex, VerletPoint p )
	{
		p.Position += Translation;
		Points[pIndex] = p;
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

	void ResolveSphereCollisions( int pIndex )
	{
		for( int i = 0; i < NumSphereColliders; i++ )
		{
			SphereCollider sphere = SphereColliders[i];
			sphere.ResolveCollision( pIndex );
		}
	}

	void ResolveCollisions( int pIndex )
	{
		ResolveSphereCollisions( pIndex );
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

	float3 CalculateTriNormal( float3 v0, float3 v1, float3 v2 )
	{
		float3 u = v1 - v0;
		float3 v = v2 - v0;
		return normalize( cross( u, v ) );
	}

	groupshared float3 TriNormals[2048];

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
		
		OutputVertices[pIndex] = v;

		uint baseIndex = pIndex;

		int i0 = baseIndex;
		int i1 = baseIndex + 1;
		int i2 = baseIndex + NumColumns;

		bool isOnEdge = x == NumColumns - 1 || y == NumColumns - 1 ;

		if ( !isOnEdge )
		{
			int iIndex0 = pIndex * 6;
			int iIndex1 = pIndex * 6 + 1;
			int iIndex2 = pIndex * 6 + 2;
			OutputIndices[iIndex0] = i0;
			OutputIndices[iIndex1] = i1;
			OutputIndices[iIndex2] = i2;
		}

		int i3 = baseIndex + NumColumns;
		int i4 = baseIndex + 1;
		int i5 = baseIndex + NumColumns + 1;

		if ( !isOnEdge )
		{
			int iIndex3 = pIndex * 6 + 3;
			int iIndex4 = pIndex * 6 + 4;
			int iIndex5 = pIndex * 6 + 5;
			OutputIndices[iIndex3] = i3;
			OutputIndices[iIndex4] = i4;
			OutputIndices[iIndex5] = i5;
		}

		GroupMemoryBarrierWithGroupSync();

		float3 tNor0 = float3( 0, 0, 1 );
		float3 tNor1 = float3( 0, 0, 1 );

		if ( !isOnEdge )
		{
			float3 v0 = OutputVertices[i0].Position;
			float3 v1 = OutputVertices[i1].Position;
			float3 v2 = OutputVertices[i2].Position;
			tNor0 = CalculateTriNormal( v0, v1, v2 );
		}

		if ( !isOnEdge )
		{
			float3 v3 = OutputVertices[i3].Position;
			float3 v4 = OutputVertices[i4].Position;
			float3 v5 = OutputVertices[i5].Position;
			tNor1 = CalculateTriNormal( v3, v4, v5 );
		}

		float3 nor = 0;
		if ( pIndex >= 1023 )
		{
			v.Normal = float4( normalize( tNor0 + tNor1 ).xyz, 1 );
			OutputVertices[pIndex] = v;
			return;
		}

		GroupMemoryBarrierWithGroupSync();

		TriNormals[pIndex * 2] = tNor0;
		TriNormals[pIndex * 2 + 1] = tNor1;
		
		GroupMemoryBarrierWithGroupSync();
		int iSelfTri = pIndex * 2;
		int offsetNW = 0;
		int offsetNNE = -1;
		int offsetENE = -2;
		int offsetSE = -(NumColumns * 2) - 1;
		int offsetSSW = -(NumColumns * 2);
		int offsetWSW = -(NumColumns * 2) + 1;

		if ( x < NumColumns - 1 && y < NumColumns - 1 )
		{
			// NW
			nor += TriNormals[iSelfTri + offsetNW];
		}
		if ( x > 0 && y < NumColumns - 1 )
		{
			// NNE
			nor += TriNormals[iSelfTri + offsetNNE];
			// ENE
			nor += TriNormals[iSelfTri + offsetENE];
		}
		if ( x > 0 && y > 0 )
		{
			// SE
			nor += TriNormals[iSelfTri + offsetSE];
		}
		if ( x < NumColumns - 1 && y > 0 )
		{
			// SSW 
			nor += TriNormals[iSelfTri + offsetSSW];
			// WSW
			nor += TriNormals[iSelfTri + offsetWSW];
		}

		v.Normal = float4( normalize( nor ).xyz, 0 );
		OutputVertices[pIndex] = v;

		GroupMemoryBarrierWithGroupSync();
	}

	void Simulate( int pIndex, float deltaTime )
	{
		VerletPoint p = Points[pIndex];

		ApplyForces( pIndex, deltaTime );
		
		if ( p.IsAnchor() )
		return;
		
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

	#if D_SHAPE_TYPE == 1
		[numthreads( 32, 32, 1 )]
	#else
		[numthreads( 1024, 1, 1 )]
	#endif
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		int pIndex = Index2DTo1D( id.xy );

		// TODO: Apply updates only from thread group 0,0
		if ( pIndex < NumPointUpdates )
		{
			ApplyUpdates( pIndex );
		}
		GroupMemoryBarrierWithGroupSync();
		
		VerletPoint p = Points[pIndex];
		if ( p.IsRopeLocal() )
		{
			ApplyTransform( pIndex, p );
		}
		GroupMemoryBarrierWithGroupSync();

		float totalTime = min( DeltaTime, MaxTimeStepPerUpdate );
		for( int i = 0; i < 50; i++ )
		{
			float deltaTime = min( TimeStepSize, totalTime );
			Simulate( pIndex, deltaTime );
			totalTime -= TimeStepSize;

			if ( totalTime < 0 )
				break;
		}

		GroupMemoryBarrierWithGroupSync();

		UpdateBounds( pIndex );

		#if D_SHAPE_TYPE == 0
			OutputRopeVertex( pIndex );
		#elif D_SHAPE_TYPE == 1
			OutputClothVertex( pIndex );
		#endif
	}
}