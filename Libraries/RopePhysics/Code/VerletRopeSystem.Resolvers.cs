namespace Duccsoft;

public partial class VerletRopeSystem
{
	private static void ResolveCollisions( RopeSimulationData simData )
	{
		ResolveSphereCollisions( simData );
		ResolveBoxCollisions( simData );
		ResolveCapsuleCollisions( simData );
		ResolveMeshCollisions( simData );
	}

	private static void ResolveSphereCollisions( RopeSimulationData simData )
	{
		foreach ( var ci in simData.Collisions.SphereColliders.Values )
		{
			var radius = ci.Radius;

			foreach ( var pointId in ci.CollidingPoints )
			{
				var point = simData.Points[pointId];
				var pointPos = ci.Transform.PointToLocal( point.Position );
				var distance = Vector3.DistanceBetween( ci.Center, pointPos );
				if ( distance - radius > simData.Radius )
					continue;

				var direction = (pointPos - ci.Center).Normal;
				var hitPosition = ci.Center + direction * ( radius + simData.Radius );
				hitPosition = ci.Transform.PointToWorld( hitPosition );
				simData.Points[pointId] = point with { Position = hitPosition };
			}
		}
	}

	private static void ResolveBoxCollisions( RopeSimulationData simData )
	{
		foreach ( var ci in simData.Collisions.BoxColliders.Values )
		{
			foreach ( var pointId in ci.CollidingPoints )
			{
				var point = simData.Points[pointId];
				var pointPos = ci.Transform.PointToLocal( point.Position );
				var halfSize = ci.Size * 0.5f;
				var scale = ci.Transform.Scale;
				// Figure out how deep the point is inside in the box.
				var pointDepth = halfSize - pointPos.Abs();
				var radius = simData.Radius;
				// If the point is entirely outside one of the axes, continue.
				if ( pointDepth.x <= -radius || pointDepth.y <= -radius || pointDepth.z <= -radius )
					continue;

				var pointScaled = pointDepth * scale;
				var signs = new Vector3( MathF.Sign( pointPos.x ), MathF.Sign( pointPos.y ), MathF.Sign( pointPos.z ) );
				if ( pointScaled.x < pointScaled.y && pointScaled.x < pointScaled.z )
				{
					pointPos.x = halfSize.x * signs.x + radius * signs.x;
				}
				else if ( pointScaled.y < pointScaled.x && pointScaled.y < pointScaled.z )
				{
					pointPos.y = halfSize.y * signs.y + radius * signs.y;
				}
				else
				{
					pointPos.z = halfSize.z * signs.z + radius * signs.z;
				}

				var hitPos = ci.Transform.PointToWorld( pointPos );
				simData.Points[pointId] = point with { Position = hitPos };
			}
		}
	}

	private static void ResolveCapsuleCollisions( RopeSimulationData simData )
	{
		foreach ( var ci in simData.Collisions.CapsuleColliders.Values )
		{
			// Adapted from: https://iquilezles.org/articles/distfunctions/
			float CapsuleSdf( Vector3 p )
			{
				var pa = p - ci.Start;
				var ba = ci.End - ci.Start;
				var h = MathX.Clamp( pa.Dot( ba ) / ba.Dot( ba ), 0f, 1f );
				return (pa - ba * h).Length - ci.Radius;
			}

			foreach ( var collision in ci.CollidingPoints )
			{
				var point = simData.Points[collision];
				var localPos = ci.Transform.PointToLocal( point.Position );
				var sdf = CapsuleSdf( localPos );
				if ( sdf > simData.Radius )
					continue;

				var xOffset = new Vector3( 0.0001f, 0f, 0f );
				var yOffset = new Vector3( 0f, 0.0001f, 0f );
				var zOffset = new Vector3( 0f, 0f, 0.0001f );
				var gradient = new Vector3()
				{
					x = CapsuleSdf( localPos + xOffset ) - CapsuleSdf( localPos - xOffset ),
					y = CapsuleSdf( localPos + yOffset ) - CapsuleSdf( localPos - yOffset ),
					z = CapsuleSdf( localPos + zOffset ) - CapsuleSdf( localPos - zOffset ),
				};
				gradient = gradient.Normal;
				var currentPos = localPos + gradient * (-sdf + simData.Radius );
				currentPos = ci.Transform.PointToWorld( currentPos );
				simData.Points[collision] = point with { Position = currentPos };
			}
		}
	}

	private static void ResolveMeshCollisions( RopeSimulationData simData )
	{
		foreach ( var ci in simData.Collisions.MeshColliders.Values )
		{
			foreach ( var collision in ci.CollidingPoints )
			{
				var point = simData.Points[collision];
				var currentPos = ci.Transform.PointToLocal( point.Position );
				var currentClosestPoint = ci.Sdf.Bounds.ClosestPoint( currentPos );
				var currentDistance = currentPos.Distance( currentClosestPoint );
				if ( currentDistance >= simData.Radius )
					continue;

				var texel = ci.Sdf.PositionToTexel( currentPos );
				var sdMesh = ci.Sdf[texel];
				if ( sdMesh >= simData.Radius )
					continue;

				var gradient = ci.Sdf.CalculateGradient( texel );
				currentPos += gradient * (-sdMesh + simData.Radius );
				currentPos = ci.Transform.PointToWorld( currentPos );
				simData.Points[collision] = point with { Position = currentPos };
			}
		}
	}
}
