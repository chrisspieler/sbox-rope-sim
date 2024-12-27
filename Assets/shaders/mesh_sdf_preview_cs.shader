MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "shared/voxel_sdf.hlsl"

	int ZLayer < Attribute( "ZLayer" ); >;
	RWTexture2D<float4> OutputTexture < Attribute( "OutputTexture" ); >;

	[numthreads( 32, 32, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		uint3 voxel = uint3( id.x, id.y, ZLayer );
		if ( !Voxel::IsInVolume( voxel ) )
			return;

		float4 texCol = 0;

		float sdf = Voxel::Load( voxel );
		if ( sdf > 0 )
		{
			sdf /= Voxel::GetVoxelSize().x * 64;
			sdf += 0.4;
			sdf = saturate( 1 - sdf );
			texCol = float4( sdf, sdf, sdf, 1 );
		}
		else 
		{
			sdf /= Voxel::GetVolumeSize().x / 4;
			sdf += 0.5f;
			sdf = saturate( sdf );
			texCol = float4( sdf * 0.1, sdf * 0.8, sdf, 1 );
		}

		OutputTexture[id.xy] = texCol;
	}	
}