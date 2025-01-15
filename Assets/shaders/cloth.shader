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
	float4 Normal : NORMAL;
	float4 Tint : TEXCOORD9;
	float4 UV : TEXCOORD3;
};

VS
{
	PS_INPUT MainVs( VS_INPUT i )
	{
		PS_INPUT o;
		o.vPositionWs = i.pos;
		o.vPositionPs = Position3WsToPs( i.pos );
		o.Normal = i.normal;
		o.Tint = i.tint;
		o.UV = i.uv;
		return o;
	}
}

PS
{
	#include "common/pixel.hlsl"

	DynamicCombo( D_WIREFRAME, 0..1, Sys( All ) );

	RenderState( CullMode, NONE );
	#if D_WIREFRAME
	RenderState( FillMode, WIREFRAME );
	#endif
	
	float4 MainPs( PS_INPUT i ) : SV_Target0
	{
		Material m = Material::Init();
		m.Albedo = float3( 0, i.UV.x, i.UV.y );
		m.Normal = i.Normal.xyz;
		m.Normal = float3( 0, 0, 1 );
		m.Roughness = 0;
		/* m.Metalness = 1.0f; // Forces the object to be metalic */
		return ShadingModelStandard::Shade( m );
	}
}
