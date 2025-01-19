namespace Duccsoft;

public class SimulationData
{
	public SimulationData( PhysicsWorld physics, VerletPoint[] points, int numColumns, float segmentLength )
	{
		Physics = physics;
		CpuPoints = points;
		PointGridDims = new Vector2Int( points.Length / numColumns, numColumns );
		SegmentLength = segmentLength;
	}

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
	public bool CpuPointsAreDirty { get; private set; }
	public float SegmentLength { get; }
	public Vector2Int PointGridDims { get; }
	public Transform Transform { get; set; }
	public Transform LastTransform { get; set; }
	/// <summary>
	/// Based on Transform and LastTransform, how much has the entire sim shifted since the last update?
	/// </summary>
	public Vector3 Translation => Transform.Position - LastTransform.Position;
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
	/// Worldspace bounds of the VerletPoints, used for culling and broad phase collision detection.
	/// </summary>
	public BBox Bounds { get; set; }
	public CollisionSnapshot Collisions { get; set; } = new();

	// Gpu inputs
	internal GpuBuffer<VerletPoint> GpuPoints { get; set; }
	public int PendingPointUpdates => PointUpdateQueue.Count;
	internal Dictionary<int, VerletPointUpdate> PointUpdateQueue { get; } = [];
	internal GpuBuffer<VerletPointUpdate> GpuPointUpdates { get; set; }

	// Gpu outputs
	internal GpuBuffer<VerletVertex> ReadbackVertices { get; set; }
	internal GpuBuffer<uint> ReadbackIndices { get; set; }


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

	public void ClearPendingPointUpdates() => PointUpdateQueue.Clear();

	public void StorePointsToGpu()
	{
		if ( GpuPoints is null )
		{
			InitializeGpu();
			return;
		}
		PointUpdateQueue.Clear();
		GpuPoints.SetData( CpuPoints );
		CpuPointsAreDirty = false;
	}

	public void InitializeGpu()
	{
		PointUpdateQueue.Clear();
		DestroyGpuData();
		GpuPoints = new GpuBuffer<VerletPoint>( CpuPoints.Length );
		GpuPoints.SetData( CpuPoints );
		var vertexCount = PointGridDims.y > 1 ? CpuPoints.Length : CpuPoints.Length + 2;
		ReadbackVertices = new GpuBuffer<VerletVertex>( vertexCount, GpuBuffer.UsageFlags.Vertex | GpuBuffer.UsageFlags.Structured );
		var readbackBuffer = new GpuBuffer<VerletBounds>( 1, GpuBuffer.UsageFlags.Structured );
		var initialBounds = new VerletBounds()
		{
			Mins = new Vector4( float.PositiveInfinity ),
			Maxs = new Vector4( float.NegativeInfinity ),
		};
		readbackBuffer.SetData( [initialBounds] );
		int numQuads = (PointGridDims.x - 1) * (PointGridDims.y - 1);
		// Two tris per quad, three indices per tri.
		var numIndices = 6 * ( numQuads + PointGridDims.x - 1 );
		numIndices = Math.Max( 6, numIndices );
		ReadbackIndices = new GpuBuffer<uint>( numIndices, GpuBuffer.UsageFlags.Index | GpuBuffer.UsageFlags.Structured );

		Bounds = BBox.FromPositionAndSize( Transform.Position, 128f );
	}

	public void LoadPointsFromGpu()
	{
		if ( !ReadbackVertices.IsValid() )
			return;

		PointUpdateQueue.Clear();

		using var scope = PerfLog.Scope( $"Vertex readback with {CpuPoints.Length} points and {Iterations} iterations" );

		if ( CpuPoints.Length != GpuPoints.ElementCount )
		{
			CpuPoints = new VerletPoint[GpuPoints.ElementCount];
		}
		
		var vertices = new VerletVertex[ReadbackVertices.ElementCount];
		ReadbackVertices.GetData( vertices );
		int indexOffset = 0;
		int indexStart = 0;
		int indexMax = vertices.Length;
		if ( PointGridDims.y == 1 )
		{
			indexOffset = -1;
			indexStart = 1;
			indexMax = vertices.Length - 1;
		}
		for ( int i = indexStart; i < indexMax; i++ )
		{
			var pIndex = i + indexOffset;

			var vtx = vertices[i];
			var p = CpuPoints[pIndex];
			CpuPoints[pIndex] = p with
			{
				Position = vtx.Position,
				LastPosition = vtx.Position - vtx.Tangent0,
			};
		}
		CpuPointsAreDirty = false;
	}

	public void DestroyGpuData()
	{
		GpuPoints?.Dispose();
		PointUpdateQueue.Clear();
		GpuPoints = null;
		GpuPointUpdates?.Dispose();
		GpuPointUpdates = null;
	}

	public void AnchorToStart( Vector3? firstPos )
	{
		if ( firstPos == FixedFirstPosition )
			return;

		AnchorToNth( firstPos, 0, false, true, false );

		FixedFirstPosition = firstPos;
		CpuPointsAreDirty = true;
	}

	public void AnchorToEnd( Vector3? pos )
	{
		if ( pos == FixedLastPosition )
			return;

		AnchorToNth( pos, PointGridDims.x - 1, false, false, true );

		FixedLastPosition = pos;
		CpuPointsAreDirty = true;
	}

	private void AnchorToNth( Vector3? startPos, int n, bool anchorMiddle, bool startLocal, bool endLocal )
	{
		Ray yRay = new( startPos ?? Vector3.Zero, FixedColumnCrossDirection );
		for ( int y = 0; y < PointGridDims.y; y++ )
		{
			int i = y * PointGridDims.x + n;
			if ( startPos is null )
			{
				CpuPoints[i] = CpuPoints[i] with 
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
			CpuPoints[i] = CpuPoints[i] with
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
		VerletPoint p = CpuPoints[i] with 
		{ 
			Position = position, 
			LastPosition = position 
		};
		CpuPoints[i] = p;
		PointUpdateQueue[i] = VerletPointUpdate.Position( i, position );
	}

	public void SetPointFlags( Vector2Int coord, VerletPointFlags flags )
		=> SetPointFlags( PointCoordToIndex( coord ), flags );

	private void SetPointFlags( int i, VerletPointFlags flags )
	{
		VerletPoint p = CpuPoints[i] with
		{
			Flags = flags,
		};
		CpuPoints[i] = p;
		PointUpdateQueue[i] = VerletPointUpdate.Flags( i, flags );
	}

	public void RecalculateCpuPointBounds()
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

		Bounds = collisionBounds;
	}
}
