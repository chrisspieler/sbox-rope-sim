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
	float PointRadius < Attribute( "PointRadius" ); Default( 1.0 ); >;
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
			if ( dist - Radius >= PointRadius )
				return;

			float3 dir = normalize( pPositionOs - Center );
			float3 hitPositionOs = Center + dir * ( PointRadius + Radius );
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
			float radius = PointRadius;

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
			if ( sd >= PointRadius )
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
			pPositionOs += gradient * ( -sd + PointRadius );
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
		if ( sd > PointRadius )
			return;

		pPositionOs += gradient * ( -sd + PointRadius );
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
	DynamicCombo( D_COLLISION, 0..1, Sys( All ) );

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
			p.LastPosition = p.Position;
			Points[pIndex] = p;
			return;
		}

		float3 temp = p.Position;
		float3 delta = p.Position - p.LastPosition;
		delta *= 1 - ( 0.95 * deltaTime );
		p.Position += delta;
		p.Position += Gravity * ( deltaTime * deltaTime );
		p.LastPosition = temp;
		Points[pIndex] = p;
	}

	void ConstrainPoints( int pIndex, int qIndex, float segmentLength )
	{
		VerletPoint p = Points[pIndex];
		VerletPoint q = Points[qIndex];

		float3 delta = p.Position - q.Position;
		float distance = length( delta );
		float distanceFactor = 0;
		if ( distance > 0 )
		{
			distanceFactor = ( segmentLength - distance ) / distance * 0.5;
		}
		float3 offset = delta * distanceFactor;
		
		if ( !p.IsAnchor() )
		{
			p.Position += offset;
			Points[pIndex] = p;
		}
		if ( !q.IsAnchor() )
		{
			q.Position -= offset;
			Points[qIndex] = q;
		}
	}

	void ApplyRopeSegmentConstraints( int pIndex )
	{
		if ( pIndex >= NumPoints - 1 )
			return;

		GroupMemoryBarrierWithGroupSync();
		if ( pIndex % 2 == 0 )
		{
			ConstrainPoints( pIndex, pIndex + 1, SegmentLength );
		}
		GroupMemoryBarrierWithGroupSync();
		if ( pIndex % 2 == 1 )
		{
			ConstrainPoints( pIndex, pIndex + 1, SegmentLength );
		}
	}

	void ApplyClothSegmentConstraints( int pIndex )
	{
		VerletPoint pCurr = Points[pIndex];
		uint2 coords = Convert1DIndexTo2D( pIndex, NumColumns );
		int x = coords.x;
		int y = coords.y;
		int xMax = NumColumns - 1;
		int yMax = NumColumns - 1;

		

		if ( x < NumColumns - 1 )
		{
			int qIndex = pIndex + 1;
			GroupMemoryBarrierWithGroupSync();
			if ( pIndex % 2 == 0 )
			{
				ConstrainPoints( pIndex, qIndex, SegmentLength );
			}
			GroupMemoryBarrierWithGroupSync();
			if ( pIndex % 2 == 1 )
			{
				ConstrainPoints( pIndex, qIndex, SegmentLength );
			}
		}

		if ( y < NumColumns - 1 )
		{
			int qIndex = pIndex + NumColumns;
			GroupMemoryBarrierWithGroupSync();
			if ( y % 2 == 0 )
			{
				ConstrainPoints( pIndex, qIndex, SegmentLength );
			}
			GroupMemoryBarrierWithGroupSync();
			if ( y % 2 == 1 )
			{
				ConstrainPoints( pIndex, qIndex, SegmentLength );
			}
		}

		if ( x < NumColumns - 1 && y < NumColumns - 1 )
		{
			int qIndex = pIndex + 1 + NumColumns;
			float length = distance( Points[pIndex].Position, Points[qIndex].Position ); 
			GroupMemoryBarrierWithGroupSync();
			if ( x % 2 == 0 )
			{
				ConstrainPoints( pIndex, qIndex, length );
			}
			GroupMemoryBarrierWithGroupSync();
			if ( x % 2 == 1 )
			{
				ConstrainPoints( pIndex, qIndex, length );
			}
		}

		if ( x > 0 && y > 0 )
		{
			int qIndex = pIndex - 1 - NumColumns;
			float length = distance( Points[pIndex].Position, Points[qIndex].Position ); 
			GroupMemoryBarrierWithGroupSync();
			if ( x % 2 == 0 )
			{
				ConstrainPoints( pIndex, qIndex, length );
			}
			GroupMemoryBarrierWithGroupSync();
			if ( x % 2 == 1 )
			{
				ConstrainPoints( pIndex, qIndex, length );
			}
		}
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
		
		for ( int i = 0; i < Iterations; i++ )
		{
			#if D_SHAPE_TYPE == 1
				ApplyClothSegmentConstraints( pIndex );
			#else
				ApplyRopeSegmentConstraints( pIndex );
			#endif

				GroupMemoryBarrierWithGroupSync();
			#if D_COLLISION
				ResolveCollisions( pIndex );
			#endif
		}
	}

	#if D_SHAPE_TYPE == 1
		[numthreads( 32, 32, 1 )]
	#else
		[numthreads( 1024, 1, 1 )]
	#endif
	void MainCs( uint3 id : SV_DispatchThreadID )
	{
		#if D_SHAPE_TYPE == 1
			int pIndex = Convert2DIndexTo1D( id.xy, NumColumns );
		#else
			int pIndex = id.x;
		#endif

		if ( pIndex >= NumPoints )
			return;

		// TODO: Apply updates only from thread group 0,0
		if ( pIndex < NumPointUpdates )
		{
			ApplyUpdates( pIndex );
		}
		
		VerletPoint p = Points[pIndex];
		if ( p.IsRopeLocal() )
		{
			ApplyTransform( pIndex, p );
		}

		float totalTime = min( DeltaTime, MaxTimeStepPerUpdate );
		float timeStepSize = max( TimeStepSize, 0.01 );
		while( totalTime >= 0 )
		{
			float deltaTime = min( timeStepSize, totalTime );
			Simulate( pIndex, deltaTime );
			totalTime -= timeStepSize;
		}
	}
}