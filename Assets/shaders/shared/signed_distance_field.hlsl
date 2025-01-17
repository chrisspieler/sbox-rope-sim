struct SignedDistanceField
{
	float3 MinsWs;
	int SdfTextureIndex;
	float3 MaxsWs;
	int TextureSize;
	float4x4 LocalToWorld;
	float4x4 WorldToLocal;

	float3 GetBoundsSize()
	{
		return MaxsWs - MinsWs;
	}

	uint3 PositionOsToTexel( float3 positionOs )
	{
		float3 normalized = ( positionOs - MinsWs ) / GetBoundsSize();
		return (uint3)( normalized * ( TextureSize - 1 ) );
	}

	float GetSignedDistance( Texture3D sdf, uint3 texel )
	{
		float normalized = sdf.Load( int4( texel.xyz, 0 ) ).a;
		float size = GetBoundsSize().x;
		return normalized * size * 2 - size;
	}

	float3 GetGradient( Texture3D sdf, uint3 texel, out float signedDistance )
	{
		float4 texData = sdf.Load( int4( texel.xyz, 0 ) );
		signedDistance = texData.a;
		return texData.rgb;
	}
};