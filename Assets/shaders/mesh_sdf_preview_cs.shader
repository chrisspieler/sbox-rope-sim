MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "shared/index.hlsl"
	#include "shared/signed_distance_field.hlsl"
	#include "common/classes/Bindless.hlsl"

	int SdfTextureIndex < Attribute( "SdfTextureIndex" ); >;
	float3 MinsWs < Attribute( "MinsWs" ); >;
	float3 MaxsWs < Attribute( "MaxsWs" ); >;
	int TextureSize < Attribute( "TextureSize" ); >;
	int ZLayer < Attribute( "ZLayer" ); >;
	RWTexture2D<float4> OutputTexture < Attribute( "OutputTexture" ); >;

	DynamicCombo( D_MODE, 0..1, Sys( All ) );

	float3 GetDistanceColor( uint3 voxel, SignedDistanceField sdf )
	{
		Texture3D sdfTexture = Bindless::GetTexture3D( SdfTextureIndex );
		float sdMesh = sdf.GetSignedDistance( sdfTexture, voxel );
		float3 minColor = float3( 0.5, 0.5, 0.5 );
		float3 maxColor = float3( 0, 0, 0 );
		float progressFactor = 2.5;
		if ( sdMesh < 0 )
		{
			minColor = float3( 0, 0.25, 0.4 );
			maxColor = float3(0, 0.15, 0.3 );
			progressFactor = 5;
		}
		float progress = abs( sdMesh ) / sdf.GetBoundsSize().x;
		progress *= progressFactor;
		progress = saturate( progress );
		return lerp( minColor, maxColor, progress );
	}

	float3 GetGradientColor( uint3 voxel, SignedDistanceField sdf )
	{
		Texture3D sdfTexture = Bindless::GetTexture3D( SdfTextureIndex );
		float sdMesh = 0;
		float3 gradient = sdf.GetGradient( sdfTexture, voxel, sdMesh );
		if ( abs( gradient.x + gradient.y + gradient.z ) < 0.001 )
			return 0;

		return gradient;
	}

	float4 GetOutputColor( uint3 voxel, SignedDistanceField sdf )
	{
		float3 texCol = 0;
		// Inside/Outside
		#if D_MODE == 0
			texCol = GetDistanceColor( voxel, sdf );
		// Gradients
		#else
			texCol = GetGradientColor( voxel, sdf );
		#endif
		return float4( texCol.xyz, 1 );
	}

	[numthreads( 8, 8, 8 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		if ( any( id > TextureSize ) )
			return;
			
		SignedDistanceField sdf;
		sdf.SdfTextureIndex = SdfTextureIndex;
		sdf.MinsWs = MinsWs;
		sdf.MaxsWs = MaxsWs;
		sdf.TextureSize = TextureSize;

		OutputTexture[id.xy] = GetOutputColor( id, sdf );
	}	
}