namespace Duccsoft;

public static class TriangleExtensions
{
	public static BBox GetBounds( this Triangle tri )
	{
		Vector3 mins = tri.A.ComponentMin( tri.B );
		mins = mins.ComponentMin( tri.C );

		Vector3 maxs = tri.A.ComponentMax( tri.B );
		maxs = maxs.ComponentMax( tri.C );

		return new BBox( mins, maxs );
	}

	public static bool IntersectsAABB( this Triangle tri, BBox bbox )
	{
		float Max( float a, float b, float c ) => MathF.Max( MathF.Max( a, b ), c );
		float Min( float a, float b, float c ) => MathF.Min( MathF.Min( a, b ), c );

		Vector3 v0 = tri.A;
		Vector3 v1 = tri.B;
		Vector3 v2 = tri.C;

		Vector3 c = bbox.Center;
		Vector3 e = bbox.Extents;

		v0 -= c;
		v1 -= c;
		v2 -= c;

		Vector3 f0 = v1 - v0;
		Vector3 f1 = v2 - v1;
		Vector3 f2 = v0 - v2;

		Vector3 u0 = Vector3.Forward;
		Vector3 u1 = Vector3.Left;
		Vector3 u2 = Vector3.Up;

		Vector3 u0f0 = u0.Cross( f0 );
		Vector3 u0f1 = u0.Cross( f1 );
		Vector3 u0f2 = u0.Cross( f2 );

		Vector3 u1f0 = u1.Cross( f0 );
		Vector3 u1f1 = u1.Cross( f1 );
		Vector3 u1f2 = u1.Cross( f2 );

		Vector3 u2f0 = u2.Cross( f0 );
		Vector3 u2f1 = u2.Cross( f1 );
		Vector3 u2f2 = u2.Cross( f2 );

		// SAT test for the given axis.
		bool TestAxis( Vector3 axis )
		{
			float p0 = Vector3.Dot( v0, axis );
			float p1 = Vector3.Dot( v1, axis );
			float p2 = Vector3.Dot( v2, axis );

			float r =	e.x * Math.Abs( u0.Dot( axis ) ) +
						e.y * Math.Abs(	u1.Dot( axis ) ) +
						e.z * Math.Abs(	u2.Dot( axis ) );

			return r < MathF.Max( -Max( p0, p1, p2 ), Min( p0, p1, p2 ) );
		}

		if ( TestAxis( u0f0 ) ) return false;
		if ( TestAxis( u0f1 ) ) return false;
		if ( TestAxis( u0f2 ) ) return false;
		if ( TestAxis( u1f0 ) ) return false;
		if ( TestAxis( u1f1 ) ) return false;
		if ( TestAxis( u1f2 ) ) return false;
		if ( TestAxis( u2f0 ) ) return false;
		if ( TestAxis( u2f1 ) ) return false;
		if ( TestAxis( u2f2 ) ) return false;

		if ( TestAxis( u0 ) ) return false;
		if ( TestAxis( u1 ) ) return false;
		if ( TestAxis( u2 ) ) return false;

		if ( TestAxis( tri.Normal ) ) return false;

		return true;
	}
}
