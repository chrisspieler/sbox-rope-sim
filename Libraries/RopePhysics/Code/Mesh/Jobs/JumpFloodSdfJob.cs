using System.Text;
using static Duccsoft.JumpFloodSdfJob;
using static Duccsoft.MeshDistanceSystem;

namespace Duccsoft;

internal class JumpFloodSdfJob : Job<InputData, OutputData>
{
	[ConVar("rope_mdf_jfa_emptyseeds")]
	public static int NumEmptySeeds { get; set; } = 8;

	public struct InputData
	{
		public MeshDistanceField Mdf;
		public Vector3Int OctreeVoxel;
		public int EmptySeedCount;
		public bool DumpDebugData;
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
		var gpuMesh = Input.Mdf.MeshData;
		if ( Input.DumpDebugData )
		{
			DumpCpuMesh( Input.Mdf.MeshData.CpuMesh );
		}

		var bounds = Input.Mdf.VoxelToLocalBounds( Input.OctreeVoxel );

		int size = Input.Mdf.OctreeLeafDims;
		int triCount = gpuMesh.Indices.ElementCount / 3;
		int voxelCount = size * size * size;

		GpuBuffer<float> scratchVoxelSdfGpu;
		using ( PerfLog.Scope( Id, $"Create {nameof( scratchVoxelSdfGpu )}" ) )
		{
			scratchVoxelSdfGpu = new GpuBuffer<float>( voxelCount, GpuBuffer.UsageFlags.Structured );
		}
		// Set the attributes for the signed distance field.
		_meshSdfCs.Attributes.Set( "VoxelMinsOs", bounds.Mins );
		_meshSdfCs.Attributes.Set( "VoxelMaxsOs", bounds.Maxs );
		_meshSdfCs.Attributes.Set( "VoxelVolumeDims", new Vector3( size ) );
		_meshSdfCs.Attributes.Set( "ScratchVoxelSdf", scratchVoxelSdfGpu );


		var voxelSeedsGpu = new GpuBuffer<int>( voxelCount, GpuBuffer.UsageFlags.Structured );
		using ( PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.InitializeVolume}" ) )
		{
			// Initialize each texel of the volume texture as having no associated seed index.
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.InitializeVolume );
			_meshSdfCs.Attributes.Set( "VoxelSeeds", voxelSeedsGpu );
			_meshSdfCs.Dispatch( size, size, size );
		}

