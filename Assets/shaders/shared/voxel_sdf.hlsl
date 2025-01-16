float3 VoxelMinsOs < Attribute( "VoxelMinsOs" ); >;
float3 VoxelMaxsOs < Attribute( "VoxelMaxsOs" ); >;
float3 VoxelVolumeDims < Attribute( "VoxelVolumeDims" ); >;
RWStructuredBuffer<int> VoxelSdf < Attribute( "VoxelSdf" ); >;
RWTexture3D<float> SdfTexture < Attribute( "SdfTexture" ); >;

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

struct Voxel
{
	static bool IsInVolume( uint3 voxel )
	{
		return all( voxel >= 0 ) && all( voxel < (uint3)VoxelVolumeDims );
	}

	static bool IsInVolume( float3 positionOs )
	{
		return all( positionOs >= VoxelMinsOs ) && all( positionOs <= VoxelMaxsOs );
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

	static uint3 Index1DTo3D( int i )
	{
		int size = VoxelVolumeDims.x;
		int x = i / ( size * size );
		int y = ( i / size ) % size;
		int z = i % size;
		return uint3( x, y, z );
	}

	static float Load( uint3 voxel )
	{
		float ud = SdfTexture[voxel];
		ud *= 2;
		ud -= 1;
		return ud * GetVolumeSize().x;
	}

	static void Store( uint3 voxel, float signedDistance )
	{
		signedDistance /= GetVolumeSize().x;
		signedDistance = clamp( signedDistance, -1, 1 );
		signedDistance += 1;
		signedDistance /= 2;
		SdfTexture[voxel] = signedDistance;
	}
};