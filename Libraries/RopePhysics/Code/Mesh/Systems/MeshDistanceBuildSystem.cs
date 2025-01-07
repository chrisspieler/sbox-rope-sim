using System.Security.Cryptography;

namespace Duccsoft;

internal class MeshDistanceBuildSystem : GameObjectSystem<MeshDistanceBuildSystem>
{
	[ConVar( "rope_mdf_build_budget" )]
	public static double FrameTimeBudgetMilliseconds { get; set; } = 5.0;
	[ConVar( "rope_mdf_build_debug" )]
	public static bool EnableDebugLog { get; set; } = false;
	private static void DebugLog( string message ) { if ( EnableDebugLog ) { Log.Info( message ); } }

	private MeshDistanceSystem MdfSystem => MeshDistanceSystem.Current;

	public MeshDistanceBuildSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, RunJobs, "Build Mesh Distance Fields" );
	}

#region Build Management

	private readonly Dictionary<int, Job<PhysicsShape, CpuMeshData>> _meshExtractionFromPhysicsJobs = new();
	private readonly Dictionary<int, Job<Model, CpuMeshData>> _meshExtractionFromModelJobs = new();
	private readonly Dictionary<int, Job<CpuMeshData, GpuMeshData>> _convertMeshToGpuJobs = new();
	private readonly Dictionary<int, Job<CreateMeshOctreeJob.InputData, CreateMeshOctreeJob.OutputData>> _createSvoJobs = new();
	private readonly Dictionary<int, Job<JumpFloodSdfJob.InputData, JumpFloodSdfJob.OutputData>> _jumpFloodSdfJobs = new();

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
	}

	private void RunJobsInternal( ref double remainingTime )
	{
		// Triangulate PhysicsShapes and return their vertices and indices in CpuMeshData.
		var extractMeshFromPhysicsResults = RunJobSet( _meshExtractionFromPhysicsJobs, ref remainingTime );
		AddConvertMeshToGpuJobs( extractMeshFromPhysicsResults );
		if ( remainingTime <= 0 )
			return;

		var extractMeshFromModelResults = RunJobSet( _meshExtractionFromModelJobs, ref remainingTime );
		AddConvertMeshToGpuJobs( extractMeshFromModelResults );
		if ( remainingTime <= 0 )
			return;

		// Convert each CpuMeshData to GpuMeshData.
		var convertMeshToGpuResults = RunJobSet( _convertMeshToGpuJobs, ref remainingTime );
		foreach ( (var jobId, var gpuMesh) in convertMeshToGpuResults )
		{
			MdfSystem[jobId].MeshData = gpuMesh;
		}
		AddCreateMeshOctreeJobs( convertMeshToGpuResults );
		if ( remainingTime <= 0 )
			return;

		var createMeshOctreeResults = RunJobSet( _createSvoJobs, ref remainingTime );
		foreach ( (var jobId, var output) in createMeshOctreeResults )
		{
			output.Mdf.SetOctree( output.Octree );
		}
		AddJumpFloodSdfJobs( createMeshOctreeResults );
		if ( remainingTime <= 0 )
			return;

		// Run a jump flooding algorithm on each GpuMeshData to obtained signed distance fields.
		var jumpFloodSdfResults = RunJobSet( _jumpFloodSdfJobs, ref remainingTime );
		foreach ( (var jobId, var output) in jumpFloodSdfResults )
		{
			output.Mdf.SetOctreeVoxel( output.OctreeVoxel, output.Sdf );
			StopBuild( jobId );
		}
	}

	public void StopBuild( int id )
	{
		DebugLog( $"Stopping build for MDF ID # {id}" );
		_meshExtractionFromPhysicsJobs.Remove( id );
		_convertMeshToGpuJobs.Remove( id );
		_createSvoJobs.Remove( id );
		_jumpFloodSdfJobs.Remove( id );
	}

#endregion

#region Job Types

	private void AddCreateMeshOctreeJobs( Dictionary<int, GpuMeshData> inputs )
	{
		foreach ( ( var id, _ ) in inputs )
		{
			var mdf = MdfSystem[id];
			mdf.RebuildFromMesh();
		}
	}

	public CreateMeshOctreeJob AddCreateMeshOctreeJob( MeshDistanceField mdf, MeshDistanceConfig config = null )
	{
		var nextInput = new CreateMeshOctreeJob.InputData
		{
			Mdf = mdf,
			LeafSize = config?.TextureResolution ?? 16,
		};
		var job = new CreateMeshOctreeJob( mdf.Id, nextInput );
		// TODO: Cancel the previous job.
		_createSvoJobs[mdf.Id] = job;
		return job;
	}

	private void AddConvertMeshToGpuJobs( Dictionary<int, CpuMeshData> inputs )
	{
		foreach ( (var id, var input) in inputs )
		{
			var mdf = MdfSystem[id];
			mdf.ConvertMeshJob = new ConvertMeshToGpuJob( id, input );
			// TODO: Cancel the previous job.
			_convertMeshToGpuJobs[id] = mdf.ConvertMeshJob;
		}
	}
	private void AddJumpFloodSdfJobs( Dictionary<int, CreateMeshOctreeJob.OutputData> inputs )
	{
		foreach ( (var id, var input ) in inputs )
		{
			foreach( var voxel in input.LeafPoints )
			{
				input.Mdf.RebuildOctreeVoxel( voxel, false );
			}
		}
	}

	internal JumpFloodSdfJob AddJumpFloodSdfJob( MeshDistanceField mdf, Vector3Int position, bool collectDebugData )
	{
		var inputData = new JumpFloodSdfJob.InputData()
		{
			Mdf = mdf,
			OctreeVoxel = position,
			EmptySeedCount = JumpFloodSdfJob.NumEmptySeeds,
			CollectDebugData = collectDebugData,
		};
		var jobId = HashCode.Combine( mdf.Id, position );
		var job = new JumpFloodSdfJob( jobId, inputData );
		// TODO: Cancel the previous job.
		_jumpFloodSdfJobs[jobId] = job;
		return job;
	}

	internal ExtractMeshFromPhysicsJob AddExtractMeshJob( int id, PhysicsShape shape )
	{
		var mdf = MdfSystem[id];
		var job = new ExtractMeshFromPhysicsJob( id, shape );
		mdf.ExtractMeshJob = job;
		_meshExtractionFromPhysicsJobs[id] = job;
		DebugLog( $"Queue {nameof( ExtractMeshFromPhysicsJob )} # {id}" );
		return job;
	}

	internal ExtractMeshFromModelJob AddExtractMeshJob( int id, Model model )
	{
		var mdf = MdfSystem[id];
		var job = new ExtractMeshFromModelJob( id, model );
		mdf.ExtractMeshJob = job;
		_meshExtractionFromModelJobs[id] = job;
		DebugLog( $"Queue {nameof( ExtractMeshFromModelJob )} # {id}" );
		return job;
	}

#endregion


}
