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


	void AddSeedPoints( uint3 DTid, float3 dims )
	{
		if ( any( DTid >= dims ) )
			return;

		// TODO: This is just for debug purposes. Delete it!
		float maxDim = max(dims.x, dims.y );
		maxDim = max( maxDim, dims.z );

		float3 localPos = TexelToLocal( DTid, dims );
		float minDist = 1e20;
		float3 closestPoint = float3( 0,0,0 );

		for( uint i = 0; i < IndexCount; i += 3 )
		{
			float3 v0 = Vertices[Indices[i]];
			float3 v1 = Vertices[Indices[i + 1]];
			float3 v2 = Vertices[Indices[i + 2]];

			float3 foundPoint = ClosestPointOnTriangle( v0, v1, v2, localPos );
			float dist = length( localPos - foundPoint );
			// TODO: This is for debugging purposes. Delete when done!
			dist *= 1 / maxDim;

			if ( dist < minDist )
			{
				minDist = dist;
				closestPoint = foundPoint;
			}
		}

		OutputTexture[DTid] = float4( closestPoint, minDist );
	}

	void JumpFlood( uint3 DTid, float3 dims )
	{
		if ( any( DTid >= dims ) )
			return;

		float4 current = OutputTexture[DTid];
		float minDist = current.w;
		float3 closestPoint = current.xyz;

		for( int z = -1; z <= 1; z++ )
		{
			for( int y = -1; y <= 1; y++ )
			{
				for( int x = -1; x <= 1; x++ )
				{
					int3 neighbor = int3( DTid ) + int3( x, y, z ) * JumpStep;

					if ( any( neighbor < 0 ) || any( neighbor >= dims ) )
						continue;

					// Find the triangle point that is tentatively closest to the neighboring texel.
					float3 neighborPoint = OutputTexture[neighbor].xyz;

					float3 localPos = TexelToLocal( DTid, dims );
					// Figure out how close we are to that neighbor's triangle.
					float dist = length( localPos - neighborPoint );

					// If that neighbor's triangle is closer than our own, we'll use their triangle.
					if ( dist < minDist )
					{
						minDist = dist;
						closestPoint = neighborPoint;
					}
				}
			}
		}

		OutputTexture[DTid] = float4( closestPoint, minDist );
	}

	DynamicCombo( D_STAGE, 0..1, Sys( All ) );

	[numthreads( 8, 8, 8 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		float3 dims;
		OutputTexture.GetDimensions( dims.x, dims.y, dims.z );

		#if D_STAGE == 0
			AddSeedPoints( id, dims );
		#elif D_STAGE == 1
			JumpFlood( id, dims );
		#endif
	}
}