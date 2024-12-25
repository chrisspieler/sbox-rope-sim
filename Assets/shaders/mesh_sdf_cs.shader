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
	RWStructuredBuffer<int> OutputData < Attribute( "OutputData" ); >;
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

	struct Cell
	{
		int3 Voxel;
		float3 VolumeDims;

		float SignedDistance;

		int SeedId;
		float3 SeedPositionOs;
		float3 SeedNormalOs;

		static int GetIndex( int3 voxel, float3 volumeDims )
		{
			return ( voxel.z * volumeDims.y * volumeDims.z ) + ( voxel.y * volumeDims.x ) + voxel.x;
		}

		static void Initialize( int3 voxel, float3 volumeDims )
		{
			int i = Cell::GetIndex(voxel, volumeDims);
			OutputData[i] = -1;
			OutputTexture[voxel] = float4( 0, 0, 0, 0 );
		}

		static Cell Load( int3 voxel, float3 volumeDims )
		{
			Cell cell;
			cell.Voxel = voxel;
			cell.VolumeDims = volumeDims;

			float4 tex = OutputTexture[cell.Voxel];
			cell.SignedDistance = tex.a;
			int i = Cell::GetIndex(cell.Voxel, cell.VolumeDims);
			cell.SeedId = OutputData[i];

			if ( cell.SeedId < 0 && cell.SignedDistance >= 0 )
				return cell;

			SeedData seed = Seeds[abs( cell.SeedId )];
			cell.SeedPositionOs = seed.PositionOs.xyz;
			cell.SeedNormalOs = seed.Normal.xyz;
			return cell;
		}

		void StoreSeedId()
		{
			int i = Cell::GetIndex(Voxel, VolumeDims);
			int iOut;
			InterlockedExchange( OutputData[i], SeedId, iOut );
		}

		void StoreTexture()
		{
			OutputTexture[Voxel] = float4( SeedPositionOs.xyz, SignedDistance );
		}
	};

//------------------------------------------------------------------
// Stages
//------------------------------------------------------------------

	// Stage 0, 3D (Voxels)
	void InitializeVolume( uint3 DTid, float3 dims )
	{
		if ( any( DTid < 0 ) || any( DTid >= dims ) )
			return;

		Cell::Initialize( DTid, dims );
	}

	// Stage 1, 1D (Triangles)
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

	// Stage 2, 1D (Seeds)
	void InitializeSeedPoints( uint3 DTid, float3 dims )
	{
		SeedData seedData = Seeds[DTid.x];
		uint3 voxel = PositionOsToTexel( seedData.PositionOs.xyz, dims );
		Cell cell = Cell::Load( voxel, dims );
		if ( cell.SeedId >= 0 )
		{
			float3 positionOs = TexelCenterToPositionOs( voxel, dims );
			SeedData previousSeedData = Seeds[cell.SeedId];
			float previousDist = distance( positionOs, cell.SeedPositionOs );
			float currentDist = distance( positionOs, seedData.PositionOs.xyz );
			if ( currentDist > previousDist )
				return;
		}
		cell.SeedId = DTid.x;
		cell.StoreSeedId();
	}

	// Stage 3, 3D (Voxels)
	void JumpFlood( uint3 DTid, float3 dims )
	{
		Cell pCell = Cell::Load( DTid, dims );

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
					
					Cell qCell = Cell::Load( neighbor, dims );

					// If the neighboring voxel is undefined, we can't get anything useful from it.
					if ( qCell.SeedId < 0 )
						continue;

					float3 localPos = TexelCenterToPositionOs( DTid, dims );
					float pDist = distance( localPos, pCell.SeedPositionOs );
					float qDist = distance( localPos, qCell.SeedPositionOs );

					// If this voxel is already defined and given a seed cell that is
					// nearer than this neighbor, don't update this voxel at all.
					if ( pCell.SeedId >= 0 && pDist < qDist )
						continue;

					// We will use our neighbor's seed, as it is nearer to this voxel.
					pCell.SeedId = qCell.SeedId;
					pCell.SeedPositionOs = qCell.SeedPositionOs;
					pCell.StoreSeedId();

					// If we've copied a triangle point from a neighbor, we should evaluate whether
					// our new distance should be positive or negative.
					float sign = 1;

					float3 qToLocalDir = normalize( localPos - qCell.SeedPositionOs );
					bool qFacesAway = dot( qToLocalDir, qCell.SeedNormalOs ) <= 0;
					if ( qFacesAway )
					{
						sign = -1;
					}

					pCell.SignedDistance = qDist * sign;
					pCell.StoreTexture();
				}
			}
		}
	}

	// Stage 4, 3D (Voxels)
	void FinalizeOutput( uint3 DTid, uint3 dims )
	{
		Cell cell = Cell::Load( DTid, dims );
		if ( cell.SignedDistance < 0 )
		{
			cell.SeedId = -cell.SeedId;
		}
		cell.StoreSeedId();
	}

	// Stage 5, 3D (Voxels)
	void DebugNormalized( uint3 DTid, uint3 dims )
	{
		bool useNormal = false;

		Cell cell = Cell::Load( DTid, dims );
		cell.SeedPositionOs -= Mins;
		cell.SeedPositionOs /= abs( Maxs - Mins );
		float sdf = cell.SignedDistance;
		sdf /= distance( Maxs, Mins );
		if ( sdf < 0 )
		{
			sdf *= 0.1;
		}
		cell.SignedDistance = abs( sdf );
		if ( useNormal )
		{
			float3 normal = cell.SeedNormalOs;
			normal += 1;
			normal *= 0.5;
			cell.SeedPositionOs = normal;
		}
		cell.StoreTexture();
	}

//------------------------------------------------------------------
// Main
//------------------------------------------------------------------

	DynamicCombo( D_STAGE, 0..5, Sys( All ) );

	#if D_STAGE == 1 || D_STAGE == 2
	[numthreads( 1024, 1, 1 )]
	#else
	[numthreads( 10, 10, 10 )]
	#endif
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		uint3 dims;
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