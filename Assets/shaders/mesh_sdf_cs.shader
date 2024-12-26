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
		texel = clamp( texel, 0, dims );
		float3 positionOs = TexelToPositionOs( texel, dims );
		return positionOs + GetVoxelSize( dims ) * 0.5;
	}

	uint3 PositionOsToTexel( float3 pos, float3 dims )
	{
		float3 normalized = ( pos - Mins ) / ( Maxs - Mins );
		uint3 texel = dims * normalized;
		return clamp(texel, 0, dims);
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
			uint3 dims = (uint3)volumeDims;
			return ( voxel.z * dims.y * dims.z ) + ( voxel.y * dims.x ) + voxel.x;
		}

		static void Initialize( int3 voxel, float3 volumeDims )
		{
			int i = Cell::GetIndex(voxel, volumeDims);
			OutputData[i] = -1;
			OutputTexture[voxel] = float4( 0, 0, 0, 0 );
		}

		bool IsValid()
		{
			return !any( Voxel < 0 ) && !any( Voxel >= (uint3)VolumeDims );
		}

		bool IsSeed()
		{
			return !( SeedId == -1 && SignedDistance < 0 );
		}

		static Cell Load( int3 voxel, float3 volumeDims )
		{
			Cell cell;
			cell.Voxel = voxel;
			cell.VolumeDims = volumeDims;
			if ( !cell.IsValid() )
				return cell;

			float4 tex = OutputTexture[cell.Voxel];
			cell.SignedDistance = tex.a;
			int i = Cell::GetIndex(cell.Voxel, cell.VolumeDims);
			cell.SeedId = OutputData[i];
			
			if ( !cell.IsSeed() )
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

//==================================================================
// STAGES
//==================================================================

//------------------------------------------------------------------
// Stage 0, 3D (Voxels)
//------------------------------------------------------------------
	void InitializeVolume( uint3 voxel, float3 dims )
	{
		if ( any( voxel < 0 ) || any( voxel >= dims ) )
			return;

		Cell::Initialize( voxel, dims );
	}

//------------------------------------------------------------------
// Stage 1, 1D (Triangles)
//------------------------------------------------------------------
	void FindSeedPoints( int triId, float3 dims )
	{
		uint iIndex = triId * 3;

		uint numIndices, indexStride;
		Indices.GetDimensions( numIndices, indexStride );
		if ( iIndex >= numIndices )
			return;
		
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

		v0 += ( triCenter - v0 ) * 0.5;
		v1 += ( triCenter - v1 ) * 0.5;
		v2 += ( triCenter - v2 ) * 0.5;

		// Each triangle has four seeds - the center, and slightly inside of each vertex.
		int sIndex = triId * 4;
		StoreSeed( sIndex, triCenter, surfaceNormal );
		StoreSeed( sIndex + 1, v0, surfaceNormal );
		StoreSeed( sIndex + 2, v1, surfaceNormal );
		StoreSeed( sIndex + 3, v2, surfaceNormal );
	}

//------------------------------------------------------------------
// Stage 2, 1D (Seeds)
//------------------------------------------------------------------
	void InitializeSeedPoints( int seedId, float3 dims )
	{
		uint numSeeds, seedStride;
		Seeds.GetDimensions( numSeeds, seedStride );
		if ( seedId >= (int)numSeeds )
			return;

		// Get data such as the object space position and normal for this seed id.
		SeedData seedData = Seeds[seedId];
		uint3 voxel = PositionOsToTexel( seedData.PositionOs.xyz, dims );
		Cell cell = Cell::Load( voxel, dims );
		if ( !cell.IsValid() )
			return;
		
		if ( cell.IsSeed() )
		{
			float3 positionOs = TexelCenterToPositionOs( voxel, dims );
			SeedData previousSeedData = Seeds[cell.SeedId];
			float previousDist = distance( positionOs, cell.SeedPositionOs );
			float currentDist = distance( positionOs, seedData.PositionOs.xyz );
			if ( currentDist > previousDist )
				return;
		}
		cell.SeedId = seedId;
		cell.StoreSeedId();
	}

//------------------------------------------------------------------
// Stage 3, 3D (Voxels)
//------------------------------------------------------------------
	Cell Flood( uint3 voxel, float3 dims, Cell pCell, Cell qCell )
	{
		// The neighbor must be a seed cell within the range of the texture, or we won't use it.
		if ( !qCell.IsValid() || !qCell.IsSeed() )
			return pCell;

		float3 localPos = TexelCenterToPositionOs( voxel, dims );
		float pDist = distance( localPos, pCell.SeedPositionOs );
		float qDist = distance( localPos, qCell.SeedPositionOs );

		// If this voxel is already defined and given a seed cell that is
		// nearer than this neighbor, don't update this voxel at all.
		if ( pCell.SeedId >= 0 && pDist < qDist )
			return pCell;

		// We will use our neighbor's seed, as it is nearer to this voxel.
		pCell.SeedId = qCell.SeedId;
		pCell.SeedPositionOs = qCell.SeedPositionOs;

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
		return pCell;
	}

	void JumpFlood( uint3 voxel, float3 dims )
	{
		Cell pCell = Cell::Load( voxel, dims );
		// In case the the shader was somehow dispatched with the wrong number of threads.
		if ( !pCell.IsValid() )
			return;

		// Each voxel has an neighbor at:
		// - Eight directions on the current Z slice
		// - Nine directions, including the center cell, on the Z+1 and Z-1 slices
		Cell nCells[26];

		for ( int z = -1; z <= 1; z++ ) {
			for ( int y = -1; y <= 1; y++ ) {
				for ( int x = -1; x <= 1; x++ ) {

					int3 nOffset = int3( x, y, z );
					int3 qVoxel = voxel + nOffset * JumpStep;

					// Shift each component +1 to remap from -1 to 1 -> 0 to 2.
					nOffset += 1;

					// The new max value per component is now 3. So, when adding each
					// component to the index, the following offsets are also added.
					//  x - 0
					// 	y - 3^1 
					// 	z - 3^2
					int i = ( nOffset.z * 9 ) + ( nOffset.y * 3 ) + nOffset.x;

					// This is the index of the local cell (0,0,0), which would have its components 
					// shifted to (1,1,1), with an index of: ( 1 * 9 ) + ( 1 * 3 ) + 1
					int centerIndex = 13;

					// Skip adding the local cell.
					if ( i == centerIndex )
					{
						continue;
					}
					// Because we skipped adding the local cell, we must account 
					// for the gap by shifting all subsequent indices down by one.
					else if ( i > centerIndex )
					{
						i--;
					}

					nCells[i] = Cell::Load( qVoxel, dims );
		}}}

		for( int i = 0; i < 26; i++ )
		{
			pCell = Flood( voxel, dims, pCell, nCells[i] );
		}
		pCell.StoreSeedId();
		pCell.StoreTexture();
	}

//------------------------------------------------------------------
// Stage 4, 3D (Voxels)
//------------------------------------------------------------------
	void FinalizeOutput( uint3 voxel, uint3 dims )
	{
		Cell cell = Cell::Load( voxel, dims );
		if ( !cell.IsValid() )
			return;

		if ( cell.SignedDistance < 0 )
		{
			cell.SeedId *= -1;
		}
		cell.StoreSeedId();
	}

//------------------------------------------------------------------
// Stage 5, 3D (Voxels)
//------------------------------------------------------------------
	void DebugNormalized( uint3 voxel, uint3 dims )
	{
		bool useNormal = false;

		Cell cell = Cell::Load( voxel, dims );
		if ( !cell.IsValid() )
			return;

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
			FindSeedPoints( id.x, dims );
		#elif D_STAGE == 2
			InitializeSeedPoints( id.x, dims );
		#elif D_STAGE == 3
			JumpFlood( id, dims );
		#elif D_STAGE == 4
			FinalizeOutput( id, dims );
		#elif D_STAGE == 5
			DebugNormalized( id, dims );
		#endif
	}
}