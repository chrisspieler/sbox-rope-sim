namespace Duccsoft;

public class SimulationData : IDataSize
{
	public SimulationData( PhysicsWorld physics, VerletPoint[] points, Vector2Int resolution, float segmentLength )
	{
		Physics = physics;
		Points = points;
		PointGridDims = resolution;
		SegmentLength = segmentLength;

		GpuData = new( this );
		GpuData.InitializeGpu();
	}

	public GpuSimulationData GpuData { get; }
	public int SimulationIndex { get; set; } = -1;
	public RealTimeSince? LastTick { get; internal set; }
	public Vector3 Gravity { get; set; } = Vector3.Down * 800f;
	public Vector3? FixedFirstPosition { get; private set; }
	public Vector3? FixedLastPosition { get; private set; }
	public Vector3 FixedColumnCrossDirection
	{
		get
		{
			if ( FixedFirstPosition is not Vector3 firstPos || FixedLastPosition is not Vector3 lastPos )
				return Vector3.Forward;

			Vector3 dir = ( lastPos - firstPos ).Normal;
			return dir.Cross( Vector3.Up );
		}
	}
	public Vector3 FirstPosition
	{
		get
		{
			if ( Points.Length > 0 )
				return Points[0].Position;

			return Vector3.Zero;
		}
	}
	public Vector3 LastPosition
	{
		get
		{
			if ( Points.Length > 0 )
				return Points[^1].Position;

			return Vector3.Down * 32f;
		}
	}

	public int RopeIndex { get; set; }
	public VerletPoint[] Points { get; private set; }
	public bool ShouldCopyToGpu { get; set; }
	public float SegmentLength { get; }
	public Vector2Int PointGridDims { get; }
	public Transform Transform { get; set; }
	public Transform LastTransform { get; set; }
	/// <summary>
	/// Based on Transform and LastTransform, how much has the entire sim shifted since the last update?
	/// </summary>
	public Vector3 Translation => Transform.Position - LastTransform.Position;
	public int Iterations { get; set; } = 80;
	public float AnchorMaxDistanceFactor { get; set; } = 0;
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
	/// Worldspace bounds of the VerletPoints, used for culling and broad phase collision detection.
	/// </summary>
	public BBox Bounds { get; set; }
	public CollisionSnapshot Collisions { get; set; } = new();

	public long DataSize => throw new NotImplementedException();

	public Vector2Int IndexToPointCoord( int index )
	{
		return new Vector2Int
		{
			x = index % PointGridDims.x - 1,
			y = index / PointGridDims.x,
		};
	}
	public int PointCoordToIndex( int x, int y ) => y * PointGridDims.x + x;
	public int PointCoordToIndex( Vector2Int pointCoord ) => pointCoord.y * PointGridDims.x + pointCoord.x;

	public void AnchorToStart( Vector3? firstPos )
	{
		if ( firstPos == FixedFirstPosition )
			return;

		AnchorToNth( firstPos, 0, false, true, false );

		FixedFirstPosition = firstPos;
		ShouldCopyToGpu = true;
	}

	public void AnchorToEnd( Vector3? pos )
	{
		if ( pos == FixedLastPosition )
			return;

		AnchorToNth( pos, PointGridDims.x - 1, false, false, true );

		FixedLastPosition = pos;
		ShouldCopyToGpu = true;
	}

	private void AnchorToNth( Vector3? startPos, int n, bool anchorMiddle, bool startLocal, bool endLocal )
	{
		Ray yRay = new( startPos ?? Vector3.Zero, FixedColumnCrossDirection );
		for ( int y = 0; y < PointGridDims.y; y++ )
		{
			int i = y * PointGridDims.x + n;
			if ( startPos is null )
			{
				Points[i] = Points[i] with 
				{ 
					IsAnchor = false, 
					IsRopeLocal = false, 
				};
				continue;
			}

			bool isYMiddle = y > 0 && y < PointGridDims.y - 1;
			// bool isYMiddle = y > 0 && y < PointGridDims.y - 1;
			bool isAnchor = anchorMiddle || !isYMiddle;
			bool isRopeLocal = false;
			if ( startLocal && ( anchorMiddle || n == 0 ) )
			{
				isRopeLocal = true;
			}
			if ( endLocal && ( anchorMiddle || n == PointGridDims.x - 1 ) )
			{ 
				isRopeLocal = true;
			}

			Vector3 anchorPos = yRay.Project( y * SegmentLength );
			Points[i] = Points[i] with
			{
				Position = anchorPos,
				LastPosition = anchorPos,
				IsAnchor = isAnchor,
				IsRopeLocal = isRopeLocal,
			};
		}
	}

	public void SetPointPosition( Vector2Int coord, Vector3 position )
		=> SetPointPosition( PointCoordToIndex( coord ), position );

	private void SetPointPosition( int i, Vector3 position )
	{
		VerletPoint p = Points[i] with 
		{ 
			Position = position, 
			LastPosition = position 
		};
		Points[i] = p;
		GpuData.QueuePointPositionUpdate( i, position );
	}

	public void SetPointFlags( Vector2Int coord, VerletPointFlags flags )
		=> SetPointFlags( PointCoordToIndex( coord ), flags );

	private void SetPointFlags( int i, VerletPointFlags flags )
	{
		VerletPoint p = Points[i] with
		{
			Flags = flags,
		};
		Points[i] = p;
		GpuData.QueuePointFlagUpdate( i, flags );
	}

	public void RecalculateBounds()
	{
		BBox collisionBounds = default;

		for ( int i = 0; i < Points.Length; i++ )
		{
			var point = Points[i];
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

		Bounds = collisionBounds;
	}

	internal void PostSimulationCleanup()
	{
		// Assume that if we just now ticked the simulation, all pending updates and collisions were applied.
		GpuData.ClearPendingPointUpdates();
		Collisions.Clear();

		// Prepare for next delta time and translation.
		LastTick = 0;
		LastTransform = Transform;
	}
}
