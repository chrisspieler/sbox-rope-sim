namespace Duccsoft;

public partial class GpuSimulationData : IDataSize
{
	public GpuSimulationData( SimulationData cpuData )
	{
		ArgumentNullException.ThrowIfNull( cpuData );

		CpuData = cpuData;
		VerletIntegrationCs ??= new ComputeShader( "shaders/physics/verlet_integration_cs.shader" );
		VerletRopeMeshCs ??= new ComputeShader( "shaders/mesh/verlet_rope_mesh_cs.shader" );
		VerletClothMeshCs ??= new ComputeShader( "shaders/mesh/verlet_cloth_mesh_cs.shader" );
		MeshBoundsCs ??= new ComputeShader( "shaders/mesh/mesh_bounds_cs.shader" );
	}

	public SimulationData CpuData { get; }
	public long DataSize
	{
		get
		{
			long dataSize = 0;

			// Assume for now that zero or one collision snapshots are used per GPU simulation.
			if ( CpuData.Collisions is CollisionSnapshot snapshot )
			{
				dataSize += snapshot.DataSize;
			}

			if ( _gpuPoints is not null )
			{
				dataSize += _gpuPoints.ElementCount * _gpuPoints.ElementSize;
			}

			if ( _gpuPointUpdates is not null )
			{
				dataSize += _gpuPointUpdates.ElementCount * _gpuPointUpdates.ElementSize;
			}

			if ( _readbackVertices is not null )
			{
				dataSize += _readbackVertices.ElementCount * _readbackVertices.ElementSize;
			}

			if ( _readbackIndices is not null )
			{
				dataSize += _readbackIndices.ElementCount * _readbackIndices.ElementSize;
			}
			
			return dataSize;
		}
	}

	#region Simulation State
	public int PointCount => CpuData.Points.Length;
	public Vector2Int PointGridSize => CpuData.PointGridDims;
	public int RowCount => PointGridSize.x;
	public int ColumnCount => PointGridSize.y;
	public bool ShouldCopyFromCpu => CpuData.ShouldCopyToGpu;

	private VerletPoint[] CpuPoints => CpuData.Points;

	internal GpuBuffer<VerletPoint> GpuPoints
	{
		get
		{
			if ( !_gpuPoints.IsValid() )
			{
				_gpuPoints = new( PointCount );
			}
			return _gpuPoints;
		}
	}

	public void StorePointsToGpu()
	{
		if ( !GpuPoints.IsValid() )
		{
			InitializeGpu();
			return;
		}
		PointUpdateQueue.Clear();
		GpuPoints.SetData( CpuPoints );
		CpuData.ShouldCopyToGpu = false;
	}

	public void LoadPointsFromGpu()
	{
		if ( !ReadbackVertices.IsValid() || !GpuPoints.IsValid() )
			return;

		PointUpdateQueue.Clear();

		using var scope = PerfLog.Scope( $"Vertex readback with {PointCount} points and {CpuData.Iterations} iterations" );

		var vertices = new RopeVertex[ReadbackVertices.ElementCount];
		ReadbackVertices.GetData( vertices );
		int indexOffset = 0;
		int indexStart = 0;
		int length = vertices.Length;
		if ( ColumnCount == 1 )
		{
			indexOffset = -1;
			indexStart = 1;
			length = vertices.Length - 1;
		}

		for ( int i = indexStart; i < length; i++ )
		{
			var pIndex = i + indexOffset;

			// Sanity check.
			if ( pIndex >= CpuPoints.Length )
				break;

			var vtx = vertices[i];
			var p = CpuPoints[pIndex];
			CpuPoints[pIndex] = p with
			{
				Position = vtx.Position,
				LastPosition = vtx.Position - vtx.Tangent0,
			};
		}
		CpuData.ShouldCopyToGpu = false;
	}

	public void InitializeGpu()
	{
		DestroyGpuData();

		GpuPoints.SetData( CpuData.Points );
	}

	public void DestroyGpuData()
	{
		PointUpdateQueue.Clear();
		
		_gpuPoints?.Dispose();
		_gpuPointUpdates?.Dispose();
	}
	#endregion

	private GpuBuffer<VerletPoint> _gpuPoints;

	#region Point Updates
	public const int MAX_POINT_UPDATES = 1024;

	public int PendingPointUpdates => PointUpdateQueue.Count;
	public void ClearPendingPointUpdates() => PointUpdateQueue.Clear();
	internal Dictionary<int, VerletPointUpdate> PointUpdateQueue { get; } = [];
	internal GpuBuffer<VerletPointUpdate> GpuPointUpdates
	{
		get
		{
			if ( !_gpuPointUpdates.IsValid() )
			{
				_gpuPointUpdates = new( MAX_POINT_UPDATES );
			}
			return _gpuPointUpdates;
		}
	}
	private GpuBuffer<VerletPointUpdate> _gpuPointUpdates;

	internal void ApplyUpdatesFromCpu()
	{
		// Should we do a full copy of all CPU points to GPU?
		if ( CpuData.ShouldCopyToGpu )
		{
			StorePointsToGpu();
		}

		// Are there individual points that should be updated?
		if ( PendingPointUpdates > 0 )
		{
			VerletPointUpdate[] updateQueue = PointUpdateQueue.Values
				.Take( MAX_POINT_UPDATES )
				.ToArray();

			GpuPointUpdates.SetData( updateQueue );
		}
	}

	// TODO: Allow different updates to the same point to be combined if they are not mutually exclusive.

	public void QueuePointPositionUpdate( int i, Vector3 position ) 
		=> PointUpdateQueue[i] = VerletPointUpdate.Position( i, position );

