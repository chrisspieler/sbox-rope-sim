MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"

	#include "shared/index.hlsl"
	#include "shared/voxel_sdf.hlsl"

//==================================================================
// DTO
//==================================================================

	struct SeedData
	{
		// w components go unused for now, but could be used later.
		float4 PositionOs;
		float4 Normal;

		static SeedData From( float3 positionOs, float3 normalOs )
		{
			SeedData data;
			data.PositionOs = float4( positionOs.xyz, 0 );
			data.Normal = float4( normalOs.xyz, 0 );
			return data;
		}
	};

//==================================================================
// GLOBALS
//==================================================================

//------------------------------------------------------------------
// Attributes
//------------------------------------------------------------------

	StructuredBuffer<float4> Vertices < Attribute("Vertices"); >;
	StructuredBuffer<uint> Indices < Attribute("Indices"); >;
	RWStructuredBuffer<SeedData> Seeds < Attribute( "Seeds" ); >;
	int NumEmptySeeds < Attribute( "NumEmptySeeds" ); >;
	RWStructuredBuffer<int> SeedVoxels < Attribute( "SeedVoxels" ); >;
	RWStructuredBuffer<int> VoxelSeeds < Attribute( "VoxelSeeds" ); >;
	int JumpStep < Attribute( "JumpStep" ); >;

//------------------------------------------------------------------
// Functions
//------------------------------------------------------------------

	int GetMeshIndexCount()
	{
		uint numIndices, indexStride;
		Indices.GetDimensions( numIndices, indexStride );
		return numIndices;
	}

	int GetMeshTriangleCount()
	{
		return GetMeshIndexCount() / 3;
	}

	int GetSeedCount()
	{
		uint numSeeds, seedStride;
		Seeds.GetDimensions( numSeeds, seedStride );
		return numSeeds;
	}

	void StoreVoxelSeedId( uint3 voxel, int seedId )
	{
		int i = Voxel::Index3DTo1D( voxel );
		int iOut;
		InterlockedExchange( VoxelSeeds[i], seedId, iOut );
	}

	void StoreSeedVoxelId( int seedId, uint3 voxel )
	{
		int voxelId = Voxel::Index3DTo1D( voxel );
		int iOut;
		InterlockedExchange( SeedVoxels[seedId], voxelId, iOut );
	}

	void StoreSeedInvalidVoxel( int seedId )
	{
		int iOut;
		InterlockedExchange( SeedVoxels[seedId], -1, iOut );
	}

	void StoreSeedVoxelId( int seedId, float3 positionOs )
	{
		uint3 voxel = Voxel::FromPositionClamped( positionOs );
		StoreSeedVoxelId( seedId, voxel );
	}

	bool TryLoadSeedVoxel( int seedId, out uint3 voxel )
	{
		int i = SeedVoxels[seedId];
		if ( i < 0 )
			return false;

		voxel = Voxel::Index1DTo3D( i );
		return true;
	}

