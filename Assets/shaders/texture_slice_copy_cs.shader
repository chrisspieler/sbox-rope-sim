MODES
{
	Default();
}

CS
{
	#include "system.fxc"

	RWTexture3D<float4> InputTexture < Attribute( "InputTexture" ); >;
	RWTexture2D<float4> OutputTexture < Attribute( "OutputTexture" ); >;
	int Slice < Attribute( "Slice" ); >;

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		OutputTexture[ id.xy ] = InputTexture[ uint3( id.xy, Slice )];;
	}	
}