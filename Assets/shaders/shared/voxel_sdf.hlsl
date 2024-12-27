float3 VoxelMinsOs < Attribute( "VoxelMinsOs" ); >;
float3 VoxelMaxsOs < Attribute( "VoxelMaxsOs" ); >;
float3 VoxelDims < Attribute( "VoxelDims" ); >;
RWStructuredBuffer<float> VoxelSdf < Attribute( "VoxelSdf" ); >;

struct Voxel
{
	static bool IsInVolume( uint3 voxel )
	{
		return !any( voxel >= (uint3)VoxelDims );
	}

	static float3 GetVolumeSize()
	{
		return VoxelMaxsOs - VoxelMinsOs;
	}

	static float3 GetVoxelSize()
	{
		return GetVolumeSize() / VoxelDims;
	}

	static float3 GetPositionOs( uint3 voxel )
	{
		float3 normalized = float3(voxel) / VoxelDims;
		return VoxelMinsOs + normalized * GetVolumeSize();
	}

	static float3 GetCenterPositionOs( uint3 voxel )
	{
		return GetPositionOs( voxel ) + GetVoxelSize() * 0.5;
	}

	static uint3 FromPosition( float3 positionOs )
	{
		float3 normalized = ( positionOs - VoxelMinsOs ) / GetVolumeSize();
		return VoxelDims * normalized;
	}

	static int Index3DTo1D( uint3 voxel )
	{
		uint3 dims = (uint3)VoxelDims;
		return ( voxel.z * dims.y * dims.z ) + ( voxel.y * dims.x ) + voxel.x;
	}

	static uint3 Index1DTo3D( int i )
	{
		int z = i / ( VoxelDims.x * VoxelDims.y );
		i -= ( z * VoxelDims.x * VoxelDims.y );
		int y = i / VoxelDims.x;
		int x = i % VoxelDims.x;
		return uint3( x, y, z );
	}

	static void Store( uint3 voxel, float signedDistance )
	{
		int i = Voxel::Index3DTo1D( voxel );
		VoxelSdf[i] = signedDistance;
	}

	static float Load( uint3 voxel )
	{
		int i = Voxel::Index3DTo1D( voxel );
		return VoxelSdf[i];
	}
};

