MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"
	#include "shared/verlet.hlsl"
	#include "shared/index.hlsl"

	struct Vertex 
	{
		float3 Position;
		float4 TexCoord0;
		float4 Normal;
		float4 Tangent0;
		float4 TexCoord1;
		float4 Color0;
	};

	int NumPoints < Attribute( "NumPoints" ); >;
	RWStructuredBuffer<VerletPoint> Points < Attribute( "Points" ); >;
	float4 Tint < Attribute( "Tint" ); Default4( 1.0, 1.0, 1.0, 1.0 ); >;
	float RenderWidth < Attribute( "RenderWidth" ); Default( 1.0 ); >;
	float TextureCoord < Attribute( "TextureCoord" ); Default( 0.0 ); >;
	RWStructuredBuffer<Vertex> OutputVertices < Attribute( "OutputVertices" ); >;
	RWStructuredBuffer<uint> OutputIndices < Attribute( "OutputIndices" ); >;

	void OutputRopeVertex( int pIndex )
	{
		VerletPoint p = Points[pIndex];
		float3 delta = p.Position - p.LastPosition;
		Vertex v;
		v.Position = p.Position;
		v.TexCoord0 = float4( RenderWidth, TextureCoord, 0, 0 );
		v.Normal = float4( 0, 0, 1, 0 );
		v.Tangent0 = float4( delta.xyz, 0 );
		v.TexCoord1 = Tint;
		v.Color0 = float4( 1, 1, 1, 1 );
		if ( pIndex == 0 )
		{
			OutputVertices[0] = v;
		}
		OutputVertices[pIndex + 1] = v;
		if ( pIndex == NumPoints - 1 )
		{
			OutputVertices[NumPoints + 1] = v;
		}
	}
	
	[numthreads( 1024, 1, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		OutputRopeVertex( id.x );
	}
}