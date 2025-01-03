float3 VoxelMinsOs < Attribute( "VoxelMinsOs" ); >;
float3 VoxelMaxsOs < Attribute( "VoxelMaxsOs" ); >;
float3 VoxelVolumeDims < Attribute( "VoxelVolumeDims" ); >;
RWStructuredBuffer<float> ScratchVoxelSdf < Attribute( "ScratchVoxelSdf"); >;
RWStructuredBuffer<int> VoxelSdf < Attribute( "VoxelSdf" ); >;

int FloatToByte( float value, float minValue, float maxValue )
{
	float invLerp = ( value - minValue ) / ( maxValue - minValue );
	invLerp = saturate( invLerp );
	return int( lerp( -128.0, 127.0, invLerp ) );
}

float ByteToFloat(int byte, float minValue, float maxValue )
{
	float invLerp = ( float(byte) - (-128.0 ) ) / ( 127.0 - (-128.0) );
	invLerp = saturate( invLerp );
	return lerp( minValue, maxValue, invLerp );
}

int PackFloats( float4 values, float minValue, float maxValue )
{
	int4 bytes = int4(
		FloatToByte( values.x, minValue, maxValue ),
		FloatToByte( values.y, minValue, maxValue ),
		FloatToByte( values.z, minValue, maxValue ),
		FloatToByte( values.w, minValue, maxValue )
	);
	
	uint4 ubytes = uint4( bytes + 128 );

	return int(( ubytes.w << 24 ) | ( ubytes.z << 16 ) | ( ubytes.y << 8 ) | ubytes.x );
}

float4 UnpackFloats( int packed, float minValue, float maxValue )
{
	int4 ubytes = int4(
		(packed) & 0xFF,
		(packed >> 8 ) & 0xFF,
		(packed >> 16 ) & 0xFF,
		(packed >> 24 ) & 0xFF
	);

	int4 bytes = int4(ubytes) - 128;

	return float4(
		ByteToFloat( bytes.x, minValue, maxValue ),
		ByteToFloat( bytes.y, minValue, maxValue ),
		ByteToFloat( bytes.z, minValue, maxValue ),
		ByteToFloat( bytes.w, minValue, maxValue )
	);
}

struct Voxel
{
	static bool IsInVolume( uint3 voxel )
	{
		return all( voxel >= 0 ) && all( voxel < (uint3)VoxelVolumeDims );
	}

	static float3 GetVolumeSize()
	{
		return VoxelMaxsOs - VoxelMinsOs;
	}

	static float3 GetVoxelSize()
	{
		return GetVolumeSize() / VoxelVolumeDims;
	}

	static float3 GetPositionOs( uint3 voxel )
	{
		float3 normalized = float3(voxel) / VoxelVolumeDims;
		return VoxelMinsOs + normalized * GetVolumeSize();
	}

	static float3 GetCenterPositionOs( uint3 voxel )
	{
		return GetPositionOs( voxel ) + GetVoxelSize() * 0.5;
	}

	static uint3 FromPosition( float3 positionOs )
	{
		float3 normalized = ( positionOs - VoxelMinsOs ) / GetVolumeSize();
		return VoxelVolumeDims * normalized;
	}

	static uint3 FromPositionClamped( float3 positionOs )
	{
		positionOs = clamp( positionOs, VoxelMinsOs, VoxelMaxsOs );
		return FromPosition( positionOs );
	}

	static int Index3DTo1D( uint3 voxel )
	{
		uint3 dims = (uint3)VoxelVolumeDims;
		return ( voxel.z * dims.y * dims.x ) + ( voxel.y * dims.x ) + voxel.x;
	}

	static int Index3DTo1DQuad( uint3 voxel )
	{
		uint3 dims = (uint3)VoxelVolumeDims;
		int i = ( voxel.z * dims.y * dims.x ) + ( voxel.y * dims.x ) + voxel.x;
		return i / 4;
	}

	static uint3 Index1DTo3D( int i )
	{
		int z = i / ( VoxelVolumeDims.x * VoxelVolumeDims.y );
		i -= ( z * VoxelVolumeDims.x * VoxelVolumeDims.y );
		int y = i / VoxelVolumeDims.x;
		int x = i % VoxelVolumeDims.x;
		return uint3( x, y, z );
	}

	static float4 Load4( uint3 voxel )
	{
		int i = Voxel::Index3DTo1DQuad( voxel );
		int packed = VoxelSdf[i];
		float size = VoxelVolumeDims.x;
		return UnpackFloats( packed, size * -0.5, size * 0.5 );
	}

	static float Load( uint3 voxel )
	{
		int i = Voxel::Index3DTo1D( voxel );
		return ScratchVoxelSdf[i];
	}

	static void Store4( uint3 voxel, float4 signedDistances )
	{
		float size = VoxelVolumeDims.x;
		int packed = PackFloats( signedDistances, size * -0.5, size * 0.5 );
		int i = Voxel::Index3DTo1DQuad( voxel );
		VoxelSdf[i] = packed;
	}

	static void Store( uint3 voxel, float signedDistance )
	{
		int i = Voxel::Index3DTo1D( voxel );
		ScratchVoxelSdf[i] = signedDistance;
	}

	static void Compress( uint3 voxel )
	{
		// By this point, the voxel distance field should have been stored 
		// as a buffer of floats. We need to fetch those.
		float4 floats = float4( 0, 0, 0, 0 );
		voxel.x *= 4;
		int index = Index3DTo1D( voxel );
		for( int i = 0; i < 4; i++ )
		{
			floats[i] = ScratchVoxelSdf[index + i];
		}
		Voxel::Store4( voxel, floats );
	}
};