//==================================================================
// CLASSES
//==================================================================

	struct Triangle
	{
		float3 V0;
		float3 V1;
		float3 V2;
		float3 Normal;
		float3 Center;

		static float3 CalculateNormal( float3 v0, float3 v1, float3 v2 )
		{
			float3 u = v1 - v0;
			float3 v = v2 - v0;
			return normalize( cross( u, v ) );
		}

		static float3 CalculateCenter( float3 v0, float3 v1, float3 v2 )
		{
			return ( v0 + v1 + v2 ) / 3.0;
		}

		static Triangle From( float3 v0, float3 v1, float3 v2 )
		{
			Triangle tri;
			tri.V0 = v0;
			tri.V1 = v1;
			tri.V2 = v2;
			tri.Normal = Triangle::CalculateNormal( v0, v1, v2 );
			tri.Center = Triangle::CalculateCenter( v0, v1, v2 );
			return tri;
		}

		static int IndexSeedToTriangleCenter( int seedId )
		{
			if ( seedId <= 0 )
				return 0;

			return seedId - seedId % 4;
		}

		static Triangle FromSeed( int seedId )
		{
			int i = Triangle::IndexSeedToTriangleCenter( seedId );

			float3 v0 = Seeds[i + 1].PositionOs.xyz;
			float3 v1 = Seeds[i + 2].PositionOs.xyz;
			float3 v2 = Seeds[i + 3].PositionOs.xyz;
			return Triangle::From( v0, v1, v2 );
		}

		static Triangle FromIndex( int indexId )
		{
			// Fetch the indices that comprise the triangle.
			uint i0 = Indices[indexId];
			uint i1 = Indices[indexId + 1]; 
			uint i2 = Indices[indexId + 2];
			// Fetch the vertices referred to by each index.
			float3 v0 = Vertices[i0].xyz;
			float3 v1 = Vertices[i1].xyz;
			float3 v2 = Vertices[i2].xyz;

			float3 triCenter = Triangle::CalculateCenter( v0, v1, v2 );
			// Nudge the seed point of each vertex slightly inward.
			// This avoids any ambiguity about which triangle is closest.
			v0 += ( triCenter - v0 ) * 0.01;
			v1 += ( triCenter - v1 ) * 0.01;
			v2 += ( triCenter - v2 ) * 0.01;

			return Triangle::From( v0, v1, v2 );
		}

		void Store( int seedId )
		{
			float3 center = CalculateCenter( V0, V1, V2 );

			int i = Triangle::IndexSeedToTriangleCenter( seedId );
			Seeds[i] = SeedData::From( center, Normal );
			StoreSeedVoxelId( i, center );

			i++;
			Seeds[i] = SeedData::From( V0, Normal );
			StoreSeedVoxelId( i, V0 );

			i++;
			Seeds[i] = SeedData::From( V1, Normal );
			StoreSeedVoxelId( i, V1 );

			i++;
			Seeds[i] = SeedData::From( V2, Normal );
			StoreSeedVoxelId( i, V2 );
		}

		// Copied from: https://www.shadertoy.com/view/ttfGWl
		float3 GetClosestPoint( float3 p )
		{
				float3 v10 = V1 - V0; float3 p0 = p - V0;
				float3 v21 = V2 - V1; float3 p1 = p - V1;
				float3 v02 = V0 - V2; float3 p2 = p - V2;
				float3 nor = cross( v10, v02 );

				float3  q = cross( nor, p0 );
				float d = 1.0/dot(nor,nor);
				float u = d*dot( q, v02 );
				float v = d*dot( q, v10 );
				float w = 1.0-u-v;
				
					if( u<0.0 ) { w = clamp( dot(p2,v02)/dot(v02,v02), 0.0, 1.0 ); u = 0.0; v = 1.0-w; }
				else if( v<0.0 ) { u = clamp( dot(p0,v10)/dot(v10, v10), 0.0, 1.0 ); v = 0.0; w = 1.0-u; }
				else if( w<0.0 ) { v = clamp( dot(p1,v21)/dot(v21, v21), 0.0, 1.0 ); w = 0.0; u = 1.0-v; }
				
				return u*V1 + v*V2 + w*V0;
		}

		float GetDistanceToPoint( float3 localPos )
		{
			float3 closestPoint = GetClosestPoint( localPos );
			return distance( localPos, closestPoint );
		}

		// This should be called sparingly, for obvious reasons.
		// TODO: Store triangles in a k-d tree on the CPU and perform the lookup there before dispatching the shader.
		static Triangle GetNearestTriangle( uint3 voxel )
		{
			uint numIndices = GetMeshIndexCount();
			float3 voxelPos = Voxel::GetCenterPositionOs( voxel );

			Triangle closestTri = Triangle::FromIndex( 0 );
			float minDistance = closestTri.GetDistanceToPoint( voxelPos );
			for( int i = 3; i < numIndices; i += 3 )
			{
				Triangle tri = Triangle::FromIndex( i );
				float tDist = tri.GetDistanceToPoint( voxelPos );
				if ( tDist < minDistance )
				{
					closestTri = tri;
					minDistance = tDist;
				}
			}
			return closestTri;
		}

		bool IsInBounds()
		{
			int trisInBounds = 0;
			if ( all( V0 >= ( VoxelMinsOs ) ) && all( V0 <= ( VoxelMaxsOs  ) ) )
				trisInBounds++;
			if ( all( V1 >= ( VoxelMinsOs ) ) && all( V1 <= ( VoxelMaxsOs  ) ) )
				trisInBounds++;
			if ( all( V2 >= ( VoxelMinsOs ) ) && all( V2 <= ( VoxelMaxsOs  ) ) )
				trisInBounds++;
			
			return trisInBounds >= 1;
		}
	};

	struct Cell
	{
		uint3 Voxel;

		float SignedDistance;

		int SeedId;
		float3 SeedPositionOs;
		float3 SeedNormalOs;

		static void Initialize( uint3 voxel )
		{
			StoreVoxelSeedId( voxel, -1 );
			Voxel::Store( voxel, 1e5 );
		}

		bool IsValid()
		{
			return Voxel::IsInVolume( Voxel );
		}

		bool IsSeed()
		{
			return !( SeedId == -1 && SignedDistance < 0 );
		}

		static Cell Load( uint3 voxel )
		{
			Cell cell;
			cell.Voxel = voxel;
			if ( !cell.IsValid() )
				return cell;

			cell.SignedDistance = Voxel::Load( cell.Voxel );
			int i = Voxel::Index3DTo1D( cell.Voxel );
			cell.SeedId = VoxelSeeds[i];
			
			if ( !cell.IsSeed() )
				return cell;

			SeedData seed = Seeds[abs( cell.SeedId )];
			cell.SeedPositionOs = seed.PositionOs.xyz;
			cell.SeedNormalOs = seed.Normal.xyz;
			return cell;
		}

		void StoreData()
		{
			StoreVoxelSeedId( Voxel, SeedId );
			Voxel::Store( Voxel, SignedDistance );
		}
	};

