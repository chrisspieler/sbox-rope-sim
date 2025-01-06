MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "shared/voxel_sdf.hlsl"
	#include "shared/index.hlsl"

	int ZLayer < Attribute( "ZLayer" ); >;
	RWTexture2D<float4> OutputTexture < Attribute( "OutputTexture" ); >;
	StructuredBuffer<float4> Gradients < Attribute( "Gradients" ); >;

	DynamicCombo( D_MODE, 0..1, Sys( All ) );

	float3 GetDistanceColor( float sdMesh )
	{
		float3 minColor = float3( 0.5, 0.5, 0.5 );
		float3 maxColor = float3( 0, 0, 0 );
		float progressFactor = 2.5;
		if ( sdMesh < 0 )
		{
			minColor = float3( 0, 0.25, 0.4 );
			maxColor = float3(0, 0.15, 0.3 );
			progressFactor = 5;
		}
		float progress = abs( sdMesh ) / Voxel::GetVolumeSize().x;
		progress *= progressFactor;
		progress = saturate( progress );
		return lerp( minColor, maxColor, progress );
	}

	float3 GetGradientColor( uint3 voxel, float sdMesh )
	{
		uint i = Convert3DIndexTo1D( voxel, VoxelVolumeDims );
		float3 gradient = Gradients[i];
		gradient += 1;
		if ( all( gradient == 0 ) )
			return 0;

		gradient /= 2.0;
		return gradient;
	}

	float3 GetOutputColor( uint3 voxel, float sdMesh )
	{
		// Inside/Outside
		#if D_MODE == 0
			return GetDistanceColor( sdMesh );
		// Gradients
		#else
			return GetGradientColor( voxel, sdMesh );
		#endif
	}

	[numthreads( 2, 8, 8 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		uint3 quadVoxel = uint3( id.x, id.y, ZLayer );
		quadVoxel.x *= 4;

		if ( !Voxel::IsInVolume( quadVoxel ) )
			return;

		float4 distances = Voxel::Load4( quadVoxel );
		for( int i = 0; i < 4; i++ )
		{
			float sdMesh = distances[i];
			uint3 fullVoxel = quadVoxel + uint3( i, 0, 0 );

			float3 texCol = GetOutputColor( fullVoxel, sdMesh );

			OutputTexture[fullVoxel.xy] = float4( texCol.rgb, 1 );
		}
	}	
}