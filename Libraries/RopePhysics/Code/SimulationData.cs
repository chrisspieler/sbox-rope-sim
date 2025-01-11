namespace Duccsoft;

public class SimulationData
{
	public SimulationData( PhysicsWorld physics, VerletPoint[] points, int numColumns, float segmentLength )
	{
		Physics = physics;
		CpuPoints = points;
		PointGridDims = new Vector2Int( points.Length / numColumns, numColumns );
		SegmentLength = segmentLength;

		InitializeGpu();
	}

	
	public Vector3 Gravity { get; set; } = Vector3.Down * 800f;
	public Vector3? FixedFirstPosition { get; set; }
	public Vector3? FixedLastPosition { get; set; }
	public Vector3 FirstPosition
	{
		get
		{
			if ( CpuPoints.Length > 0 )
				return CpuPoints[0].Position;

			return Vector3.Zero;
		}
	}
	public Vector3 LastPosition
	{
		get
		{
			if ( CpuPoints.Length > 0 )
				return CpuPoints[^1].Position;

			return Vector3.Down * 32f;
		}
	}

	public VerletPoint[] CpuPoints { get; private set; }
	public float SegmentLength { get; }
	public Vector2Int PointGridDims { get; }

	public int Iterations { get; set; } = 80;
	public float Radius { get; set; } = 1f;

	[ConVar( "verlet_collision_mdf_enable" )]
	public static bool UseMeshDistanceFields { get; set; } = true;

	public PhysicsWorld Physics { get; init; }
	public TagSet CollisionInclude { get; set; } = [];
	public TagSet CollisionExclude { get; set; } = ["noblockrope"];
	/// <summary>
	/// A bias applied to CollisionRadius that is used when finding colliders that are near to
	/// a rope node. Higher values will reduce the chance of clipping.
	/// </summary>
	public float CollisionSearchRadius { get; set; } = 10f;
	/// <summary>
	/// The total area to search for collisions, encompassing the entire rpoe.
	/// </summary>
	public BBox CollisionBounds { get; set; }
	public CollisionSnapshot Collisions { get; set; } = new();

	public GpuBuffer<VerletPoint> GpuPoints { get; set; }
	public GpuBuffer<VerletVertex> ReadbackVertices { get; set; }

	public void StorePointsToGpu()
	{
		if ( GpuPoints is null )
		{
			InitializeGpu();
			return;
		}
		GpuPoints.SetData( CpuPoints );
	}

	public void InitializeGpu()
	{
		DestroyGpuData();
		GpuPoints = new GpuBuffer<VerletPoint>( CpuPoints.Length );
		GpuPoints.SetData( CpuPoints );
		ReadbackVertices = new GpuBuffer<VerletVertex>( CpuPoints.Length + 2, GpuBuffer.UsageFlags.Vertex | GpuBuffer.UsageFlags.Structured );
	}

	public void LoadPointsFromGpu()
	{
		if ( !ReadbackVertices.IsValid() )
			return;

		using var scope = PerfLog.Scope( $"Vertex readback with {CpuPoints.Length} points and {Iterations} iterations" );

		if ( CpuPoints.Length != GpuPoints.ElementCount )
		{
			CpuPoints = new VerletPoint[GpuPoints.ElementCount];
		}
		
		var vertices = new VerletVertex[ReadbackVertices.ElementCount];
		ReadbackVertices.GetData( vertices );
		for ( int i = 1; i < vertices.Length - 1; i++ )
		{
			if ( i == 0 )
				continue;

			var pIndex = i - 1;

			var vtx = vertices[i];
			var p = CpuPoints[pIndex];
			CpuPoints[pIndex] = p with
			{
				Position = vtx.Position,
				LastPosition = vtx.Position - vtx.Tangent0,
			};
		}
	}

	public void DestroyGpuData()
	{
		GpuPoints?.Dispose();
		GpuPoints = null;
	}

	public void UpdateAnchors()
	{
		if ( CpuPoints.Length < 1 )
			return;

		var anchorFirst = FixedFirstPosition is not null;
		SetAnchor( 0, anchorFirst );
		var anchorLast = FixedLastPosition is not null;
		SetAnchor( CpuPoints.Length - 1, anchorLast );
	}
	
	public void SetAnchor( int index, bool isAnchor )
	{
		if ( CpuPoints.Length < 1 || index < 0 || index >= CpuPoints.Length )
			return;

		var p = CpuPoints[index];
		p.Flags = p.Flags.WithFlag( VerletPointFlags.Anchor, isAnchor );
		CpuPoints[index] = p;
	}

	public void RecalculatePointBounds()
	{
		BBox collisionBounds = default;

		for ( int i = 0; i < CpuPoints.Length; i++ )
		{
			var point = CpuPoints[i];
			var pointBounds = BBox.FromPositionAndSize( point.Position, CollisionSearchRadius );
			if ( i == 0 )
			{
				collisionBounds = pointBounds;
			}
			else
			{
				collisionBounds = collisionBounds.AddBBox( pointBounds );
			}
		}

		CollisionBounds = collisionBounds;
	}
}
