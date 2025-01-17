float3 VoxelMinsOs < Attribute( "VoxelMinsOs" ); >;
float3 VoxelMaxsOs < Attribute( "VoxelMaxsOs" ); >;
float3 VoxelVolumeDims < Attribute( "VoxelVolumeDims" ); >;
RWTexture3D<float4> SdfTexture < Attribute( "SdfTexture" ); >;

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

	static void LoadData( uint3 voxel, out float3 gradient, out float signedDistance )
	{
		float4 texData = SdfTexture[voxel];
		gradient = texData.rgb;
		float size = GetVolumeSize().x;
		signedDistance = lerp( -size, size, texData.a );
	}

	static void StoreData( uint3 voxel, float3 gradient, float signedDistance )
	{
		signedDistance /= GetVolumeSize().x;
		signedDistance = clamp( signedDistance, -1, 1 );
		signedDistance += 1;
		signedDistance /= 2;
		SdfTexture[voxel] = float4( gradient.rgb, signedDistance );
	}
};