//==================================================================
// STAGES
//==================================================================

//------------------------------------------------------------------
// Stage 0, 3D (Voxels)
//------------------------------------------------------------------

	void InitializeVolume( uint3 voxel )
	{
		if ( !Voxel::IsInVolume( voxel ) )
			return;
		
		Cell::Initialize( voxel );
	}

//------------------------------------------------------------------
// Stage 1, 1D (Triangles + Empty Seeds)
//------------------------------------------------------------------

	// Assume emptySeedCount is a power of 2.
	void AddEmptySeed( int emptySeedId, int emptySeedOffset )
	{
		// Balance the seeds between octants.
		int seedsPerDim = NumEmptySeeds / 4;
		// Layout of seeds in each dimension, seed positions marked with parentheses.
		// iStep will be dims / seedsPerDim
		// 2 in 8 is 4  : 0 (2) (6) 8
		// 4 in 8 is 2  : 0 (1) (3) (5) (7) 8
		// 2 in 16 is 8 : 0 (4) (12) 16
		// 4 in 16 is 4	: 0 (2) (6) (10) (14) 16
		// 8 in 16 is 2 : 0 (1) (3) (5) (7) (9) (11) (13) (15) 16
		// 4 in 32 is 8 : 0 (4) (12) (20) (28) 32
		// 8 in 32 is 4 : 0 (4) (8) (12) (16) (20) (24) (28) 32
		int3 iStep = VoxelVolumeDims / seedsPerDim;
		
		// As shown by the above table, the starting index is always iStep / 2
		int3 startIdx = iStep / 2;
		// Find the index of each of the three dimensions that is represented by the seed index.
		uint3 seed3dIdx = Convert1DIndexTo3D( emptySeedOffset, (uint3)seedsPerDim );
		// Calculate how far away we've stepped from 0,0,0
		int3 offsetIdx = seed3dIdx * iStep;

		// Get a voxel that corresponds to the empty seed index.
		// Note that what we've done is equivalent to a nested for-loop, just spread between threads.
		uint3 voxel = startIdx + offsetIdx;

		// TODO: Store the triangles in a k-d tree on the CPU and manually set the seed values from there.
		Triangle tri = Triangle::GetNearestTriangle( voxel );
		SeedData data;
		data.PositionOs = float4( tri.Center, emptySeedId );
		data.Normal = float4( tri.Normal, emptySeedOffset );
		Seeds[emptySeedId] = data;
		StoreSeedVoxelId( emptySeedId, voxel );
	}

	void StoreTriangleSeedsFromIndices( int triId )
	{
		// Each triangle is represented by three indices.
		uint indexId = triId * 3;

		uint numIndices = GetMeshIndexCount();
		if ( indexId >= numIndices )
			return;
		
		Triangle tri = Triangle::FromIndex( indexId );

		// Each triangle has four seeds - the center, and slightly inside of each vertex.
		int sIndex = triId * 4;
		tri.Store( sIndex );
	}

	void FindSeedPoints( int DTid )
	{
		int numTris = GetMeshTriangleCount();
		if ( DTid < numTris )
		{
			// This thread corresponds to a triangle, so find that triangle.
			StoreTriangleSeedsFromIndices( DTid );
			return;
		}
		
		int maxDTid = ( numTris - 1 ) + ( NumEmptySeeds );
		if ( DTid <= maxDTid )
		{
			// This thread corresponds to an empty seed.
			int emptySeedOffset = maxDTid - DTid;
			// Account for the fact that all preceeding DTids would output 4 seeds each.
			int baseOutputSeedId = numTris * 4;
			int emptySeedId = baseOutputSeedId + emptySeedOffset;
			AddEmptySeed( emptySeedId, emptySeedOffset );
			return;
		}

		// The thread was out of range, so do nothing.
	}

