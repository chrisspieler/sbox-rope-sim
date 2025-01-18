MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"
	#include "shared/verlet.hlsl"
	#include "shared/index.hlsl"

	struct Vertex 
	{
		float3 Position;
		float4 TexCoord0;
		float4 Normal;
		float4 Tangent0;
		float4 TexCoord1;
		float4 Color0;
	};

	int NumPoints < Attribute( "NumPoints" ); >;
	int NumColumns < Attribute( "NumColumns" ); >;
	RWStructuredBuffer<VerletPoint> Points < Attribute( "Points" ); >;
	float4 Tint < Attribute( "Tint" ); Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	RWStructuredBuffer<Vertex> OutputVertices < Attribute( "OutputVertices" ); >;
	RWStructuredBuffer<uint> OutputIndices < Attribute( "OutputIndices" ); >;

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
		v.TexCoord1 = Tint;
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


	[numthreads( 32, 32, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		int i = Convert2DIndexTo1D( id.xy, uint2( NumPoints / NumColumns, NumColumns ) );
		OutputClothVertex( i );
	}	
}