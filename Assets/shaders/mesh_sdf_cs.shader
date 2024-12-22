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

	uint3 LocalToTexel( float3 localPos, float3 dims )
	{
		float3 normalized = ( localPos - Mins ) / ( Maxs - Mins );
		return uint3( normalized * dims );
	}

	float3 TexelToLocal( uint3 texel, float3 dims )
	{
		float3 normalized = float3(texel) / dims;
		return Mins + normalized * ( Maxs - Mins );
	}

	float dot2( in float3 v ) { return dot(v,v); }

	// Copied from: https://www.shadertoy.com/view/ttfGWl
	float3 ClosestPointOnTriangle( float3 v0, float3 v1, float3 v2, float3 p )
	{
		    float3 v10 = v1 - v0; float3 p0 = p - v0;
			float3 v21 = v2 - v1; float3 p1 = p - v1;
			float3 v02 = v0 - v2; float3 p2 = p - v2;
			float3 nor = cross( v10, v02 );

			if( dot(cross(v10,nor),p0)<0.0 ) return v0 + v10*clamp( dot(p0,v10)/dot2(v10), 0.0, 1.0 );
    		if( dot(cross(v21,nor),p1)<0.0 ) return v1 + v21*clamp( dot(p1,v21)/dot2(v21), 0.0, 1.0 );
    		if( dot(cross(v02,nor),p2)<0.0 ) return v2 + v02*clamp( dot(p2,v02)/dot2(v02), 0.0, 1.0 );
    		return p - nor*dot(nor,p0)/dot2(nor);
	}

	float3 TriangleCentroid( float3 v0, float3 v1, float3 v2 )
	{
		return ( v0 + v1 + v2 ) / 3.0;
	}

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

	float4 Load( uint3 texel )
	{
		float4 voxel = OutputTexture[texel];
		float3 pos = voxel.rgb;
		pos *= Maxs - Mins;
		pos += Mins;
		float dist = voxel.a;
		dist *= distance( Mins, Maxs );
		return float4( pos.rgb, dist );
	}

	void Store( uint3 texel, float4 voxel )
	{
		float3 pos = voxel.rgb;
		pos -= Mins;
		pos /= Maxs - Mins;
		float dist = voxel.a;
		dist /= distance( Mins, Maxs );
		OutputTexture[texel] = float4( pos.rgb, dist );
	}

	void AddSeedPoints( uint3 DTid, float3 dims )
	{
		if ( any( DTid >= dims ) )
			return;

		float3 localPos = TexelToLocal( DTid, dims );
		
		// Treat any distance greater than the bounds of the mesh as undefined.
		Store( DTid, float4( 0, 0, 0, distance( Mins, Maxs ) + 1 ) );

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
		if ( neighborColor.a < maxSize )
			return;
		
		float4 thisColor = Load( DTid );
		float3 localPos = TexelToLocal( DTid, dims );
		float localDistToTri = distance( neighborColor.rgb, localPos );

		bool hasCopiedNeighbor = false;

		// If this voxel is undefined, it should copy the first neighboring seed.
		if ( thisColor.a > maxSize )
		{
			thisColor.rgb = neighborColor.rgb;
			thisColor.a = localDistToTri;
			hasCopiedNeighbor = true;
		}
		// If this voxel is now a seed point (i.e. its distance is defined), 
		// its triangle point will only change if a neighbor offers a closer point.
		else if ( localDistToTri < thisColor.a )
		{
			thisColor.rgb = neighborColor.rgb;
			thisColor.a = localDistToTri;
			hasCopiedNeighbor = true;
		}
		else 
		{
			// This voxel remains unchanged from its previous value.
			return;
		}
		
		// If we've copied a triangle point from a neighbor, we should evaluate whether
		// our new distance should be positive or negative.
		if ( hasCopiedNeighbor )
		{
			// BAD ASSUMPTION: If we are closer to the origin than we are
			// to the neighbor's triangle point, we are inside the mesh.
			bool localIsInside = length( localPos ) < length( neighborColor.rgb );

			// Choose the appropriate sign for the distance to the mesh depending on
			// whether we estimate that this position is on the inside or outside.
			if ( localIsInside && sign( thisColor.a) > 0 )
			{
				thisColor.a *= -1;
			}
			else if ( !localIsInside && sign( thisColor.a ) < 0 )
			{
				thisColor.a *= -1;
			}
		}

		Store( DTid, thisColor );
	}

	void JumpFlood( uint3 DTid, float3 dims )
	{
		if ( any( DTid >= dims ) )
			return;

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

	void DebugDenormalize( uint3 DTid, uint3 dims )
	{
		float4 denormalized = Load( DTid );
		denormalized.a = 1;
		OutputTexture[DTid] = denormalized;
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
			DebugDenormalize( id, dims );
		#endif
	}
}