		var seedCount = triCount * 4 + Input.EmptySeedCount;
		GpuBuffer<MeshSeedData> seedDataGpu;
		using ( PerfLog.Scope( Id, $"Create {nameof( seedDataGpu )}" ) )
		{
			seedDataGpu = new GpuBuffer<MeshSeedData>( seedCount, GpuBuffer.UsageFlags.Structured );
		}
		using ( PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.FindSeeds}" ) )
		{
			// For each triangle, write its object space position and normal to the seed data.
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.FindSeeds );
			_meshSdfCs.Attributes.Set( "Vertices", gpuMesh.Vertices );
			_meshSdfCs.Attributes.Set( "Indices", gpuMesh.Indices );
			_meshSdfCs.Attributes.Set( "NumEmptySeeds", Input.EmptySeedCount);
			_meshSdfCs.Attributes.Set( "Seeds", seedDataGpu );
			_meshSdfCs.Dispatch( threadsX: triCount + Input.EmptySeedCount );
		}

		if ( Input.DumpDebugData )
		{
			DumpSeedData( seedDataGpu );
		}


		using ( PerfLog.Scope( Id, $"Dispatch {nameof( MdfBuildStage.InitializeSeeds )}" ) )
		{
			// For each seed we found, write it to the seed data.
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.InitializeSeeds );
			_meshSdfCs.Dispatch( threadsX: seedCount );
		}

		// Run a jump flooding algorithm to find the nearest seed index for each texel/voxel
		// and calculate the signed distance to that seed's object space position.
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.JumpFlood );
		for ( int step = size / 2; step > 0; step /= 2 )
		{
			using ( PerfLog.Scope( Id, $"Dispatch {MdfBuildStage.JumpFlood}, Step Size: {step}" ) )
			{
				_meshSdfCs.Attributes.Set( "JumpStep", step );
				_meshSdfCs.Dispatch( size, size, size );
			}
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
			_meshSdfCs.Dispatch( size, size, size );
		}

		//int[] voxelSeeds;
		//using ( PerfLog.Scope( id, $"Read {nameof( voxelSeedsGpu )}" ) )
		//{
		//	voxelSeeds = new int[voxelCount];
		//	voxelSeedsGpu.GetData( voxelSeeds );
		//}
		//DumpVoxelSeeds( voxelSeeds );

		int[] voxelSdf;
		using ( PerfLog.Scope( Id, $"Read {nameof( voxelSdfGpu )}" ) )
		{
			voxelSdf = new int[voxelCount / 4];
			voxelSdfGpu.GetData( voxelSdf );
		}
		//DumpVoxelSdf( voxelSdf );

		if ( Input.DumpDebugData )
		{
			Log.Info( $"Logs are available at: {FileSystem.OrganizationData.GetFullPath( DIR_DUMP )}" );
		}

		_meshSdfCs.Attributes.Clear();
		result = new OutputData()
		{
			Mdf = Input.Mdf,
			OctreeVoxel = Input.OctreeVoxel,
			Sdf = new SignedDistanceField( voxelSdf, size, bounds ),
		};
		return true;
	}

	const string DIR_DUMP = "mdfData";
	const string PATH_DUMP_SEED_DATA = $"{DIR_DUMP}/dump_seedData.txt";
	const string PATH_DUMP_CPU_MESH = $"{DIR_DUMP}/dump_cpuMesh.txt";

	private void DumpCpuMesh( CpuMeshData meshData )
	{
		var dumpTimer = new MultiTimer();
		using ( dumpTimer.RecordTime() )
		{
			var indices = meshData.Indices;
			var vertices = meshData.Vertices;
			var sb = new StringBuilder();
			sb.AppendLine( $"Dumping {indices.Length} indices, {vertices.Length} vertices, {indices.Length / 3.0f} triangles" );
			sb.AppendLine( $"Mesh bounds: {meshData.Bounds}" );
			for ( int i = 0; i < indices.Length; i += 3 )
			{
				var i0 = indices[i];
				var i1 = indices[i + 1];
				var i2 = indices[i + 2];
				var v0 = vertices[i0];
				var v1 = vertices[i1];
				var v2 = vertices[i2];
				sb.AppendLine( $"tri # {i / 3} (idx {i0},{i1},{i2}) vtx: ({v0}),({v1}),({v2})" );
			}
			var fs = FileSystem.OrganizationData;
			fs.CreateDirectory( DIR_DUMP );
			fs.WriteAllText( PATH_DUMP_CPU_MESH, sb.ToString() );
		}
		Log.Info( $"DumpMesh in {dumpTimer.LastMilliseconds:F3}ms" );
			
	}


	private void DumpSeedData( GpuBuffer<MeshSeedData> gpuData )
	{
		var seedDataDumpTimer = new MultiTimer();
		using ( seedDataDumpTimer.RecordTime() )
		{
			MeshSeedData[] seedData;
			seedData = new MeshSeedData[gpuData.ElementCount];
			gpuData.GetData( seedData );
			var sb = new StringBuilder();
			var triCount = Input.Mdf.MeshData.Indices.ElementCount / 3;
			var emptySeedStartIdx = triCount * 4;
			sb.AppendLine( $"SeedData dump for {Input.OctreeVoxel}[{Input.OctreeVoxel/Input.Mdf.OctreeLeafDims}] of MDF # {Input.Mdf.Id}" );
			sb.AppendLine( $"{triCount} tris, {Input.EmptySeedCount} empty seeds, data: {seedData.Length}, expected data: {triCount * 4 + Input.EmptySeedCount}" );
			for ( int i = 0; i < seedData.Length; i++ )
			{
				if ( i == emptySeedStartIdx )
				{
					sb.AppendLine( "===" );
					sb.AppendLine( $"EMPTY SEED DATA BEGINS NOW!" );
					sb.AppendLine( "===" );
				}
				var seedDatum = seedData[i];
				Vector4 positionOs = seedDatum.Position;
				Vector4 normal = seedDatum.Normal;
				var line = $"seed # {i} pOs: {positionOs}, nor: {normal}";
				sb.AppendLine( line );
			}
			var fs = FileSystem.OrganizationData;
			fs.CreateDirectory( DIR_DUMP );
			fs.WriteAllText( PATH_DUMP_SEED_DATA, sb.ToString() );
		}
		Log.Info( $"Seed data dump in {seedDataDumpTimer.LastMilliseconds:F3}ms" );
	}

	private void DumpVoxelSeeds( Span<int> voxelData )
	{
		for ( int i = 0; i < voxelData.Length; i++ )
		{
			var voxel = voxelData[i];
			Log.Info( $"voxel {i} seedId: {voxel}" );
		}
	}

	private void DumpVoxelSdf( Span<int> voxelSdf )
	{
		for ( int i = 0; i < voxelSdf.Length; i++ )
		{
			var signedDistance = voxelSdf[i];
			Log.Info( $"voxel {i} packed int: {signedDistance}" );
		}
	}
}
