MODES
{
	Default();
}


CS
{
	#include "system.fxc"
	#include "common.fxc"
	

	float3 Mins < Attribute( "Mins" ); >;
	float3 Maxs < Attribute( "Maxs" ); >;
	StructuredBuffer<float3> Vertices < Attribute("Vertices"); >;
	int VertexCount < Attribute( "VertexCount" ); >;
	StructuredBuffer<int> Indices < Attribute("Indices"); >;
	int IndexCount < Attribute( "IndexCount" ); >;
	RWTexture3D<float4> OutputTexture < Attribute( "OutputTexture" ); >;

	int JumpStep < Attribute( "JumpStep" ); >;

	float3 TriangleCentroid( float3 v0, float3 v1, float3 v2 )
	{
		return ( v0 + v1 + v2 ) / 3.0;
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

	float3 TexelToLocal( uint3 texel, float3 dims )
	{
		float3 normalized = float3(texel) / dims;
		return Mins + normalized * ( Maxs - Mins );
	}

	void Store( uint3 texel, float4 voxel )
	{
		OutputTexture[texel] = voxel;
	}
	
	float4 Load( uint3 texel )
	{
		return OutputTexture[texel];
	}

	void AddSeedPoints( uint3 DTid, float3 dims )
	{
		if ( any( DTid >= dims ) )
			return;

		float3 localPos = TexelToLocal( DTid, dims );
		
		// Treat any distance greater than the bounds of the mesh as undefined.
		float maxDistance = distance( Mins, Maxs ) + 1;
		Store( DTid, float4( 0, 0, 0, maxDistance ) );

		for( uint i = 0; i < IndexCount; i += 3 )
		{
			float3 v0 = Vertices[Indices[i]];
			float3 v1 = Vertices[Indices[i + 1]];
			float3 v2 = Vertices[Indices[i + 2]];

			// To avoid weird bias toward triangle edges, use triangle center instead
			// of finding the nearest point on the triangle.
			float3 triCenter = TriangleCentroid( v0, v1, v2 );

			float unsignedDist = distance( localPos, triCenter );
			// This triangle is too far away to make this voxel a seed point.
			if ( unsignedDist > 1 )
				continue;

			float sign = 1;
			// BAD ASSUMPTION: The center of the mesh is always on the inside.
			// This is a safe assumption for convex meshes, but not concave meshes.
			float3 v3 = ( Maxs - Mins ) * 0.5;
			float3 triOrient = TriangleOrientation( v0, v1, v2, v3 );
			if ( triOrient.z < 0 )
			{
				float3 temp = v1;
				v1 = v0;
				v0 = temp;
				// If inside the mesh, flip the signed distance.
				sign = -1;
			}
			Store( DTid, float4( triCenter, unsignedDist * sign ) );
			return;
		}
	}

	void Flood( uint3 DTid, uint3 neighbor, float3 dims )
	{
		// Any distance above maxSize indicates an undefined voxel.
		float maxSize = distance( Mins, Maxs );

		// Don't sample neighbors that are out of the range of the texture.
		if ( any( neighbor < 0 ) || any( neighbor >= dims ) )
			return;

		float4 neighborColor = Load( neighbor );

		// If the neighboring voxel is undefined, we can't get anything useful from it.
		if ( neighborColor.a > maxSize )
			return;
		
		float4 thisColor = Load( DTid );
		float3 localPos = TexelToLocal( DTid, dims );

		float3 thisPos = thisColor.rgb;
		float thisUd = abs( thisColor.a );
		float distToNeighborTri = distance( neighborColor.rgb, localPos );

		// If this voxel was undefined, or if this neighbor's triangle is closer,
		// this voxel should copy the position from the neighboring seed.
		if ( thisUd > maxSize || thisUd > distToNeighborTri )
		{
			thisPos = neighborColor.rgb;
			thisUd = distToNeighborTri;
		}
		else 
		{
			// This voxel remains unchanged from its previous value.
			return;
		}
		
		// If we've copied a triangle point from a neighbor, we should evaluate whether
		// our new distance should be positive or negative.
		//
		// BAD ASSUMPTION: If we are closer to the origin than we are
		// to the neighbor's triangle point, we are inside the mesh.
		if ( length( localPos ) < distToNeighborTri )
		{
			thisUd *= -1;
		}

		Store( DTid, float4( thisPos.rgb , thisUd ) );
	}

	void JumpFlood( uint3 DTid, float3 dims )
	{
		// Z = -1
		Flood( DTid, DTid + int3( -1, -1, -1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  0, -1, -1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  1, -1, -1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3( -1,  0, -1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  0,  0, -1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  1,  0, -1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3( -1,  1, -1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  0,  1, -1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  1,  1, -1 ) * JumpStep, dims );
		// Z = 0
		Flood( DTid, DTid + int3( -1, -1,  0 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  0, -1,  0 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  1, -1,  0 ) * JumpStep, dims );
		Flood( DTid, DTid + int3( -1,  0,  0 ) * JumpStep, dims ); 
		// Skip 0, 0, 0 since that's us!
		Flood( DTid, DTid + int3(  1,  0,  0 ) * JumpStep, dims );
		Flood( DTid, DTid + int3( -1,  1,  0 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  0,  1,  0 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  1,  1,  0 ) * JumpStep, dims );
		// Z = 1
		Flood( DTid, DTid + int3( -1, -1,  1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  0, -1,  1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  1, -1,  1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3( -1,  0,  1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  0,  0,  1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  1,  0,  1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3( -1,  1,  1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  0,  1,  1 ) * JumpStep, dims );
		Flood( DTid, DTid + int3(  1,  1,  1 ) * JumpStep, dims );
	}

	void DebugNormalized( uint3 DTid, uint3 dims )
	{
		float4 denormalized = Load( DTid );
		float3 pixelColor = denormalized.rgb;
		pixelColor -= Mins;
		pixelColor /= abs( Maxs - Mins );
		float sdf = denormalized.a;
		sdf /= distance( Maxs, Mins );
		OutputTexture[DTid] = float4( pixelColor.rgb, sdf );
	}

	DynamicCombo( D_STAGE, 0..2, Sys( All ) );

	[numthreads( 8, 8, 8 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		float3 dims;
		OutputTexture.GetDimensions( dims.x, dims.y, dims.z );

		#if D_STAGE == 0
			AddSeedPoints( id, dims );
		#elif D_STAGE == 1
			JumpFlood( id, dims );
		#elif D_STAGE == 2
			DebugNormalized( id, dims );
		#endif
	}
}