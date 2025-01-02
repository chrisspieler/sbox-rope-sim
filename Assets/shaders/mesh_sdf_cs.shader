MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"

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
// ATTRIBUTES
//==================================================================

	StructuredBuffer<float4> Vertices < Attribute("Vertices"); >;
	StructuredBuffer<uint> Indices < Attribute("Indices"); >;
	RWStructuredBuffer<SeedData> Seeds < Attribute( "Seeds" ); >;
	RWStructuredBuffer<int> VoxelSeeds < Attribute( "VoxelSeeds" ); >;
	int JumpStep < Attribute( "JumpStep" ); >;

//==================================================================
// GLOBALS
//==================================================================



//==================================================================
// CLASSES
//==================================================================

	struct Triangle
	{
		float3 V0;
		float3 V1;
		float3 V2;
		float3 Normal;

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

		void Store( int seedId )
		{
			int i = Triangle::IndexSeedToTriangleCenter( seedId );
			float3 center = CalculateCenter( V0, V1, V2 );
			Seeds[i] = SeedData::From( center, Normal );
			Seeds[i + 1] = SeedData::From( V0, Normal );
			Seeds[i + 2] = SeedData::From( V1, Normal );
			Seeds[i + 3] = SeedData::From( V2, Normal );
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
			int i = Voxel::Index3DTo1D( voxel );
			VoxelSeeds[i] = -1;
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
			int i = Voxel::Index3DTo1D( Voxel );
			int iOut;
			InterlockedExchange( VoxelSeeds[i], SeedId, iOut );
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
// Stage 1, 1D (Triangles)
//------------------------------------------------------------------
	void FindSeedPoints( int triId )
	{
		// Each triangle is represented by three indices.
		uint indexId = triId * 3;

		uint numIndices, indexStride;
		Indices.GetDimensions( numIndices, indexStride );
		if ( indexId >= numIndices )
			return;
		
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

		Triangle tri = Triangle::From( v0, v1, v2 );

		// Calculate triangle centroid and normal.
		float3 triNormal = tri.Normal;

		// Each triangle has four seeds - the center, and slightly inside of each vertex.
		int sIndex = triId * 4;
		tri.Store( sIndex );
	}

//------------------------------------------------------------------
// Stage 2, 1D (Seeds)
//------------------------------------------------------------------
	void InitializeSeedPoints( int seedId )
	{
		uint numSeeds, seedStride;
		Seeds.GetDimensions( numSeeds, seedStride );
		if ( seedId >= (int)numSeeds )
			return;

		Triangle tri = Triangle::FromSeed( seedId );
		if ( !tri.IsInBounds() )
			return;

		// Get data such as the object space position and normal for this seed id.
		SeedData seedData = Seeds[seedId];
		uint3 voxel = Voxel::FromPosition( seedData.PositionOs.xyz );
		Cell cell = Cell::Load( voxel );
		if ( !cell.IsValid() )
			return;
		
		if ( cell.IsSeed() )
		{
			float3 positionOs = Voxel::GetCenterPositionOs( voxel );
			SeedData previousSeedData = Seeds[cell.SeedId];
			float previousDist = distance( positionOs, cell.SeedPositionOs );
			float currentDist = distance( positionOs, seedData.PositionOs.xyz );
			if ( currentDist > previousDist )
				return;
		}
		cell.SeedId = seedId;
		cell.StoreData();
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

	Cell FixLeak( uint3 voxel, Cell pCell, Cell qCell)
	{
		// Heuristic to plug leaks that might escape from janky geometry with weird normals.
		// We assume that once the number of jump steps is 1, every neighbor will have difference
		// in signed distance of less than two, otherwise some sort of error has occurred.
		if ( abs( pCell.SignedDistance - qCell.SignedDistance ) > 2 )
			pCell.SignedDistance = abs(pCell.SignedDistance );
			
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

					nCells[i] = Cell::Load( qVoxel );
		}}}

		for( int i = 0; i < 26; i++ )
		{
			pCell = Flood( voxel, pCell, nCells[i] );
		}
		pCell.StoreData();
		
		if ( JumpStep > 1 )
			return;

		for ( int j = 0; j < 26; j++ )
		{
			pCell = FixLeak( voxel, pCell, nCells[j] );
		}
		pCell.StoreData();
	}

//------------------------------------------------------------------
// Stage 4, 3D (Voxels)
//------------------------------------------------------------------
	void Compress( uint3 voxel )
	{
		Voxel::Compress( voxel );
	}

//==================================================================
// MAIN
//==================================================================

	DynamicCombo( D_STAGE, 0..4, Sys( All ) );

	#if D_STAGE == 1 || D_STAGE == 2
	[numthreads( 16, 1, 1 )]
	#elif D_STAGE == 4
	[numthreads( 4, 8, 8 )]
	#else
	[numthreads( 8, 8, 8 )]
	#endif
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		#if D_STAGE == 0
			InitializeVolume( id );
		#elif D_STAGE == 1
			FindSeedPoints( id.x );
		#elif D_STAGE == 2
			InitializeSeedPoints( id.x );
		#elif D_STAGE == 3
			JumpFlood( id );
		#elif D_STAGE == 4
			Compress( id );
		#endif
	}
}