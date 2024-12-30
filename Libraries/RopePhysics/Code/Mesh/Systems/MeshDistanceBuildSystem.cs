namespace Duccsoft;

public class MeshDistanceBuildSystem : GameObjectSystem<MeshDistanceBuildSystem>
{
	[ConVar( "rope_mdf_build_budget" )]
	public static double FrameTimeBudgetMilliseconds { get; set; } = 5.0;
	[ConVar( "rope_mdf_build_debug" )]
	public static bool EnableDebugLog { get; set; } = false;

	public MeshDistanceBuildSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, RunJobs, "Build Mesh Distance Fields" );
	}

	public bool IsBuilding( int id ) => _allJobs.ContainsKey( id );

	private readonly Dictionary<int, Job> _allJobs = new();
	private readonly Dictionary<int, Job<PhysicsShape, CpuMeshData>> _meshExtractionJobs = new();
	private readonly Dictionary<int, Job<CpuMeshData, GpuMeshData>> _convertMeshToGpuJobs = new();
	private readonly Dictionary<int, Job<GpuMeshData, SparseVoxelOctree<VoxelSdfData>>> _createSvoJobs = new();
	private readonly Dictionary<int, Job<GpuMeshData, VoxelSdfData>> _jumpFloodSdfJobs = new();

	private readonly Dictionary<int, MeshDistanceField> _mdfsInProgress = new();

	private Dictionary<int, TOutput> RunJobSet<TInput, TOutput>( Dictionary<int, Job<TInput, TOutput>> jobSource, ref double remainingTime )
	{
		var jobSet = new JobSet<TInput, TOutput>( jobSource.GetHashCode(), jobSource.Values, remainingTime );
		jobSet.Run( out var resultDict );
		remainingTime -= jobSet.TimerStats.LastMilliseconds;
		foreach ( var result in resultDict )
		{
			jobSource.Remove( result.Key );
		}
		return resultDict;
	}

	private void RunJobs()
	{
		if ( !Game.IsPlaying )
			return;

		var remainingTime = FrameTimeBudgetMilliseconds;
		RunJobsInternal( ref remainingTime );
		if ( remainingTime <= 0 )
		{
			DebugLog( "RunJobs incomplete, continuing next update." );
		}
	}

	private void RunJobsInternal( ref double remainingTime )
	{
		// Triangulate PhysicsShapes and return their vertices and indices in CpuMeshData.
		var extractMeshFromPhysicsResults = RunJobSet( _meshExtractionJobs, ref remainingTime );
		AddConvertMeshToGpuJobs( extractMeshFromPhysicsResults );
		if ( remainingTime <= 0 )
			return;

		// Convert each CpuMeshData to GpuMeshData.
		var convertMeshToGpuResults = RunJobSet( _convertMeshToGpuJobs, ref remainingTime );
		foreach ( (var jobId, var gpuMesh) in convertMeshToGpuResults )
		{
			_mdfsInProgress[jobId] = new MeshDistanceField( jobId, gpuMesh );
		}
		AddCreateMeshOctreeJobs( convertMeshToGpuResults );
		if ( remainingTime <= 0 )
			return;

		var createMeshOctreeResults = RunJobSet( _createSvoJobs, ref remainingTime );
		foreach ( (var jobId, var octree) in createMeshOctreeResults )
		{
			Log.Info( $"Add octree {jobId} to mdf" );
			_mdfsInProgress[jobId].Octree = octree;
		}
		AddJumpFloodSdfJobs( createMeshOctreeResults );
		if ( remainingTime <= 0 )
			return;

		// Run a jump flooding algorithm on each GpuMeshData to obtained signed distance fields.
		var jumpFloodSdfResults = RunJobSet( _jumpFloodSdfJobs, ref remainingTime );
		var mdfSystem = MeshDistanceSystem.Current;
		foreach ( (var jobId, var voxels) in jumpFloodSdfResults )
		{
			_mdfsInProgress[jobId].VoxelSdf = voxels;
			var mdf = StopBuild( jobId );
			mdfSystem.AddMdf( jobId, mdf );
			DebugLog( $"Added MDF ID # {jobId}" );
		}
	}

	public MeshDistanceField StopBuild( int id )
	{
		DebugLog( $"Stopping build for MDF ID # {id}" );
		_allJobs.Remove( id );
		_meshExtractionJobs.Remove( id );
		_convertMeshToGpuJobs.Remove( id );
		_createSvoJobs.Remove( id );
		_jumpFloodSdfJobs.Remove( id );

		if ( !_mdfsInProgress.TryGetValue( id, out var mdf ) )
			return null;

		_mdfsInProgress.Remove( id );
		return mdf;
	}

	private void AddCreateMeshOctreeJobs( Dictionary<int, GpuMeshData> inputs )
	{
		foreach ( (var id, var input) in inputs )
		{
			var job = new CreateMeshOctreeJob( id, input );
			_createSvoJobs[id] = job;
			_allJobs[id] = job;
		}
	}

	private void AddConvertMeshToGpuJobs( Dictionary<int, CpuMeshData> inputs )
	{
		foreach ( (var id, var input) in inputs )
		{
			var job = new ConvertMeshToGpuJob( id, input );
			_convertMeshToGpuJobs[id] = job;
			_allJobs[id] = job;
		}
	}
	private void AddJumpFloodSdfJobs( Dictionary<int, SparseVoxelOctree<VoxelSdfData>> inputs )
	{
		foreach ( (var id, var input) in inputs )
		{
			var mdf = _mdfsInProgress[id];
			var job = new JumpFloodSdfJob( id, mdf.MeshData );
			_jumpFloodSdfJobs[id] = job;
			_allJobs[id] = job;
		}
	}

	internal void BuildFromPhysicsShape( int id, PhysicsShape shape )
	{
		var extractMeshJob = new ExtractMeshFromPhysicsJob( id, shape );
		_meshExtractionJobs.Add( id, extractMeshJob );
		_allJobs[id] = extractMeshJob;
		DebugLog( $"Queue {nameof( ExtractMeshFromPhysicsJob )} # {id}" );
	}

	private static void DebugLog( string message )
	{
		if ( !EnableDebugLog )
			return;

		Log.Info( message );
	}
}
