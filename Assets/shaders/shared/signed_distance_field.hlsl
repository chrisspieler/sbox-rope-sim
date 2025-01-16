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
		float ud = sdf.Load( int4( texel.xyz, 0 ) ).r;
		ud *= 2;
		ud -= 1;
		return ud * GetBoundsSize().x;
	}

	float3 GetGradient( Texture3D sdf, uint3 texel, out float signedDistance )
	{
		// This method was adapted from: https://shaderfun.com/2018/07/23/signed-distance-fields-part-8-gradients-bevels-and-noise/

		float d = GetSignedDistance( sdf, texel );
		signedDistance = d;
		float sign = d < 0 ? -1 : 1;
		float maxValue = TextureSize * sign;

		float3 xOffset = float3( 1, 0, 0 );
		float x0 = texel.x > 0 ? GetSignedDistance( sdf, texel - xOffset ) : maxValue;
		float x1 = texel.x < (TextureSize - 1) ? GetSignedDistance( sdf, texel + xOffset ) : maxValue;
		// Y
		float3 yOffset = float3( 0, 1, 0 );
		float y0 = texel.y > 0 ? GetSignedDistance( sdf, texel - yOffset ) : maxValue;
		float y1 = texel.y < (TextureSize - 1) ? GetSignedDistance( sdf, texel + yOffset ) : maxValue;
		// Z
		float3 zOffset = float3( 0, 0, 1 );
		float z0 = texel.z > 0 ? GetSignedDistance( sdf, texel - zOffset ) : maxValue;
		float z1 = texel.z < (TextureSize - 1) ? GetSignedDistance( sdf, texel + zOffset ) : maxValue;

		float ddx = sign * x0 < sign * x1 ? -(x0 - d) : (x1 - d);
		float ddy = sign * y0 < sign * y1 ? -(y0 - d) : (y1 - d);
		float ddz = sign * z0 < sign * z1 ? -(z0 - d) : (z1 - d);
		return normalize( float3( ddx, ddy, ddz ) );
	}
};