MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"
	#include "shared/index.hlsl"

	// TODO: Make this a superclass of VerletPoint?
	// !! THIS MUST MATCH THE SIZE OF VerletPoint IN shared/verlet.hlsl !!
	struct MeshPoint
	{
		float3 Position;
		float Padding;
		float4 Padding2;
	};

	struct VerletBounds
	{
		float4 Mins;
		float4 Maxs;
	};

	// Input
	int BoundsIndex < Attribute( "BoundsIndex"); >;
	int NumPoints < Attribute( "NumPoints" ); >;
	RWStructuredBuffer<MeshPoint> Points < Attribute( "Points" ); >;
	float Skin < Attribute( "SkinSize" ); Default( 1.0 ); >;

	// Output
	RWStructuredBuffer<VerletBounds> BoundsBuffer < Attribute( "Bounds" ); >;

	groupshared float3 g_vMins[1024];
	groupshared float3 g_vMaxs[1024];

	// Perform parallel reduction to find bounds, but it's very unreliable if the rope has more points than threads.
	void UpdateBounds( int pIndex )
	{
		if ( pIndex >= NumPoints )
		{
			g_vMins[pIndex] = 1e20;
			g_vMaxs[pIndex] = -1e20;
		}
		else 
		{
			MeshPoint p = Points[pIndex];
			g_vMins[pIndex] = p.Position;
			g_vMaxs[pIndex] = p.Position;
		}
		
		GroupMemoryBarrierWithGroupSync();

		for ( int stride = 512; stride > 1; stride /= 2 )
		{
			if ( pIndex >= stride )
				return;
			
			float3 mn1 = g_vMins[pIndex];
			float3 mn2 = g_vMins[pIndex + stride];	
			g_vMins[pIndex] = min( mn1, mn2 );

			float3 mx1 = g_vMaxs[pIndex];
			float3 mx2 = g_vMaxs[pIndex + stride];
			g_vMaxs[pIndex] = max( mx1, mx2 );
			GroupMemoryBarrierWithGroupSync();
		}

		if ( pIndex == 0 )
		{
			VerletBounds bounds;
			bounds.Mins = float4( ( g_vMins[0] - Skin ).xyz, 0 );
			bounds.Maxs = float4( ( g_vMaxs[0] + Skin ).xyz, 0 );
			BoundsBuffer[BoundsIndex] = bounds;
		}
	}

	[numthreads( 1024, 1, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		if ( id.x >= 1024 )
			return;

		UpdateBounds( id.x );
	}	
}