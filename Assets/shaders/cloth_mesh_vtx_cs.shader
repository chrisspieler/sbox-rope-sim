MODES
{
	Default();
}

CS
{
	#include "system.fxc"

	struct SimpleVertex
	{
		float4 Position;
		float4 Normal;
		float4 Tangent;
		float4 TexCoord;
	};


	RWStructuredBuffer<SimpleVertex> Vertices < Attribute( "Vertices" ); >;
	RWTexture2D<float4> Result < Attribute( "Result" ); >;



	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		Result[ id.xy ] = float4( id.x & id.y, ( id.x & 15 ) / 15.0, ( id.y & 15 ) / 15.0, 0.0 );
	}	
}