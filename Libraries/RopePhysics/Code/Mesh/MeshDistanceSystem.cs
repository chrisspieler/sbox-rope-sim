using Sandbox.Diagnostics;
using static Duccsoft.MeshDistanceSystem;

namespace Duccsoft;

internal record MdfBuildRequest( MeshCpuData CpuMesh, MeshGpuData GpuMesh )
{
	public int VertexCount => CpuMesh.Vertices.Length;
	public int IndexCount => CpuMesh.Indices.Length;
	public int TriangleCount => CpuMesh.Indices.Length / 3;
}

internal record MeshCpuData( Vector3[] Vertices, uint[] Indices )
{
	public static MeshCpuData FromPhysicsShape( int id, PhysicsShape shape )
	{
		Assert.IsValid( shape );

		Vector3[] vertices;
		uint[] indices;
		using ( PerfLog.Scope( id, $"{nameof( PhysicsShape )}.{nameof( PhysicsShape.Triangulate )}" ) )
		{
			shape.Triangulate( out vertices, out indices );
		}
		return new MeshCpuData( vertices, indices );
	}
	public BBox CalculateMeshBounds()
	{
		Assert.NotNull( Vertices );
		if ( Vertices.Length < 1 )
			return BBox.FromPositionAndSize( Vector3.Zero, 16f );

		var bbox = BBox.FromPositionAndSize( Vertices[0] );
		for ( int i = 1; i < Vertices.Length; i++ )
		{
			bbox = bbox.AddPoint( Vertices[i] );
		}
		return bbox.Grow( 4f );
	}
}

internal record MeshGpuData( GpuBuffer<Vector4> Vertices, GpuBuffer<uint> Indices )
{
	public static MeshGpuData FromCpuData( int id, MeshCpuData cpuData )
	{
		ArgumentNullException.ThrowIfNull( cpuData );
		Assert.NotNull( cpuData.Vertices );
		Assert.NotNull( cpuData.Indices );

		Vector4[] vertices;
		int vertexCount = cpuData.Vertices.Length;
		int indexCount = cpuData.Indices.Length;
		using ( PerfLog.Scope( id, "Convert Vector3 -> Vector4" ) )
		{
			vertices = new Vector4[vertexCount];
			for ( int i = 0; i < vertexCount; i++ )
			{
				vertices[i] = new Vector4( cpuData.Vertices[i] );
			}
		}
		GpuBuffer<Vector4> vtxBuffer;
		using ( PerfLog.Scope( id, "Fill Vertex Buffer" ) )
		{
			vtxBuffer = new GpuBuffer<Vector4>( vertexCount, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
			vtxBuffer.SetData( vertices );
		}
		GpuBuffer<uint> idxBuffer;
		using ( PerfLog.Scope( id, "Fill Index Buffer" ) )
		{
			idxBuffer = new GpuBuffer<uint>( indexCount, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Index );
			idxBuffer.SetData( cpuData.Indices );
		}
		return new MeshGpuData( vtxBuffer, idxBuffer );
	}
}

public partial class MeshDistanceSystem : GameObjectSystem<MeshDistanceSystem>
{

	[ConVar( "rope_collision_mdf_build_rate" )]
	public static int MaxBuildsPerUpdate { get; set; } = 10;
	[ConVar( "rope_collision_mdf_voxel_density")]
	public static float VoxelsPerWorldUnit { get; set; } = 1;
	[ConVar( "rope_collision_mdf_debug" )]
	public static bool EnableDebugLog { get; set; } = true;
	[ConVar( "rope_collision_mdf_perf_log" )]
	public static bool EnablePerfLog { get; set; } = false;
	[ConVar( "rope_collision_mdf_voxel_max" )]
	public static int MaxVoxelDimension { get; set; } = 128;
	[ConVar( "rope_collision_build_frametime_budget" )]
	public static float FrameTimeBudgetMilliseconds { get; set; } = 5f;