//------------------------------------------------------------------
// Stage 2, 3D (Voxels)
//------------------------------------------------------------------

	void InitializeSeedPoints( uint3 voxel )
	{
		if ( !Voxel::IsInVolume( voxel ) )
			return;

		float3 voxelPos = Voxel::GetCenterPositionOs( voxel );

		SeedData closestSeed;
		int closestSeedId = -1;
		float closestDistance = 1e5;

		int numSeeds = GetSeedCount();
		for ( int i = 0; i < numSeeds; i++ )
		{
			uint3 seedVoxel = 0;
			if ( !TryLoadSeedVoxel( i, seedVoxel ))
				continue;

			if ( any( seedVoxel != voxel ) )
				continue;

			// Triangle tri = Triangle::FromSeed( i );
			SeedData seedData = Seeds[i];
			float seedDistance = distance( voxelPos, seedData.PositionOs.xyz );
			if ( seedDistance < closestDistance )
			{
				closestSeed = seedData;
				closestSeedId = i;
				closestDistance = seedDistance;
			}
		}

		if ( closestSeedId > -1 )
		{
			float3 dirToSeed = normalize( closestSeed.PositionOs.xyz - voxelPos );
			// Detect whether we are likely inside the mesh, assuming for now that this triangle is the closest.
			if ( dot( dirToSeed, closestSeed.Normal.xyz ) > 0 )
			{
				closestDistance *= -1;
			}
		}

		StoreVoxelSeedId( voxel, closestSeedId );
		Voxel::Store( voxel, closestDistance );
	}

//------------------------------------------------------------------
// Stage 3, 3D (Voxels)
//------------------------------------------------------------------
	Cell Flood( uint3 voxel, Cell pCell, Cell qCell )
	{
		// The neighbor must be a seed cell within the range of the texture, or we won't use it.
		if ( !qCell.IsValid() || !qCell.IsSeed() )
			return pCell;

		float3 localPos = Voxel::GetCenterPositionOs( voxel );

		Triangle pTri = Triangle::FromSeed( pCell.SeedId );
		float3 pClosest = pTri.GetClosestPoint( localPos );
		float pDist =  distance( localPos, pClosest );

		Triangle qTri = Triangle::FromSeed( qCell.SeedId );
		float3 qClosest = qTri.GetClosestPoint( localPos );
		float qDist = distance( localPos, qClosest );

		// If this voxel is already defined and given a seed cell that is
		// nearer than this neighbor, don't update this voxel at all.
		if ( pCell.SeedId >= 0 && pDist < qDist )
			return pCell;

		// We will use our neighbor's seed, as it is nearer to this voxel.
		pCell.SeedId = qCell.SeedId;
		pCell.SeedPositionOs = qClosest;
		pCell.SeedNormalOs = qTri.Normal;

		// If we've copied a triangle point from a neighbor, we should evaluate whether
		// our new distance should be positive or negative.
		float sign = 1;

		float3 qToLocalDir = normalize( localPos - qClosest );
		bool qFacesAway = dot( qToLocalDir, qTri.Normal ) < 0;
		if ( qFacesAway )
		{
			sign = -1;
		}

		pCell.SignedDistance = qDist * sign;
		return pCell;
	}

	void JumpFlood( uint3 voxel )
	{
		Cell pCell = Cell::Load( voxel );
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

					// The possible number of values per component is 3. So, when adding each
					// component to the index, the following offsets are also added.
					//  x - 0
					// 	y - 3^1 
					// 	z - 3^2
					int i = ( nOffset.z * 9 ) + ( nOffset.y * 3 ) + nOffset.x;

					// This is the index of the local cell (0,0,0), which would have its components 
					// shifted to (1,1,1), with an index of: ( 1 * 9 ) + ( 1 * 3 ) + 1
					int centerIndex = 13;

					int neighborIndex = i;
					// Skip adding the local cell.
					if ( i == centerIndex )
					{
						continue;
					}
					// Because we skipped adding the local cell, we must account 
					// for the gap by shifting all subsequent indices down by one.
					else if ( i > centerIndex )
					{
						neighborIndex = i - 1;
					}

					nCells[neighborIndex] = Cell::Load( qVoxel );
		}}}

		for( int i = 0; i < 26; i++ )
		{
			pCell = Flood( voxel, pCell, nCells[i] );
		}

		if ( !pCell.IsSeed() )
		{
			pCell.SignedDistance = 1e5;
		}
		pCell.StoreData();
	}

//------------------------------------------------------------------
// Stage 4, 3D (Voxels)
//------------------------------------------------------------------
	void Compress( uint3 voxel )
	{
		voxel.x *= 4;
		Voxel::Compress( voxel );
	}

//==================================================================
// MAIN
//==================================================================

	DynamicCombo( D_STAGE, 0..4, Sys( All ) );

	#if D_STAGE == 1
	[numthreads( 64, 1, 1 )]
	#elif D_STAGE == 4
	[numthreads( 2, 8, 8 )]
	#else 
	[numthreads( 4, 4, 4 )]
	#endif
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		#if D_STAGE == 0
			InitializeVolume( id );
		#elif D_STAGE == 1
			FindSeedPoints( id.x );
		#elif D_STAGE == 2
			InitializeSeedPoints( id );
		#elif D_STAGE == 3
			JumpFlood( id );
		#elif D_STAGE == 4
			Compress( id );
		#endif
	}
}