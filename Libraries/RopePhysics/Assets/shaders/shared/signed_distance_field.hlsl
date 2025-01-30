SamplerState SdfSampler < Filter( Trilinear ); AddressU( Clamp ); AddressV( Clamp ); AddressW( Clamp ); >;

struct SignedDistanceField
{
	int SdfTextureIndex;
	int TextureSize;
	float BoundsSizeOs;
	float4x4 LocalToWorld;
	float4x4 WorldToLocal;

	float3 PositionOsToNormal( float3 positionOs )
	{
		return positionOs / BoundsSizeOs;
	}

	uint3 PositionOsToTexel( float3 positionOs )
	{
		return (uint3)( TextureSize * PositionOsToNormal( positionOs ) );
	}

	uint3 GetTexelSize()
	{
		return BoundsSizeOs / TextureSize;
	}

	float3 TexelToPositionOs( uint3 texel )
	{
		return (float3)texel / TextureSize * BoundsSizeOs;
	}

	float3 TexelCenterToPositionOs( uint3 texel )
	{
		return TexelToPositionOs( texel ) + GetTexelSize() * 0.5;
	}

	float GetSignedDistance( Texture3D sdf, float3 localPos )
	{
		localPos = PositionOsToNormal( localPos );
		float normalized = sdf.SampleLevel( SdfSampler, localPos, 0 ).a;
		float size = BoundsSizeOs;
		return normalized * size * 2 - size;
	}

	float3 GetGradient( Texture3D sdf, float3 localPos, out float signedDistance )
	{
		localPos = PositionOsToNormal( localPos );
		float4 texData = sdf.SampleLevel( SdfSampler, localPos, 0 );
		float normalized = texData.a;
		float size = BoundsSizeOs;
		signedDistance = normalized * size * 2 - size;
		float3 gradient = texData.rgb;
		gradient *= 2;
		gradient -= 1;
		return gradient;
	}
};