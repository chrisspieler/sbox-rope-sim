MODES
{
	Default();
}

CS
{
	#include "system.fxc"
	#include "common.fxc"

	#include "common/classes/Bindless.hlsl"

	#include "shared/index.hlsl"
	#include "shared/verlet.hlsl"
	
	// Layout
	int NumPoints < Attribute( "NumPoints" ); >;
	int NumColumns < Attribute( "NumColumns" ); >;
	float SegmentLength < Attribute( "SegmentLength" ); Default( 1.0 ); >;
	RWStructuredBuffer<VerletPoint> Points < Attribute( "Points" ); >;
	// Updates
	int NumPointUpdates < Attribute( "NumPointUpdates" ); Default( 0 ); >;
	RWStructuredBuffer<VerletPointUpdate> PointUpdates < Attribute ( "PointUpdates" ); >;
	// Simulation
	int Iterations < Attribute( "Iterations" ); >;
	float PointWidth < Attribute( "PointWidth" ); Default( 1.0 ); >;
	// Forces
	float3 Gravity < Attribute( "Gravity" ); Default3( 0, 0, -800.0 ); >;
	// Rope rendering
	float RopeRenderWidth < Attribute( "RopeRenderWidth" ); Default( 1.0 ); >;
	float RopeTextureCoord < Attribute( "RopeTextureCoord" ); Default( 1.0 ); >;
	float4 RopeTint < Attribute( "RopeTint"); Default4( 0.0, 0.0, 0.0, 1.0 ); >;
	// Delta
	float DeltaTime < Attribute( "DeltaTime" ); >;
	float TimeStepSize < Attribute( "TimeStepSize"); Default( 0.01 ); >;
	float MaxTimeStepPerUpdate < Attribute( "MaxTimeStepPerUpdate" ); Default( 0.1 ); >;
	float3 Translation < Attribute( "Translation" ); >;

	struct SphereCollider
	{
		float3 Center;
		float Radius;
		float4x4 LocalToWorld;
		float4x4 WorldToLocal;

		void ResolveCollision( int pIndex )
		{
			VerletPoint p = Points[pIndex];
			float3 pPositionOs = mul( WorldToLocal, float4( p.Position.xyz, 1 ) ).xyz;
			float dist = distance( pPositionOs, Center );
			if ( dist - Radius > PointWidth )
				return;

			float3 dir = normalize( pPositionOs - Center );
			float3 hitPositionOs = Center + dir * ( PointWidth + Radius );
			float3 hitPositionWs = mul( LocalToWorld, float4( hitPositionOs.xyz, 1 ) ).xyz;
			p.Position = hitPositionWs;
			Points[pIndex] = p;
		}
	};
	
	struct BoxCollider
	{
		float3 Size;
		int Padding;
		float4x4 LocalToWorld;
		float4x4 WorldToLocal;

		void ResolveCollision( int pIndex )
		{
			VerletPoint p = Points[pIndex];
			float3 pPositionOs = mul( WorldToLocal, float4( p.Position.xyz, 1 ) ).xyz;
			float3 halfSize = Size * 0.5f;
			float3 scale = 1;
			float3 pointDepth = halfSize - abs( pPositionOs );
			float radius = PointWidth;

			if ( pointDepth.x <= -radius || pointDepth.y <= -radius || pointDepth.z <= -radius )
				return;

			float3 pointScaled = pointDepth * scale;
			float3 signs = sign( pPositionOs );
			if ( pointScaled.x < pointScaled.y && pointScaled.x < pointScaled.z )
			{
				pPositionOs.x = halfSize.x * signs.x + radius * signs.x;
			}
			else if ( pointScaled.y < pointScaled.x && pointScaled.y < pointScaled.z )
			{
				pPositionOs.y = halfSize.y * signs.y + radius * signs.y;
			}
			else
			{
				pPositionOs.z = halfSize.z * signs.z + radius * signs.z;
			}
			p.Position = mul( LocalToWorld, float4( pPositionOs.xyz, 1 ) ).xyz;
			Points[pIndex] = p;
		}
	};

	struct CapsuleCollider
	{
		float3 Start;
		float Radius;
		float3 End;
		int Padding;
		float4x4 LocalToWorld;
		float4x4 WorldToLocal;

		// Adapted from: https://iquilezles.org/articles/distfunctions/
		float sdCapsule( float3 p )
		{
			float3 pa = p - Start;
			float3 ba = End - Start;
			float h = clamp( dot( pa, ba ) / dot( ba, ba ), 0, 1 );
			return length(pa - ba * h) - Radius;
		}

		void ResolveCollision( int pIndex )
		{
			VerletPoint p = Points[pIndex];
			float3 pPositionOs = mul( WorldToLocal, float4( p.Position.xyz, 1 ) ).xyz;
			float sd = sdCapsule( pPositionOs );
			if ( sd > PointWidth )
				return;

			float3 xOffset = float3( 0.0001, 0, 0 );
			float3 yOffset = float3( 0, 0.0001, 0 );
			float3 zOffset = float3( 0, 0, 0.0001 );
			float3 gradient = float3
			(
				sdCapsule( pPositionOs + xOffset ) - sdCapsule( pPositionOs - xOffset ),
				sdCapsule( pPositionOs + yOffset ) - sdCapsule( pPositionOs - yOffset ),
				sdCapsule( pPositionOs + zOffset ) - sdCapsule( pPositionOs - zOffset )
			);
			gradient = normalize( gradient );
			pPositionOs += gradient * ( -sd + PointWidth );
			p.Position = mul( LocalToWorld, float4( pPositionOs.xyz, 1 ) ).xyz;
			Points[pIndex] = p;
		}
	};

	#include "shared/signed_distance_field.hlsl"

	void ResolveSdfCollision( SignedDistanceField sdf, int pIndex )
	{
		Texture3D sdTex = Bindless::GetTexture3D( sdf.SdfTextureIndex );
		VerletPoint p = Points[pIndex];

		float3 pPositionOs = mul( sdf.WorldToLocal, float4( p.Position.xyz, 1 ) ).xyz;
		if ( any( pPositionOs < 0 ) || any( pPositionOs > sdf.BoundsSizeOs ) )
			return;
			
		uint3 texel = sdf.PositionOsToTexel( pPositionOs );
		float sd = 0;
		float3 gradient = sdf.GetGradient( sdTex, texel, sd );
		if ( sd > PointWidth )
			return;

		pPositionOs += gradient * ( -sd + PointWidth );
		p.Position = mul( sdf.LocalToWorld, float4( pPositionOs.xyz, 1 ) ).xyz;
		Points[pIndex] = p;
	}

	// Colliders
	int NumSphereColliders < Attribute( "NumSphereColliders" ); >;
	RWStructuredBuffer<SphereCollider> SphereColliders < Attribute( "SphereColliders" ); >;
	int NumBoxColliders < Attribute( "NumBoxColliders" ); >;
	RWStructuredBuffer<BoxCollider> BoxColliders < Attribute( "BoxColliders" ); >;
	int NumCapsuleColliders < Attribute( "NumCapsuleColliders" ); >;
	RWStructuredBuffer<CapsuleCollider> CapsuleColliders < Attribute( "CapsuleColliders" ); >;
	int NumMeshColliders < Attribute( "NumMeshColliders" ); >;
	RWStructuredBuffer<SignedDistanceField> MeshColliders < Attribute( "MeshColliders" ); >;

	DynamicCombo( D_SHAPE_TYPE, 0..1, Sys( All ) );

	void ApplyUpdates( int DTid )
	{
		VerletPointUpdate update = PointUpdates[DTid];
		int pIndex = update.Index;
		VerletPoint p = Points[pIndex];
		if ( update.ShouldUpdatePosition() )
		{
			p.Position = update.Position;
			p.LastPosition = update.Position;
		}
		if ( update.ShouldUpdateFlags() )
		{
			p.Flags = update.PointFlags;
		}
		Points[pIndex] = p;
	}

	void ApplyTransform( int pIndex, VerletPoint p )
	{
		p.Position += Translation;
		Points[pIndex] = p;
	}

	void ApplyForces( int pIndex, float deltaTime )
	{
		VerletPoint p = Points[pIndex];

		if ( p.IsAnchor() )
		{
			Points[pIndex] = p;
			return;
		}

		float3 temp = p.Position;
		float3 delta = p.Position - p.LastPosition;
		delta *= 1 - (0.95 * deltaTime);
		p.Position += delta;
		p.Position += Gravity * ( deltaTime * deltaTime );
		p.LastPosition = temp;
		Points[pIndex] = p;
	}

	void ApplyRopeSegmentConstraint( int pIndex )
	{
		VerletPoint pCurr = Points[pIndex];

		if ( pIndex < NumPoints - 1 )
		{
			VerletPoint pNext = Points[pIndex + 1];

			float3 delta = pCurr.Position - pNext.Position;
			float distance = length( delta );
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = ( SegmentLength - distance ) / distance * 0.5;
			}
			float3 offset = delta * distanceFactor;

			pCurr.Position += offset;
			Points[pIndex] = pCurr;
		}

		if  ( pIndex > 0 )
		{
			VerletPoint pPrev = Points[pIndex - 1];

			float3 delta = pPrev.Position - pCurr.Position;
			float distance = length( delta );
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = ( SegmentLength - distance ) / distance * 0.5;
			}
			float3 offset = delta * distanceFactor;

			pCurr.Position -= offset;
			Points[pIndex] = pCurr;
		}
	}

	void ApplyRopeConstraints( int pIndex )
	{
		ApplyRopeSegmentConstraint( pIndex );
	}

	void ApplyClothSegmentConstraint( int pIndex )
	{
		VerletPoint pCurr = Points[pIndex];
		int x = pIndex % NumColumns;
		int xSize = NumColumns;
		int y = pIndex / NumColumns;
		int ySize = NumColumns;

		if ( x < xSize - 1 )
		{
			VerletPoint pNext = Points[pIndex + 1];

			float3 delta = pCurr.Position - pNext.Position;
			float distance = length( delta );
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = ( SegmentLength - distance ) / distance * 0.5;
			}
			float3 offset = delta * distanceFactor;

			pCurr.Position += offset;
			Points[pIndex] = pCurr;
		}

		if  ( x > 0 )
		{
			VerletPoint pPrev = Points[pIndex - 1];

			float3 delta = pPrev.Position - pCurr.Position;
			float distance = length( delta );
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = ( SegmentLength - distance ) / distance * 0.5;
			}
			float3 offset = delta * distanceFactor;

			pCurr.Position -= offset;
			Points[pIndex] = pCurr;
		}

		if ( y < ySize - 1 )
		{
			VerletPoint pNext = Points[pIndex + xSize];

			float3 delta = pCurr.Position - pNext.Position;
			float distance = length( delta );
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = ( SegmentLength - distance ) / distance * 0.5;
			}
			float3 offset = delta * distanceFactor;

			pCurr.Position += offset;
			Points[pIndex] = pCurr;
		}

		if ( y > 0 )
		{
			VerletPoint pPrev = Points[pIndex - xSize];

			float3 delta = pPrev.Position - pCurr.Position;
			float distance = length( delta );
			float distanceFactor = 0;
			if ( distance > 0 )
			{
				distanceFactor = ( SegmentLength - distance ) / distance * 0.5;
			}
			float3 offset = delta * distanceFactor;

			pCurr.Position -= offset;
			Points[pIndex] = pCurr;
		}
	}

	void ApplyClothConstraints( int pIndex )
	{
		ApplyClothSegmentConstraint( pIndex );
	}

	void ResolveSphereCollisions( int pIndex )
	{
		for( int i = 0; i < NumSphereColliders; i++ )
		{
			SphereCollider sphere = SphereColliders[i];
			sphere.ResolveCollision( pIndex );
		}
	}

	void ResolveBoxCollisions( int pIndex )
	{
		for ( int i = 0; i < NumBoxColliders; i++ )
		{
			BoxCollider box = BoxColliders[i];
			box.ResolveCollision( pIndex );
		}
	}

	void ResolveCapsuleCollisions( int pIndex )
	{
		for ( int i = 0; i < NumCapsuleColliders; i++ )
		{
			CapsuleCollider capsule = CapsuleColliders[i];
			capsule.ResolveCollision( pIndex );
		}
	}

	void ResolveMeshCollisions( int pIndex )
	{
		for( int i = 0; i < NumMeshColliders; i++ )
		{
			SignedDistanceField sdf = MeshColliders[i];
			ResolveSdfCollision( sdf, pIndex );
		}
	}

	void ResolveCollisions( int pIndex )
	{
		ResolveSphereCollisions( pIndex );
		ResolveBoxCollisions( pIndex );
		ResolveCapsuleCollisions( pIndex );
		ResolveMeshCollisions( pIndex );
	}

	void Simulate( int pIndex, float deltaTime )
	{
		VerletPoint p = Points[pIndex];

		ApplyForces( pIndex, deltaTime );
		
		if ( p.IsAnchor() )
		return;
		
		for ( int i = 0; i < Iterations; i++ )
		{
			#if D_SHAPE_TYPE == 0
				ApplyRopeConstraints( pIndex );
			#elif D_SHAPE_TYPE == 1
				ApplyClothConstraints( pIndex );
			#endif
			
			GroupMemoryBarrierWithGroupSync();		
			ResolveCollisions( pIndex );
		}
	}

	#if D_SHAPE_TYPE == 1
		[numthreads( 32, 32, 1 )]
	#else
		[numthreads( 1024, 1, 1 )]
	#endif
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		int pIndex = Convert2DIndexTo1D( id.xy, uint2( NumPoints / NumColumns, NumColumns ) );

		// TODO: Apply updates only from thread group 0,0
		if ( pIndex < NumPointUpdates )
		{
			ApplyUpdates( pIndex );
		}
		GroupMemoryBarrierWithGroupSync();
		
		VerletPoint p = Points[pIndex];
		if ( p.IsRopeLocal() )
		{
			ApplyTransform( pIndex, p );
		}
		GroupMemoryBarrierWithGroupSync();

		float totalTime = min( DeltaTime, MaxTimeStepPerUpdate );
		for( int i = 0; i < 50; i++ )
		{
			float deltaTime = min( TimeStepSize, totalTime );
			Simulate( pIndex, deltaTime );
			totalTime -= TimeStepSize;

			if ( totalTime < 0 )
				break;
		}

		GroupMemoryBarrierWithGroupSync();
	}
}