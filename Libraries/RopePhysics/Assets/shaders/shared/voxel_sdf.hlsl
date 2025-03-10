float3 VoxelMinsOs < Attribute( "VoxelMinsOs" ); >;
float3 VoxelMaxsOs < Attribute( "VoxelMaxsOs" ); >;
uint SdfTextureSize < Attribute( "SdfTextureSize" ); >;
globallycoherent RWTexture3D<float4> SdfTexture < Attribute( "SdfTexture" ); >;

struct Voxel
{
	static bool IsInVolume( uint3 voxel )
	{
		return all( voxel >= 0 ) && all( voxel < SdfTextureSize );
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
		return GetVolumeSize() / SdfTextureSize;
	}

	static float3 GetPositionOs( uint3 voxel )
	{
		float3 normalized = float3(voxel) / SdfTextureSize;
		return VoxelMinsOs + normalized * GetVolumeSize();
	}

	static float3 GetCenterPositionOs( uint3 voxel )
	{
		return GetPositionOs( voxel ) + GetVoxelSize() * 0.5;
	}

	static uint3 FromPosition( float3 positionOs )
	{
		float3 normalized = ( positionOs - VoxelMinsOs ) / GetVolumeSize();
		return (uint3)(SdfTextureSize  * normalized );
	}

	static uint3 FromPositionClamped( float3 positionOs )
	{
		positionOs = clamp( positionOs, VoxelMinsOs, VoxelMaxsOs );
		return FromPosition( positionOs );
	}

	static int Index3DTo1D( uint3 voxel )
	{
		return ( voxel.z * SdfTextureSize * SdfTextureSize ) + ( voxel.y * SdfTextureSize ) + voxel.x;
	}

	static uint3 Index1DTo3D( int i )
	{
		int z = i / ( SdfTextureSize * SdfTextureSize );
		int y = ( i / SdfTextureSize ) % SdfTextureSize;
		int x = i % SdfTextureSize;
		return uint3( x, y, z );
	}

	static void LoadData( uint3 voxel, out float3 gradient, out float signedDistance )
	{
		float4 texData = SdfTexture[voxel];
		gradient = texData.rgb;
		gradient *= 2;
		gradient -= 1;
		float size = GetVolumeSize().x;
		signedDistance = lerp( -size, size, texData.a );
	}

	static void StoreData( uint3 voxel, float3 gradient, float signedDistance )
	{
		signedDistance /= GetVolumeSize().x;
		signedDistance = clamp( signedDistance, -1, 1 );
		signedDistance += 1;
		signedDistance /= 2;
		gradient += 1;
		gradient /= 2;
		gradient = saturate( gradient );
		SdfTexture[voxel] = float4( gradient.rgb, signedDistance );
	}
};