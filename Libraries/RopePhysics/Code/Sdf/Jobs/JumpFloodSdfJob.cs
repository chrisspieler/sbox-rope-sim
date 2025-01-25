using static Duccsoft.JumpFloodSdfJob;

namespace Duccsoft;

internal class JumpFloodSdfJob : Job<InputData, OutputData>
{
	[ConVar("rope_mdf_jfa_emptyseeds")]
	public static int NumEmptySeeds { get; set; } = 0;
	[ConVar( "rope_mdf_jfa_inside_threshold" )]
	public static float InsideThreshold { get; set; } = 0.99f;

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
		FindSeeds,
		InitializeSeeds,
		JumpFlood
	}

	public MeshDistanceField MeshDistanceField { get; }
	public Vector3Int LocalPosition { get; }

	private readonly ComputeShader _meshSdfCs = new( "shaders/sdf/mesh_sdf_cs.shader" );

	protected override bool RunInternal( out OutputData result )
	{
		RenderAttributes attributes = new();

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

		outputSdf.DataTexture = Texture.CreateVolume( res, res, res, ImageFormat.RGBA8888 )
			.WithUAVBinding()
			.Finish();

		// Set the attributes for the signed distance field.
		attributes.Set( "VoxelMinsOs", bounds.Mins );
		attributes.Set( "VoxelMaxsOs", bounds.Maxs );
		attributes.Set( "SdfTextureSize", res );
		attributes.Set( "SdfTexture", outputSdf.DataTexture );
		attributes.Set( "InsideDetectionThreshold", InsideThreshold );

		var seedCount = triCount * 4 + Input.EmptySeedCount;
		GpuBuffer<MeshSeedData>	seedDataGpu = new( seedCount );
		GpuBuffer<int> seedVoxelsGpu = new( seedCount );
		using ( PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.FindSeeds}" ) )
		{
			// For each triangle, write its object space position and normal to the seed data.
			// For each empty seed, write use the information from the nearest triangle.
			attributes.SetComboEnum( "D_STAGE", MdfBuildStage.FindSeeds );
			attributes.Set( "Vertices", gpuMesh.Vertices );
			attributes.Set( "Indices", gpuMesh.Indices );
			attributes.Set( "NumEmptySeeds", Input.EmptySeedCount );
			attributes.Set( "Seeds", seedDataGpu );
			attributes.Set( "SeedVoxels", seedVoxelsGpu );
			_meshSdfCs.DispatchWithAttributes( attributes, threadsX: triCount + Input.EmptySeedCount );
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

		GpuBuffer<int> voxelSeedsGpu = new( voxelCount );
		using ( PerfLog.Scope( Id, $"Dispatch {nameof( MdfBuildStage.InitializeSeeds )}" ) )
		{
			// For each seed we found, write it to the seed data.
			attributes.SetComboEnum( "D_STAGE", MdfBuildStage.InitializeSeeds );
			attributes.Set( "VoxelSeeds", voxelSeedsGpu );
			_meshSdfCs.DispatchWithAttributes( attributes, res, res, res );
		}


		// Run a jump flooding algorithm to find the nearest seed index for each texel/voxel
		// and calculate the signed distance to that seed's object space position.
		attributes.SetComboEnum( "D_STAGE", MdfBuildStage.JumpFlood );
		for ( int step = res / 2; step > 0; step /= 2 )
		{
			using var perfLog = PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.JumpFlood}, Step Size: {step}" );
			attributes.Set( "JumpStep", step );
			_meshSdfCs.DispatchWithAttributes( attributes, res, res, res );
		}
		for ( int i = 0; i < 2; i++ )
		{
			using ( PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.JumpFlood}, Step Size: 1" ) )
			{
				attributes.Set( "JumpStep", 1 );
				_meshSdfCs.DispatchWithAttributes( attributes, res, res, res );
			}
		}

		if ( Input.CollectDebugData )
		{
			var seedIds = new int[voxelSeedsGpu.ElementCount];
			voxelSeedsGpu.GetData( seedIds );
			outputSdf.Debug.VoxelSeedIds = seedIds;
		}

		if ( Input.CollectDebugData )
		{
			outputSdf.Debug.PrecalculateGradients();
		}

		result = new OutputData()
		{
			Mdf = Input.Mdf,
			OctreeVoxel = Input.OctreeVoxel,
			Sdf = outputSdf,
		};
		return true;
	}
}
