float3 VoxelMinsOs < Attribute( "VoxelMinsOs" ); >;
float3 VoxelMaxsOs < Attribute( "VoxelMaxsOs" ); >;
float3 VoxelVolumeDims < Attribute( "VoxelVolumeDims" ); >;
RWStructuredBuffer<float> ScratchVoxelSdf < Attribute( "ScratchVoxelSdf"); >;
globallycoherent RWByteAddressBuffer VoxelSdf < Attribute( "VoxelSdf" ); >;

uint FloatToUnsignedByte( float value, float minValue, float maxValue )
{
	float invLerp = ( value - minValue ) / ( maxValue - minValue );
	invLerp = saturate( invLerp );
	return uint(invLerp * 255 );
}

float UnsignedByteToFloat(uint byte, float minValue, float maxValue )
{
	float invLerp = saturate( ( float(byte) + 127.0 ) / 255.0 );
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

	static void StoreByte( uint3 voxel, float signedDistance )
	{
		int i = Voxel::Index3DTo1D( voxel );

		// How many bytes beyond the DWORD are we?
		uint offset = i % 4;
		// What is the DWORD-aligned index of this voxel?
		int iWord = i - offset;
		// How many bits left of the lowest byte are we storing this byte?
		uint shift = 8 * offset;

		// Create our byte...
		uint byte = FloatToUnsignedByte( signedDistance, VoxelVolumeDims.x * -0.5, VoxelVolumeDims.x * 0.5 );
		// ...mask it...
		byte &= 0xFF;
		// ...and shift it to the correct offset within the DWORD.
		byte <<= shift;

		uint packed = VoxelSdf.Load( iWord );
		// Create a mask that will empty out the byte we want to write to.
		uint mask = (uint)0xFF << shift;
		packed &= ~mask;
		// Finally, slot our byte in to its proper place.
		packed |= byte;

		uint iOut;
		VoxelSdf.InterlockedExchange( iWord, packed, iOut );
		// We're using globallycoherent, so don't worry about clobbering other bytes.
		// VoxelSdf.Store( iWord, packed );
	}

	static float LoadByte( uint3 voxel )
	{
		int i = Voxel::Index3DTo1D( voxel );
		// How many bytes beyond the DWORD boundary are we?
		uint offset = i % 4;
		// What is the DWORD-aligned index of this voxel?
		int iWord = i - offset;
		// How many bits left of the lowest byte is the byte we are loading?
		uint shift = 8 * offset;

		uint packed = VoxelSdf.Load( iWord );
		
		// Make sure the lowest byte is the one we're looking for.
		uint byte = packed >> shift;
		// Mask out everything we don't need.
		byte &= 0xFF;

		return UnsignedByteToFloat( byte, VoxelVolumeDims.x * -0.5, VoxelVolumeDims.x * 0.5 );
	}

	static float Load( uint3 voxel )
	{
		int i = Voxel::Index3DTo1D( voxel );
		return ScratchVoxelSdf[i];
	}

	static void Store( uint3 voxel, float signedDistance )
	{
		int i = Voxel::Index3DTo1D( voxel );
		// Store this voxel as a float to work with more accurately within the shader.
		ScratchVoxelSdf[i] = signedDistance;
		// Store this voxel as a byte as well.
		StoreByte( voxel, signedDistance );
	}
};

