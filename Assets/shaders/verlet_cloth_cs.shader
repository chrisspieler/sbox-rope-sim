MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"

	struct VerletPoint
	{
		float3 Position;
		float3 LastPosition;
	};

	struct VerletStickConstraint
	{
		int Point1;
		int Point2;
		float Length;
	};

	RWStructuredBuffer<VerletPoint> Points < Attribute( "Points" ); >;
	RWStructuredBuffer<VerletStickConstraint> Sticks < Attribute( "Sticks" ); >;
	int NumPoints < Attribute( "NumPoints" ); >;
	int NumColumns < Attribute( "NumColumns" ); >;
	float DeltaTime < Attribute( "DeltaTime" ); >;

	int Index2DTo1D( uint2 i )
	{
		return i.y * NumColumns + i.x;
	}

	[numthreads( 8, 8, 1 )]
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		int i = Index2DTo1D( id.xy );
		if ( i >= NumPoints )
			return;

		VerletPoint p = Points[id.x];
		float3 temp = p.Position;
		float3 delta = p.Position - p.LastPosition;
		p.Position += delta;
		// Gravity
		p.Position += float3( 0, 0, -800 ) * ( DeltaTime * DeltaTime );
		p.LastPosition = temp;
		Points[id.x] = p;
	}	
}