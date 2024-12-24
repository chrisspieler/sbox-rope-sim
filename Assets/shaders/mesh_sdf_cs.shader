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
	StructuredBuffer<float4> Vertices < Attribute("Vertices"); >;
	StructuredBuffer<uint> Indices < Attribute("Indices"); >;
	RWStructuredBuffer<SeedData> Seeds < Attribute( "Seeds" ); >;
	RWTexture3D<float4> OutputTexture < Attribute( "OutputTexture" ); >;

	int JumpStep < Attribute( "JumpStep" ); >;

	float3 TriangleSurfaceNormal( float3 v0, float3 v1, float3 v2 )
	{
		float3 u = v1 - v0;
		float3 v = v2 - v0;
		return normalize( cross( u, v ) );
	}

	float3 TexelToPositionOs( uint3 texel, float3 dims )
	{
		float3 normalized = float3(texel) / dims;
		return Mins + normalized * ( Maxs - Mins );
	}

	float3 GetVoxelSize( float3 dims) { return ( Maxs - Mins ) / dims; }

	float3 TexelCenterToPositionOs( uint3 texel, float3 dims )
	{
		float3 positionOs = TexelToPositionOs( texel, dims );
		return positionOs + GetVoxelSize( dims ) * 0.5;
	}

	uint3 PositionOsToTexel( float3 pos, float3 dims )
	{
		float3 normalized = ( pos - Mins ) / ( Maxs - Mins );
		uint3 texel = dims * normalized;
		return texel;
	}

	void StoreSeed( int i, float3 positionOs, float3 surfaceNormal )
	{
		SeedData data;
		// w components go unused for now, but could be used later.
		data.PositionOs = float4( positionOs.xyz, 0 );
		data.Normal = float4( surfaceNormal.xyz, 0 );
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
		uint triIndex = DTid.x;
		uint iIndex = triIndex * 3;
		
		// Triangle indices
		uint i0 = Indices[iIndex];
		uint i1 = Indices[iIndex + 1]; 
		uint i2 = Indices[iIndex + 2];
		// Triangle vertices
		float3 v0 = Vertices[i0].xyz;
		float3 v1 = Vertices[i1].xyz;
		float3 v2 = Vertices[i2].xyz;

		// Triangle centroid and normal
		float3 triCenter = ( v0 + v1 + v2 ) / 3.0;
		float3 surfaceNormal = TriangleSurfaceNormal( v0, v1, v2 );

		v0 += ( triCenter - v0 ) * 0.1;
		v1 += ( triCenter - v1 ) * 0.1;
		v2 += ( triCenter - v2 ) * 0.1;

		StoreSeed( triIndex * 4, triCenter, surfaceNormal );
		StoreSeed( triIndex * 4 + 1, v0, surfaceNormal );
		StoreSeed( triIndex * 4 + 2, v1, surfaceNormal );
		StoreSeed( triIndex * 4 + 3, v2, surfaceNormal );
	}

	void InitializeSeedPoints( uint3 DTid, float3 dims )
	{
		SeedData seedData = Seeds[DTid.x];
		uint3 voxel = PositionOsToTexel( seedData.PositionOs.xyz, dims );
		float3 positionOs = TexelCenterToPositionOs( voxel, dims );
		int previousSeed = (int)OutputTexture[voxel].x;
		if ( previousSeed >= 0 )
		{
			SeedData previousSeedData = Seeds[previousSeed];
			float previousDist = distance( positionOs, previousSeedData.PositionOs.xyz );
			float currentDist = distance( positionOs, seedData.PositionOs.xyz );
			if ( currentDist > previousDist )
				return;
		}
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

					float3 p = pSeed.PositionOs.xyz;
					float3 q = qSeed.PositionOs.xyz;

					float3 localPos = TexelCenterToPositionOs( DTid, dims );
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
					bool qFacesAway = dot( qToLocalDir, qSeed.Normal.xyz ) <= 0;
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

	void FinalizeOutput( uint3 DTid, uint3 dims )
	{
		float4 cellData = OutputTexture[DTid];
		SeedData seedData = Seeds[(int)cellData.x];
		OutputTexture[DTid] = float4( seedData.PositionOs.xyz, cellData.y );
	}

	void DebugNormalized( uint3 DTid, uint3 dims )
	{
		float4 data = OutputTexture[DTid];
		float3 positionOs = data.rgb;
		positionOs -= Mins;
		positionOs /= abs( Maxs - Mins );
		float sdf = data.a;
		sdf /= distance( Maxs, Mins );
		if ( sdf < 0 )
		{
			sdf *= 0.1;
		}
		sdf = abs( sdf );
		SeedData seed = Seeds[(int)data.x];
		float3 normal = seed.Normal.xyz;
		normal += 1;
		normal *= 0.5;
		// OutputTexture[DTid] = float4( normal.rgb, 1 );
		OutputTexture[DTid] = float4( positionOs.rgb, sdf );
	}

	DynamicCombo( D_STAGE, 0..5, Sys( All ) );

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
			FinalizeOutput( id, dims );
		#elif D_STAGE == 5
			DebugNormalized( id, dims );
		#endif
	}
}