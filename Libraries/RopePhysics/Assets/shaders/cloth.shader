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

	bool UseColorTexture < Attribute( "UseColorTexture" ); >;
	Texture2D TexColor < Attribute( "TexColor" ); >;
	bool UseNormalTexture < Attribute( "UseNormalTexture" ); >;
	Texture2D TexNormal < Attribute( "TexNormal" ); >;
	bool UseTintMask < Attribute( "UseTintMask" ); >;
	Texture2D TexTintMask < Attribute( "TexTintMask" ); >;
	float4 Tint < Attribute( "Tint" ); Default4( 1.0, 1.0, 1.0, 1.0 ); >;

	RenderState( CullMode, NONE );
	#if D_WIREFRAME
	RenderState( FillMode, WIREFRAME );
	#endif

	float3 GetTint( PS_INPUT i, float3 albedo )
	{
		float strength = 1;
		if ( UseTintMask )
		{
			strength = TexTintMask.Sample( g_sAniso, i.UV.xy ).r;
		}
		return lerp( albedo, albedo * Tint.rgb, strength );
	}

	float3 GetAlbedo( PS_INPUT i )
	{
		float3 albedo = float3( 0, i.UV.x, i.UV.y );
		if ( UseColorTexture )
		{
			albedo = TexColor.Sample( g_sAniso, i.UV.xy ).rgb;
		}
		albedo *= GetTint( i, albedo ).rgb;
		return albedo;
	}

	float3 GetNormal( PS_INPUT i )
	{
		float3 normal = i.Normal.xyz;
		float3 dirToCam = normalize( g_vCameraPositionWs -  i.vPositionWs);
		return normal;
	}

	float GetRoughness( PS_INPUT i )
	{
		if ( UseNormalTexture )
		{
			return 1;
		}
		return 0.1;
	}

	float GetAmbientOcclusion( PS_INPUT i )
	{
		return 1;
	}
	
	float4 MainPs( PS_INPUT i ) : SV_Target0
	{
		Material m = Material::Init();
		
		m.Albedo = GetAlbedo( i );
		m.Normal = GetNormal( i );
		m.Roughness = GetRoughness( i );
		m.AmbientOcclusion = GetAmbientOcclusion( i );
		m.WorldPosition = i.vPositionWs;
		return ShadingModelStandard::Shade( m );
	}
}
