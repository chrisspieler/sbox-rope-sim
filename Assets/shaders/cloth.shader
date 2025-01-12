FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
    VrForward();
    Depth();
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VS_INPUT
{
	float3 pos : POSITION < Semantic( None ); >;
	float4 uv  : TEXCOORD0 < Semantic( None ); >;
    float4 normal : NORMAL < Semantic( None ); >;
    float4 velocity : TANGENT0 < Semantic( None ); >;
    float4 tint : TEXCOORD1 < Semantic( None ); >;
    float4 color : COLOR0 < Semantic( None ); >;
};

struct PS_INPUT
{
	float4 vPositionPs : SV_Position;
    float3 vPositionWs: TEXCOORD1;
	float4 tint : TEXCOORD9;
	float4 uv : TEXCOORD3;
};

VS
{
	PS_INPUT MainVs( VS_INPUT i )
	{
		PS_INPUT o;
		o.vPositionWs = i.pos;
		o.vPositionPs = Position3WsToPs( i.pos );
		return o;
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	float4 MainPs( PS_INPUT i ) : SV_Target0
	{
		// Material m = Material::From( i );
		/* m.Metalness = 1.0f; // Forces the object to be metalic */
		// return ShadingModelStandard::Shade( i, m );
		return float4( 0, i.uv.x, i.uv.y, 1 );
	}
}
