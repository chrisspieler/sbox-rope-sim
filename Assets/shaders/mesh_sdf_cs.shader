MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"

	struct SeedData
	{
		float4 PositionOs;
		float4 Normal;
	};

	float3 Mins < Attribute( "Mins" ); >;
	float3 Maxs < Attribute( "Maxs" ); >;
	StructuredBuffer<float3> Vertices < Attribute("Vertices"); >;
	StructuredBuffer<int> Indices < Attribute("Indices"); >;
	RWStructuredBuffer<SeedData> Seeds < Attribute( "Seeds" ); >;
	RWTexture3D<float4> OutputTexture < Attribute( "OutputTexture" ); >;

	int JumpStep < Attribute( "JumpStep" ); >;

	float3 TriangleSurfaceNormal( float3 v0, float3 v1, float3 v2 )
	{
		float3 u = v1 - v0;
		float3 v = v2 - v0;
		return normalize( cross( u, v ) );
	}

	float3 TriangleOrientation( float3 v0, float3 v1, float3 v2, float3 v3 )
	{
		float4x4 matOrient = {
			v0.x, v0.y, v0.z, 1,
			v1.x, v1.y, v1.z, 1,
			v2.x, v2.y, v2.z, 1,
			v3.x, v3.y, v3.z, 1,
		};
		return determinant( matOrient );
	}

	float3 GetVoxelSize( float3 dims)
	{
		return ( Maxs - Mins ) / dims;
	}

	float3 TexelToPositionOs( uint3 texel, float3 dims )
	{
		texel = clamp( texel, 0, dims );
		float3 normalized = float3(texel) / dims;
		return Mins + normalized * ( Maxs - Mins );
	}

	uint3 PositionOsToTexel( float3 pos, float3 dims )
	{
		float3 normalized = ( pos - Mins ) / ( Maxs - Mins );
		uint3 texel = dims * normalized;
		return clamp( texel, 0, dims );
	}

	void StoreSeed( int i, float3 positionOs, float3 surfaceNormal )
	{
		SeedData data;
		// w components go unused for now, but could be used later.
		data.PositionOs = float4( positionOs.xyz, 1 );
		data.Normal = float4( surfaceNormal.xyz, 1 );
		Seeds[i] = data;
	}

	void InitializeVolume( uint3 DTid, float3 dims )
	{
		if ( any( DTid >= dims ) )
			return;

		OutputTexture[DTid] = float4( -1, 0, 0, 0 );
	}

	void FindSeedPoints( uint3 DTid, float3 dims )
	{
		int i = DTid.x;
		float3 v0 = Vertices[Indices[i]];
		float3 v1 = Vertices[Indices[i + 1]];
		float3 v2 = Vertices[Indices[i + 2]];

		float3 triCenter = ( v0 + v1 + v2 ) / 3.0;
		// float3 surfaceNormal = TriangleSurfaceNormal( v0, v2, v1 );
		float3 surfaceNormal = normalize( triCenter );
		float3 triOrient = TriangleOrientation( v0, v1, v2, triCenter + surfaceNormal );
		if ( triOrient.z < 0 )
		{
			float3 temp = v1;
			v1 = v0;
			v0 = temp;
			// surfaceNormal *= -1;
		}

		StoreSeed( i * 4, triCenter, surfaceNormal );
		// StoreSeed( i * 4 + 1, v0, surfaceNormal );
		// StoreSeed( i * 4 + 2, v1, surfaceNormal );
		// StoreSeed( i * 4 + 3, v2, surfaceNormal );
	}

	void InitializeSeedPoints( uint3 DTid, float3 dims )
	{
		SeedData seedData = Seeds[DTid.x];
		uint3 voxel = PositionOsToTexel( seedData.PositionOs.xyz, dims );
		OutputTexture[voxel] = float4( DTid.x, 0, 0, 0 );
	}

	void JumpFlood( uint3 DTid, float3 dims )
	{
		float4 pData = OutputTexture[DTid];

		for ( int z = -1; z <= 1; z++ )
		{
			for ( int y = -1; y <= 1; y++ )
			{
				for ( int x = -1; x <= 1; x++ )
				{
					uint3 nOffset = uint3( x, y, z );
					uint3 neighbor = DTid + nOffset * JumpStep;

					// Don't sample neighbors that are out of the range of the texture.
					if ( any( neighbor < 0 ) || any( neighbor >= dims ) )
						continue;
					
					float4 qData = OutputTexture[neighbor];

					// If the neighboring voxel is undefined, we can't get anything useful from it.
					if ( (int)qData.x < 0 )
						continue;
					

					SeedData qSeed = Seeds[max( 0, (int)qData.x )];
					SeedData pSeed = Seeds[max( 0, (int)pData.x )];

					float3 p = pSeed.PositionOs;
					float3 q = qSeed.PositionOs;

					float3 localPos = TexelToPositionOs( DTid, dims );
					float pDist = distance( localPos, p );
					float qDist = distance( localPos, q );

					// If this voxel is already defined and given a seed cell that is
					// nearer than this neighbor, don't update this voxel at all.
					if ( (int)pData.x > -1 && pDist < qDist )
						continue;

					// If we've copied a triangle point from a neighbor, we should evaluate whether
					// our new distance should be positive or negative.
					float sign = 1;

					float3 qToLocalDir = normalize( localPos - q );
					bool qFacesAway = dot( qToLocalDir, qSeed.Normal.xyz ) < 0;
					if ( qFacesAway )
					{
						sign = -1;
					}

					pData.x = qData.x;
					pData.y = qDist * sign;
				}
			}
		}

		OutputTexture[DTid] = pData;
	}

	void DebugNormalized( uint3 DTid, uint3 dims )
	{
		float4 data = OutputTexture[DTid];
		SeedData seed = Seeds[(int)data.x];
		float3 positionOs = seed.PositionOs;
		float3 normal = seed.Normal;
		positionOs -= Mins;
		positionOs /= abs( Maxs - Mins );
		float sdf = data.y;
		sdf /= distance( Maxs, Mins );
		OutputTexture[DTid] = float4( positionOs.rgb, sdf );
	}

	DynamicCombo( D_STAGE, 0..4, Sys( All ) );

	#if D_STAGE == 1 || D_STAGE == 2
	[numthreads( 1024, 1, 1 )]
	#else
	[numthreads( 10, 10, 10 )]
	#endif
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		float3 dims;
		OutputTexture.GetDimensions( dims.x, dims.y, dims.z );
		#if D_STAGE == 0
			InitializeVolume( id, dims );
		#elif D_STAGE == 1
			FindSeedPoints( id, dims );
		#elif D_STAGE == 2
			InitializeSeedPoints( id, dims );
		#elif D_STAGE == 3
			JumpFlood( id, dims );
		#elif D_STAGE == 4
			DebugNormalized( id, dims );
		#endif
	}
}