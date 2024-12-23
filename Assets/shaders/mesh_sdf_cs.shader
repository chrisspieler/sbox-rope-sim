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

	float3 TriangleSurfaceNormal( float3 v0, float3 v1, float3 v2 )
	{
		float3 u = v1 - v0;
		float3 v = v2 - v0;
		return normalize( cross( u, v ) );
	}

	float dot2( in float3 v ) { return dot(v,v); }

	// Copied from: https://www.shadertoy.com/view/ttfGWl
	float3 TriangleClosestPoint( float3 v0, float3 v1, float3 v2, float3 p )
	{
		    float3 v10 = v1 - v0; float3 p0 = p - v0;
			float3 v21 = v2 - v1; float3 p1 = p - v1;
			float3 v02 = v0 - v2; float3 p2 = p - v2;
			float3 nor = cross( v10, v02 );

			float3  q = cross( nor, p0 );
			float d = 1.0/dot2(nor);
			float u = d*dot( q, v02 );
			float v = d*dot( q, v10 );
			float w = 1.0-u-v;
			
				 if( u<0.0 ) { w = clamp( dot(p2,v02)/dot2(v02), 0.0, 1.0 ); u = 0.0; v = 1.0-w; }
			else if( v<0.0 ) { u = clamp( dot(p0,v10)/dot2(v10), 0.0, 1.0 ); v = 0.0; w = 1.0-u; }
			else if( w<0.0 ) { v = clamp( dot(p1,v21)/dot2(v21), 0.0, 1.0 ); w = 0.0; u = 1.0-v; }
			
			return u*v1 + v*v2 + w*v0;
	}

	// Copied from embree: https://github.com/RenderKit/embree/blob/master/tutorials/common/math/closest_point.h
	float3 TriangleClosestPoint2( float3 v0, float3 v1, float3 v2, float3 p )
	{
		float3 v01 = v1 - v0;
		float3 v02 = v2 - v0;
		float3 v0p = p - v0;

		float d1 = dot( v01, v0p );
		float d2 = dot( v02, v0p );
		if ( d1 <= 0.0 && d2 <= 0.0 ) return v0;

		float3 v1p = p - v1;
		float d3 = dot( v01, v1p );
		float d4 = dot( v02, v1p );
		if ( d3 >= 0.0 && d4 <= d3 ) return v1;

		float3 v2p = p - v2;
		float d5 = dot( v01, v2p );
		float d6 = dot( v02, v2p );
		if ( d6 >= 0.0 && d5 <= d6 ) return v2;

		float vc = d1 * d4 - d3 * d2;
		if ( vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0 )
		{
			float v = d1 / ( d1 - d3 );
			return v0 + v * v01;
		}

		float vb = d5 * d2 - d1 * d6;
		if ( vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0 )
		{
			float v = d2 / ( d2 - d6 );
			return v0 + v * v02;
		}

		float va = d3 * d6 - d5 * d4;
		if ( va <= 0.0 && ( d4 - d3 ) >= 0.0 && ( d5 - d6 ) >= 0.0 )
		{
			float v = ( d4 - d3 )  / ( ( d4 - d3 ) + ( d5 - d6 ) );
			return v1 + v * ( v2 - v1 );
		}

		float denom = 1.0 / (va + vb + vc);
		float v = vb * denom;
		float w = vc * denom;
		return v0 + v * v01 + w * v02;
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


	float3 TexelToPositionOs( uint3 texel, float3 dims )
	{
		float3 normalized = float3(texel) / dims;
		return Mins + normalized * ( Maxs - Mins );
	}

	uint3 PositionOsToTexel( float3 pos, float3 dims )
	{
		float3 normalized = ( pos - Mins ) / ( Maxs - Mins );
		return uint3(dims) * normalized;
	}

	void Store( uint3 texel, float4 voxel )
	{
		OutputTexture[texel] = voxel;
	}
	
	float4 Load( uint3 texel )
	{
		return OutputTexture[texel];
	}

	void InitializeVolume( uint3 DTid, float3 dims )
	{
		if ( any( DTid >= dims ) )
			return;

		float maxDistance = distance( Mins, Maxs ) + 1;
		Store( DTid, float4( 0, 0, 0, maxDistance ) );
	}

	void AddSeedPoints( uint3 DTid, float3 dims )
	{
		int i = DTid.x;
		float3 v0 = Vertices[Indices[i]];
		float3 v1 = Vertices[Indices[i + 1]];
		float3 v2 = Vertices[Indices[i + 2]];

		float3 triCenter = ( v0 + v1 + v2 ) / 3.0;
		float3 surfaceNormal = triCenter + TriangleSurfaceNormal( v0, v1, v2 );
		float3 triOrient = TriangleOrientation( v0, v1, v2, surfaceNormal );
		float sign = 1;

		if ( triOrient.z < 0 )
		{
			float3 temp = v1;
			v1 = v0;
			v0 = temp;
			// If inside the mesh, flip the signed distance.
			sign = -1;
		}

		uint3 texel = PositionOsToTexel( triCenter, dims );
		Store( texel, float4( triCenter, 0 ) );
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
		float3 localPos = TexelToPositionOs( DTid, dims );

		float thisUd = abs( thisColor.a );
		float distToNeighborTri = distance( neighborColor.rgb, localPos );

		// If this voxel is already defined and given a seed cell that is
		// nearer than this neighbor, don't update this voxel at all.
		if ( thisUd < maxSize && thisUd < distToNeighborTri )
			return;

		// If we've copied a triangle point from a neighbor, we should evaluate whether
		// our new distance should be positive or negative.
		//
		// BAD ASSUMPTION: If we are closer to the origin than we are
		// to the neighbor's triangle point, we are inside the mesh.
		float sign = 1;
		if ( length( localPos ) < length( distToNeighborTri ) )
		{
			sign = -1;
		}

		Store( DTid, float4( neighborColor.rgb, distToNeighborTri * sign ) );
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

	DynamicCombo( D_STAGE, 0..3, Sys( All ) );

	#if D_STAGE == 1
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
			AddSeedPoints( id, dims );
		#elif D_STAGE == 2
			JumpFlood( id, dims );
		#elif D_STAGE == 3
			DebugNormalized( id, dims );
		#endif
	}
}