struct SignedDistanceField
{
	int SdfTextureIndex;
	int TextureSize;
	float BoundsSizeOs;
	float4x4 LocalToWorld;
	float4x4 WorldToLocal;

	uint3 PositionOsToTexel( float3 positionOs )
	{
		float3 normalized = positionOs / BoundsSizeOs;
		return (uint3)( TextureSize * normalized );
	}

	float GetSignedDistance( Texture3D sdf, uint3 texel )
	{
		float normalized = sdf.Load( int4( texel.xyz, 0 ) ).a;
		float size = BoundsSizeOs;
		return normalized * size * 2 - size;
	}

	float3 GetGradient( Texture3D sdf, uint3 texel, out float signedDistance )
	{
		if ( any( texel < 0 ) || any( texel > TextureSize ) )
		{
			signedDistance = 0;
			return 0;
		}

		float4 texData = sdf.Load( int4( texel.xyz, 0 ) );
		float normalized = texData.a;
		float size = BoundsSizeOs;
		signedDistance = normalized * size * 2 - size;
		float3 gradient = texData.rgb;
		gradient *= 2;
		gradient -= 1;
		if ( dot( gradient, gradient ) < 0.001 )
		{
			gradient = 0;
		}
		return gradient;
	}
};