using static Duccsoft.JumpFloodSdfJob;

namespace Duccsoft;

internal class JumpFloodSdfJob : Job<InputData, OutputData>
{
	[ConVar("rope_mdf_jfa_emptyseeds")]
	public static int NumEmptySeeds { get; set; } = 8;
	[ConVar( "rope_mdf_jfa_inside_threshold" )]
	public static float InsideThreshold { get; set; } = 0.75f;

	public struct InputData
	{
		public MeshDistanceField Mdf;
		public Vector3Int OctreeVoxel;
		public int TextureResolution;
		public int EmptySeedCount;
		public bool CollectDebugData;
	}

	public struct OutputData
	{
		public MeshDistanceField Mdf;
		public Vector3Int OctreeVoxel;
		public SignedDistanceField Sdf;
	}

	public JumpFloodSdfJob( int id, InputData input ) : base( id, input ) { }
	
	private enum MdfBuildStage
	{
		InitializeVolume,
		FindSeeds,
		InitializeSeeds,
		JumpFlood,
		Compress
	}

	public MeshDistanceField MeshDistanceField { get; }
	public Vector3Int LocalPosition { get; }

	private readonly ComputeShader _meshSdfCs = new( "mesh_sdf_cs" );

	protected override bool RunInternal( out OutputData result )
	{
		var bounds = Input.Mdf.VoxelToLocalBounds( Input.OctreeVoxel );
		int res = Input.TextureResolution;

		var outputSdf = new SignedDistanceField( null, res, bounds );

		var gpuMesh = Input.Mdf.MeshData;
		if ( Input.CollectDebugData )
		{
			outputSdf.Debug ??= new( Input.Mdf, Input.OctreeVoxel, outputSdf );
		}

		int triCount = gpuMesh.Indices.ElementCount / 3;
		int voxelCount = res * res * res;

		GpuBuffer<float> scratchVoxelSdfGpu;
		using ( PerfLog.Scope( Id, $"Create {nameof( scratchVoxelSdfGpu )}" ) )
		{
			scratchVoxelSdfGpu = new GpuBuffer<float>( voxelCount, GpuBuffer.UsageFlags.Structured );
		}
		// Set the attributes for the signed distance field.
		_meshSdfCs.Attributes.Set( "VoxelMinsOs", bounds.Mins );
		_meshSdfCs.Attributes.Set( "VoxelMaxsOs", bounds.Maxs );
		_meshSdfCs.Attributes.Set( "VoxelVolumeDims", new Vector3( res ) );
		_meshSdfCs.Attributes.Set( "ScratchVoxelSdf", scratchVoxelSdfGpu );


		var voxelSeedsGpu = new GpuBuffer<int>( voxelCount, GpuBuffer.UsageFlags.Structured );
		using ( PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.InitializeVolume}" ) )
		{
			// Initialize each texel of the volume texture as having no associated seed index.
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.InitializeVolume );
			_meshSdfCs.Attributes.Set( "VoxelSeeds", voxelSeedsGpu );
			_meshSdfCs.Dispatch( res, res, res );
		}

		var seedCount = triCount * 4 + Input.EmptySeedCount;
		GpuBuffer<MeshSeedData> seedDataGpu;
		using ( PerfLog.Scope( Id, $"Create {nameof( seedDataGpu )}" ) )
		{
			seedDataGpu = new GpuBuffer<MeshSeedData>( seedCount, GpuBuffer.UsageFlags.Structured );
		}
		var seedVoxelsGpu = new GpuBuffer<int>( seedCount, GpuBuffer.UsageFlags.Structured );
		using ( PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.FindSeeds}" ) )
		{
			// For each triangle, write its object space position and normal to the seed data.
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.FindSeeds );
			_meshSdfCs.Attributes.Set( "Vertices", gpuMesh.Vertices );
			_meshSdfCs.Attributes.Set( "Indices", gpuMesh.Indices );
			_meshSdfCs.Attributes.Set( "NumEmptySeeds", Input.EmptySeedCount);
			_meshSdfCs.Attributes.Set( "Seeds", seedDataGpu );
			_meshSdfCs.Attributes.Set( "SeedVoxels", seedVoxelsGpu );
			_meshSdfCs.Dispatch( threadsX: triCount + Input.EmptySeedCount );
		}

		if ( Input.CollectDebugData )
		{
			var seedData = new MeshSeedData[seedDataGpu.ElementCount];
			seedDataGpu.GetData( seedData );
			outputSdf.Debug.SeedData = seedData;
		}

		if ( Input.CollectDebugData )
		{
			var seedVoxels = new int[seedVoxelsGpu.ElementCount];
			seedVoxelsGpu.GetData( seedVoxels );
			outputSdf.Debug.SeedVoxels = seedVoxels;
		}


		using ( PerfLog.Scope( Id, $"Dispatch {nameof( MdfBuildStage.InitializeSeeds )}" ) )
		{
			// For each seed we found, write it to the seed data.
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.InitializeSeeds );
			_meshSdfCs.Dispatch( res, res, res );
		}

		// Run a jump flooding algorithm to find the nearest seed index for each texel/voxel
		// and calculate the signed distance to that seed's object space position.
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.JumpFlood );
		_meshSdfCs.Attributes.Set( "InsideDetectionThreshold", InsideThreshold );
		using ( PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.JumpFlood}, Step Size: 1" ) )
		{
			_meshSdfCs.Attributes.Set( "JumpStep", 1 );
			_meshSdfCs.Dispatch( res, res, res );
		}
		for ( int step = res / 2; step > 0; step /= 2 )
		{
			using var perfLog = PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.JumpFlood}, Step Size: {step}" );
			_meshSdfCs.Attributes.Set( "JumpStep", step );
			_meshSdfCs.Dispatch( res, res, res );
		}

		if ( Input.CollectDebugData )
		{
			var seedIds = new int[voxelSeedsGpu.ElementCount];
			voxelSeedsGpu.GetData( seedIds );
			outputSdf.Debug.VoxelSeedIds = seedIds;
		}

		GpuBuffer<int> voxelSdfGpu;
		using ( PerfLog.Scope( Id, $"Create {nameof( voxelSdfGpu )}" ) )
		{
			voxelSdfGpu = new GpuBuffer<int>( voxelCount / 4, GpuBuffer.UsageFlags.Structured );
		}
		using ( PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.Compress}" ) )
		{
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.Compress );
			_meshSdfCs.Attributes.Set( "VoxelSdf", voxelSdfGpu );
			_meshSdfCs.Dispatch( res, res, res );
		}

		int[] voxelSdf;
		using ( PerfLog.Scope( Id, $"Read {nameof( voxelSdfGpu )}" ) )
		{
			voxelSdf = new int[voxelCount / 4];
			voxelSdfGpu.GetData( voxelSdf );
		}
		outputSdf.Data = voxelSdf;

		if ( Input.CollectDebugData )
		{
			outputSdf.Debug.PrecalculateGradients();
		}

		_meshSdfCs.Attributes.Clear();
		result = new OutputData()
		{
			Mdf = Input.Mdf,
			OctreeVoxel = Input.OctreeVoxel,
			Sdf = outputSdf,
		};
		return true;
	}
}
