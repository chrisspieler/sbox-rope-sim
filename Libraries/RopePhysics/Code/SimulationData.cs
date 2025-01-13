﻿using static Sandbox.VertexLayout;

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
	public GpuBuffer<int> ReadbackIndices { get; set; }

	public void StorePointsToGpu()
	{
		if ( GpuPoints is null )
		{
			InitializeGpu();
			return;
		}
		GpuPoints.SetData( CpuPoints );
		CpuPointsAreDirty = false;
	}

	public void InitializeGpu()
	{
		DestroyGpuData();
		GpuPoints = new GpuBuffer<VerletPoint>( CpuPoints.Length );
		GpuPoints.SetData( CpuPoints );
		var vertexCount = PointGridDims.y > 1 ? CpuPoints.Length : CpuPoints.Length + 2;
		ReadbackVertices = new GpuBuffer<VerletVertex>( vertexCount, GpuBuffer.UsageFlags.Vertex | GpuBuffer.UsageFlags.Structured );
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
		GpuPoints = null;
	}

	public void AnchorToStart( Vector3? firstPos )
	{
		if ( firstPos == FixedFirstPosition )
			return;

		AnchorToNth( firstPos, 0 );

		FixedFirstPosition = firstPos;
		CpuPointsAreDirty = true;
	}

	public void AnchorToEnd( Vector3? pos )
	{
		if ( pos == FixedLastPosition )
			return;

		AnchorToNth( pos, PointGridDims.x - 1 );

		FixedLastPosition = pos;
		CpuPointsAreDirty = true;
	}

	private void AnchorToNth( Vector3? startPos, int n )
	{
		Ray yRay = new( startPos ?? Vector3.Zero, FixedColumnCrossDirection );
		for ( int y = 0; y < PointGridDims.y; y++ )
		{
			int i = y * PointGridDims.x + n;
			if ( startPos is null )
			{
				CpuPoints[i] = CpuPoints[i] with { IsAnchor = false };
				continue;
			}

			Vector3 anchorPos = yRay.Project( y * SegmentLength );
			CpuPoints[i] = CpuPoints[i] with
			{
				Position = anchorPos,
				LastPosition = anchorPos,
				IsAnchor = true,
			};
		}
	}


	private void SetPointPosition( int i, Vector3 position )
	{
		CpuPoints[i] = CpuPoints[i] with { Position = position, LastPosition = position };
		// TODO: Queue a store to GPU because the CpuPoints are dirty
	}

	public void SetPointPosition( Vector2Int coord, Vector3 position )
	{
		var i = coord.y * PointGridDims.x + coord.x;
		SetPointPosition( i, position );
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
