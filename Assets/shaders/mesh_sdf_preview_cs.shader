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

	[numthreads( 2, 8, 8 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		uint3 quadVoxel = uint3( id.x, id.y, ZLayer );
		quadVoxel.x *= 4;

		if ( !Voxel::IsInVolume( quadVoxel ) )
			return;

		float4 texCol = 0;
		float4 sdfs = Voxel::Load4( quadVoxel );
		for( int i = 0; i < 4; i++ )
		{
			float sdf = sdfs[i];
			texCol = float4( sdf, sdf, sdf, 1 );
			if ( sdf > 0 )
			{
				sdf /= Voxel::GetVolumeSize().x;
				sdf += 0.5;
				sdf = saturate( 1 - sdf );
				texCol = float4( sdf, sdf, sdf, 1 );
			}
			else 
			{
				sdf /= Voxel::GetVolumeSize().x;
				sdf += 0.5;
				sdf = saturate( sdf );
				texCol = float4( max( 0.1, sdf * 0.2), max( 0.15, sdf * 0.7 ), max( 0.3, sdf ), 1 );
			}
			int2 iTex = int2(
				quadVoxel.x + i,
				quadVoxel.y
			);
			OutputTexture[iTex] = texCol;
		}
	}	
}