	public void QueuePointFlagUpdate( int i, VerletPointFlags flags )
		=> PointUpdateQueue[i] = VerletPointUpdate.Flags( i, flags );
	#endregion

	#region Mesh Build
	internal GpuBuffer<RopeVertex> ReadbackVertices
	{
		get
		{
			var vertexCount = ColumnCount > 1 ? PointCount : PointCount + 2;
			if ( !_readbackVertices.IsValid() || _readbackVertices.ElementCount != vertexCount )
			{
				_readbackVertices = new GpuBuffer<RopeVertex>( vertexCount, GpuBuffer.UsageFlags.Vertex | GpuBuffer.UsageFlags.Structured );
			}
			return _readbackVertices;
		}
	}
	private GpuBuffer<RopeVertex> _readbackVertices;
	internal GpuBuffer<uint> ReadbackIndices
	{
		get
		{
			// TODO: Is this correct?
			int numQuads = ( RowCount - 1 ) * ( ColumnCount - 1);
			// I don't know why I have to do this, but we don't output enough indices otherwise.
			numQuads += RowCount - 1;
			// Two tris per quad times three indices per tri equals six indices per quad.
			var numIndices = 6 * numQuads;
			numIndices = Math.Max( 6, numIndices );
			if ( !_readbackIndices.IsValid() || _readbackIndices.ElementCount != numIndices )
			{
				_readbackIndices = new( numIndices, GpuBuffer.UsageFlags.Index | GpuBuffer.UsageFlags.Structured );
			}
			return _readbackIndices;
		}
	}
	private GpuBuffer<uint> _readbackIndices;

	private readonly ComputeShader VerletRopeMeshCs;
	private readonly ComputeShader VerletClothMeshCs;
	private readonly ComputeShader MeshBoundsCs;

	internal void DispatchBuildRopeMesh( float width, Color tint )
	{
		RenderAttributes attributes = new();
		attributes.Set( "NumPoints", PointCount );
		attributes.Set( "Points", GpuPoints );
		attributes.Set( "RenderWidth", width );
		attributes.Set( "TextureCoord", 0f );
		attributes.Set( "Tint", tint );
		attributes.Set( "OutputVertices", ReadbackVertices );
		attributes.Set( "OutputIndices", ReadbackIndices );

		VerletRopeMeshCs.DispatchWithAttributes( attributes, PointCount, 1, 1 );
	}

	internal void DispatchBuildClothMesh( Color tint )
	{
		RenderAttributes attributes = new();

		attributes.Set( "NumPoints", PointCount );
		attributes.Set( "NumColumns", ColumnCount );
		attributes.Set( "Points", PointCount );
		attributes.Set( "Tint", tint );
		attributes.Set( "OutputVertices", ReadbackVertices );
		attributes.Set( "OutputIndices", ReadbackIndices );

		VerletClothMeshCs.DispatchWithAttributes( attributes, RowCount, ColumnCount, 1 );
	}

	internal void DispatchCalculateMeshBounds( GpuBuffer<VerletBounds> globalBoundsBuffer, float skinSize )
	{
		RenderAttributes attributes = new();

		attributes.Set( "NumPoints", PointCount );
		attributes.Set( "Points", GpuPoints );
		attributes.Set( "SkinSize", 1f );

		attributes.Set( "BoundsIndex", CpuData.RopeIndex );
		attributes.Set( "Bounds", globalBoundsBuffer );

		MeshBoundsCs.DispatchWithAttributes( attributes, PointCount );
	}
	#endregion

	#region Simulation
	[ConVar( "verlet_infestation" )]
	public static bool InfestationMode { get; set; } = false;

	private enum VerletShapeType
	{
		Rope = 0,
		Cloth = 1,
	}

	private readonly ComputeShader VerletIntegrationCs;

	internal void DispatchSimulate( float fixedTimeStep )
	{
		RenderAttributes attributes = new();

		var shapeType = ColumnCount > 1 ? VerletShapeType.Cloth : VerletShapeType.Rope;

		// Choose rope or cloth
		attributes.SetComboEnum( "D_SHAPE_TYPE", shapeType );
		// Funny Venom effect
		attributes.SetCombo( "D_INFESTATION", InfestationMode );

		// Layout
		attributes.Set( "NumPoints", GpuPoints.ElementCount );
		attributes.Set( "NumColumns", ColumnCount );
		attributes.Set( "SegmentLength", CpuData.SegmentLength );
		attributes.Set( "Points", GpuPoints );
		// Updates
		attributes.Set( "NumPointUpdates", PendingPointUpdates );
		attributes.Set( "PointUpdates", GpuPointUpdates );
		// Simulation
		attributes.Set( "Iterations", CpuData.Iterations );
		attributes.Set( "AnchorMaxDistanceFactor", CpuData.AnchorMaxDistanceFactor );
		attributes.Set( "PointRadius", CpuData.Radius );
		// Forces
		attributes.Set( "Gravity", CpuData.Gravity );
		// Delta
		attributes.Set( "DeltaTime", CpuData.LastTick.Value.Relative );
		attributes.Set( "FixedTimeStep", fixedTimeStep );
		attributes.Set( "Translation", CpuData.Translation );
		// Colliders
		CpuData.Collisions.ApplyColliderAttributes( attributes );

		VerletIntegrationCs.DispatchWithAttributes( attributes, RowCount, ColumnCount, 1 );
	}

	internal void PostSimulationCleanup() => CpuData.PostSimulationCleanup();
	#endregion
}