	public MeshDistanceSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, () => _updateStartTimer = FastTimer.StartNew(), "Update Start Timer" );
		Listen( Stage.FinishUpdate, 0, BuildAllMdfs, "Build Mesh Distance Fields" );
	}

	private FastTimer _updateStartTimer;

	private readonly Dictionary<int, MeshDistanceField> _meshDistanceFields = new();

	private readonly Dictionary<int, MdfBuildRequest> _mdfBuildQueue = new();
	private readonly ComputeShader _meshSdfCs = new( "mesh_sdf_cs" );

	public int MdfCount => _meshDistanceFields.Count;
	public int MdfTotalDataSize { get; private set; }

	private void AddMdf( int id, MeshDistanceField mdf )
	{
		if ( mdf is null )
		{
			RemoveMdf( id );
			return;
		}

		_meshDistanceFields.TryGetValue( id, out var previousMdf );
		_meshDistanceFields[id] = mdf;
		if ( previousMdf is null )
		{
			MdfTotalDataSize += mdf.DataSize;
		}
		else if ( previousMdf != mdf )
		{
			MdfTotalDataSize += mdf.DataSize - previousMdf.DataSize;
		}
	}

	public void RemoveMdf( int id )
	{
		if ( _meshDistanceFields.TryGetValue( id, out var mdf ) )
		{
			_meshDistanceFields.Remove( id );
			MdfTotalDataSize -= mdf.DataSize;
		}
	}

	private void BuildAllMdfs()
	{
		if ( !Game.IsPlaying )
			return;

		var startBuildTime = _updateStartTimer.ElapsedMilliSeconds;
		var built = 0;
		while( _mdfBuildQueue.Count > 0 )
		{
			(int id, MdfBuildRequest request ) = _mdfBuildQueue.First();
			if ( built >= MaxBuildsPerUpdate )
			{
				DebugLog( $"Request {id}, first of {_mdfBuildQueue.Count}, skipped until next update. Reached max builds per update: {MaxBuildsPerUpdate}" );
				return;
			}
			var buildTime = _updateStartTimer.ElapsedMilliSeconds - startBuildTime;
			if ( buildTime >= FrameTimeBudgetMilliseconds )
			{
				DebugLog( $"Request {id}, first of {_mdfBuildQueue.Count}, skipped until next update. Build time: {buildTime:F3}" );
				return;
			}
			BuildMdf( id, request );
			_mdfBuildQueue.Remove( id );
			built++;
		}
	}

	private enum MdfBuildStage
	{
		InitializeVolume,
		FindSeeds,
		InitializeSeeds,
		JumpFlood,
		Compress
	}

	private static void DebugLog( string message )
	{
		if ( !EnableDebugLog )
			return;

		Log.Info( message );
	}

	private static int NearestPowerOf2( int v )
	{
		v--;
		v |= v >> 1;
		v |= v >> 2;
		v |= v >> 4;
		v |= v >> 8;
		v |= v >> 16;
		v++;
		return v;
	}

	private void BuildMdf( int id, MdfBuildRequest request )
	{
		( MeshCpuData cpuMesh, MeshGpuData gpuMesh ) = request;

		BBox bounds;
		using ( PerfLog.Scope( id, nameof( MeshCpuData.CalculateMeshBounds ) ) )
		{
			bounds = cpuMesh.CalculateMeshBounds();
		}
		// int size = (int)Math.Max( Math.Max( bounds.Size.x, bounds.Size.y ), bounds.Size.z );
		int size = 16;

		if ( size > MaxVoxelDimension )
		{
			DebugLog( $"Reducing MDF ID {id} max voxel dimension from {size} to {MaxVoxelDimension}" );
			size = MaxVoxelDimension;
		}

		DebugLog( $"Build MDF ID {id}! Bounds: {bounds}, Volume: {size}x{size}x{size}" );
		var voxelSdf = DispatchBuildShader( id, size, gpuMesh, bounds );
		SparseVoxelOctree<int[]> octree;
		using ( PerfLog.Scope( id, $"Fill Octree" ) )
		{
			int svoSize = (int)Math.Max( Math.Max( bounds.Size.x, bounds.Size.y ), bounds.Size.z );
			svoSize = NearestPowerOf2( svoSize );
			// We want the octree depth to result in voxels that have a size of 16 units.
			int logDiff = (int)Math.Log2( svoSize ) - 4;
			int octreeDepth = Math.Max( 1, logDiff );
			Log.Info( $"Create octree of size {svoSize} and depth {octreeDepth}" );
			octree = new SparseVoxelOctree<int[]>( svoSize, octreeDepth );
			for ( int i = 0; i < request.VertexCount; i++ )
			{
				var vtx = request.CpuMesh.Vertices[i];
				int[] data = [(int)vtx.x, (int)vtx.y, (int)vtx.z, 0];
				octree.Insert( vtx, data );
			}
		}
		MeshDistanceField mdf;
		using ( PerfLog.Scope( id, $"Instantiate {nameof(MeshDistanceField)}" ) )
		{
			mdf = new MeshDistanceField( id, size, voxelSdf, octree, bounds );
		}
		using ( PerfLog.Scope( id, nameof(AddMdf) ) )
		{
			AddMdf( id, mdf );
		}
	}

	private int[] DispatchBuildShader( int id, int size, MeshGpuData data, BBox bounds )
	{
		int triCount = data.Indices.ElementCount / 3;
		int voxelCount = size * size * size;

		GpuBuffer<float> scratchVoxelSdfGpu;
		using ( PerfLog.Scope( id, $"Create {nameof(scratchVoxelSdfGpu)}" ) )
		{
			scratchVoxelSdfGpu = new GpuBuffer<float>( voxelCount, GpuBuffer.UsageFlags.Structured );
		}
		// Set the attributes for the signed distance field.
		_meshSdfCs.Attributes.Set( "VoxelMinsOs", bounds.Mins );
		_meshSdfCs.Attributes.Set( "VoxelMaxsOs", bounds.Maxs );
		_meshSdfCs.Attributes.Set( "VoxelVolumeDims", new Vector3( size ) );
		_meshSdfCs.Attributes.Set( "ScratchVoxelSdf", scratchVoxelSdfGpu );

		
		var voxelSeedsGpu = new GpuBuffer<int>( voxelCount, GpuBuffer.UsageFlags.Structured );
		using ( PerfLog.Scope( id, $"Dispatch {MdfBuildStage.InitializeVolume}" ) )
		{
			// Initialize each texel of the volume texture as having no associated seed index.
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.InitializeVolume );
			_meshSdfCs.Attributes.Set( "VoxelSeeds", voxelSeedsGpu );
			_meshSdfCs.Dispatch( size, size, size );
		}

		var seedCount = triCount * 4;
		GpuBuffer<MeshSeedData> seedDataGpu;
		using ( PerfLog.Scope( id, $"Create {nameof(seedDataGpu)}") )
		{
			seedDataGpu = new GpuBuffer<MeshSeedData>( seedCount, GpuBuffer.UsageFlags.Structured );
		}
		using ( PerfLog.Scope( id, $"Dispatch {MdfBuildStage.FindSeeds}" ) )
		{
			// For each triangle, write its object space position and normal to the seed data.
			_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.FindSeeds );
			_meshSdfCs.Attributes.Set( "Vertices", data.Vertices );
			_meshSdfCs.Attributes.Set( "Indices", data.Indices );
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

		using ( PerfLog.Scope( id, $"Dispatch {nameof(MdfBuildStage.InitializeSeeds)}" ) )
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
			using ( PerfLog.Scope( id, $"Dispatch {MdfBuildStage.JumpFlood}, Step Size: {step}") )
			{
				_meshSdfCs.Attributes.Set( "JumpStep", step );
				_meshSdfCs.Dispatch( size, size, size );
			}
		}

		GpuBuffer<int> voxelSdfGpu;
		using ( PerfLog.Scope( id, $"Create {nameof(voxelSdfGpu)}" ) )
		{
			voxelSdfGpu = new GpuBuffer<int>( voxelCount / 4, GpuBuffer.UsageFlags.Structured );
		}
		using ( PerfLog.Scope( id, $"Dispatch {MdfBuildStage.Compress}" ) )
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
		using ( PerfLog.Scope( id, $"Read {nameof( voxelSdfGpu )}" ) )
		{
			voxelSdf = new int[voxelCount / 4];
			voxelSdfGpu.GetData( voxelSdf );
		}
		//DumpVoxelSdf( voxelSdf );

		_meshSdfCs.Attributes.Clear();

		return voxelSdf;
	}

	public bool TryGetMdfByIndex( int index, out MeshDistanceField mdf )
	{
		index = index.Clamp( 0, MdfCount - 1 );
		if ( index < 0 )
		{
			mdf = null;
			return false;
		}
		mdf = _meshDistanceFields.Values
			.Skip( index )
			.FirstOrDefault();
		return mdf is not null;
	}

	public int IndexOf( MeshDistanceField mdf )
	{
		if ( MdfCount < 1 )
			return -1;

		int i = 0;
		foreach( var foundMdf in _meshDistanceFields.Values )
		{
			if ( foundMdf == mdf )
			{
				return i;
			}

			i++;
		}
		return -1;
	}

	public bool TryGetMdf( PhysicsShape shape, out MeshDistanceField meshDistanceField )
	{
		var id = shape.GetHashCode();

		// Did we already build a mesh distance field?
		if ( _meshDistanceFields.TryGetValue( id, out meshDistanceField ) )
			return true;

		// Should we begin building a mesh distance field?
		if ( !_mdfBuildQueue.ContainsKey( id ) )
			AddPhysicsShape( id, shape );
		
		return false;
	}

	private void AddPhysicsShape( int id, PhysicsShape shape )
	{
		var cpuMesh = MeshCpuData.FromPhysicsShape( id, shape );
		var gpuMesh = MeshGpuData.FromCpuData( id, cpuMesh );
		var buildRequest = new MdfBuildRequest( cpuMesh, gpuMesh );
		DebugLog( $"Queue MDF ID {id}! v: {buildRequest.VertexCount}, i: {buildRequest.IndexCount}, t: {buildRequest.TriangleCount}" );
		_mdfBuildQueue[id] = buildRequest;
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
			DebugOverlaySystem.Current.Sphere( new Sphere( ( v0 + v1 + v2 ) / 3.0f, 1f ), color: Color.Orange, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v0, v1, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v1, v2, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			DebugOverlaySystem.Current.Line( v2, v0, color: Color.Green, duration: 5f, transform: shape.Body.Transform, overlay: true );
			Log.Info( $"i (0:{i0} 1:{i1} 2:{i2}), v: (0:{v0} 1:{v1} 2:{v2})" );
		}
	}

	private void DumpSeedData( Span<MeshSeedData> seedData, MeshGpuData data )
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
