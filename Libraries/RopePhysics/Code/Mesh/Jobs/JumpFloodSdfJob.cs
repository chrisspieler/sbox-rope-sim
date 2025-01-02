using static Duccsoft.JumpFloodSdfJob;
using static Duccsoft.MeshDistanceSystem;

namespace Duccsoft;

internal class JumpFloodSdfJob : Job<InputData, OutputData>
{
	public struct InputData
	{
		public MeshDistanceField Mdf;
		public Vector3Int OctreeVoxel;
	}

	public struct OutputData
	{
		public MeshDistanceField Mdf;
		public Vector3Int OctreeVoxel;
		public VoxelSdfData Sdf;
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

		var seedCount = triCount * 4;
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
			_meshSdfCs.Attributes.Set( "Seeds", seedDataGpu );
			_meshSdfCs.Dispatch( threadsX: triCount );
		}

		//MeshSeedData[] seedData;
		//using ( PerfLog.Scope( id, $"Read {nameof(seedDataGpu)}" ) )
		//{
		//	seedData = new MeshSeedData[seedCount];
		//	seedDataGpu.GetData( seedData );
		//}
		//DumpSeedData( seedData, data );

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

		_meshSdfCs.Attributes.Clear();
		result = new OutputData()
		{
			Mdf = Input.Mdf,
			OctreeVoxel = Input.OctreeVoxel,
			Sdf = new VoxelSdfData( voxelSdf, size, bounds ),
		};
		return true;
	}

	private void DumpMesh( Vector4[] vertices, uint[] indices, PhysicsShape shape )
	{
		for ( int i = 0; i < indices.Length; i += 3 )
		{
			var i0 = indices[i];
			var i1 = indices[i + 1];
			var i2 = indices[i + 2];
			var v0 = vertices[i0];
			var v1 = vertices[i1];
			var v2 = vertices[i2];

			DebugOverlaySystem.Current.Sphere( new Sphere( v0, 1f ), color: Color.Red, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Sphere( new Sphere( v1, 1f ), color: Color.Red, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Sphere( new Sphere( v2, 1f ), color: Color.Red, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Sphere( new Sphere( (v0 + v1 + v2) / 3.0f, 1f ), color: Color.Orange, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v0, v1, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v1, v2, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v2, v0, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			Log.Info( $"i (0:{i0} 1:{i1} 2:{i2}), v: (0:{v0} 1:{v1} 2:{v2})" );
		}
	}

	private void DumpSeedData( Span<MeshSeedData> seedData, GpuMeshData data )
	{
		for ( int i = 0; i < seedData.Length; i++ )
		{
			var seedDatum = seedData[i];
			Vector4 positionOs = seedDatum.Position;
			Vector4 normal = seedDatum.Normal;
			Log.Info( $"# {i} pOs: {positionOs}, nor: {normal}" );
		}
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
