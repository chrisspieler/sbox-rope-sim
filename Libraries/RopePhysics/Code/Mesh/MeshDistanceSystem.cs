using Sandbox.Diagnostics;

namespace Duccsoft;

public class MeshDistanceSystem : GameObjectSystem<MeshDistanceSystem>
{
	private struct MeshSeedData
	{
		public Vector4 Position;
		public Vector4 Normal;
	}

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

	public MeshDistanceSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, BuildAllMdfs, "Build Mesh Distance Fields" );
	}

	private struct MeshData
	{
		public GpuBuffer<Vector4> Vertices;
		public GpuBuffer<uint> Indices;
		public Transform Transform;

		public readonly BBox CalculateMeshBounds()
		{
			var vertices = new Vector4[Vertices.ElementCount];
			Vertices.GetData( vertices );
			if ( vertices.Length < 1 )
				return default;

			var bbox = BBox.FromPositionAndSize( vertices[0] );
			for ( int i = 1; i < vertices.Length; i++ )
			{
				bbox = bbox.AddPoint( vertices[i] );
			}
			return bbox.Grow( 4f );
		}
	}

	private readonly Dictionary<int, MeshDistanceField> _meshDistanceFields = new();

	private readonly Dictionary<int, MeshData> _mdfBuildQueue = new();
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
		var built = 0;
		while( _mdfBuildQueue.Count > 0 && built < MaxBuildsPerUpdate )
		{
			(int id, MeshData data ) = _mdfBuildQueue.First();
			BuildMdf( id, data );
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

	private class PerfLog : IDisposable
	{
		private PerfLog() { }
		private int Id;
		private string Label;
		private FastTimer Stopwatch;

		public static PerfLog Scope( int id, string label )
		{
			var perflog = new PerfLog();
			perflog.Id = id;
			perflog.Label = label;
			perflog.Stopwatch = FastTimer.StartNew();
			return perflog;
		}

		public void Dispose()
		{
			if ( !EnablePerfLog )
				return;

			Log.Info( $"MDF {Id} {Stopwatch.ElapsedMilliSeconds:F3}ms: {Label}" );
		}
	}

	private void DebugLog( string message )
	{
		if ( !EnableDebugLog )
			return;

		Log.Info( message );
	}

	private void BuildMdf( int id, MeshData data )
	{
		BBox bounds;
		using ( PerfLog.Scope( id, nameof( MeshData.CalculateMeshBounds ) ) )
		{
			bounds = data.CalculateMeshBounds();
		}
		// int size = (int)Math.Max( Math.Max( bounds.Size.x, bounds.Size.y ), bounds.Size.z );
		int size = 16;

		if ( size > MaxVoxelDimension )
		{
			DebugLog( $"Reducing MDF ID {id} max voxel dimension from {size} to {MaxVoxelDimension}" );
			size = MaxVoxelDimension;
		}

		DebugLog( $"Build MDF ID {id}! Bounds: {bounds}, Volume: {size}x{size}x{size}" );
		var voxelSdf = DispatchBuildShader( id, size, data, bounds );
		MeshDistanceField mdf;
		using ( PerfLog.Scope( id, $"Instantiate {nameof(MeshDistanceField)}" ) )
		{
			mdf = new MeshDistanceField( id, size, voxelSdf, bounds );
		}
		using ( PerfLog.Scope( id, nameof(AddMdf) ) )
		{
			AddMdf( id, mdf );
		}
	}

	private int[] DispatchBuildShader( int id, int size, MeshData data, BBox bounds )
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
			_meshSdfCs.Dispatch( size / 4, size, size );
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
		Vector3[] vtx3;
		uint[] indices;
		using ( PerfLog.Scope( id, $"{nameof(PhysicsShape)}.{nameof(PhysicsShape.Triangulate)}" ) )
		{
			shape.Triangulate( out vtx3, out indices );
		}
		Vector4[] vertices;
		using ( PerfLog.Scope( id, "Convert Vector3 -> Vector4" ) )
		{
			vertices = new Vector4[vtx3.Length];
			for ( int i = 0; i < vtx3.Length; i++ )
			{
				vertices[i] = new Vector4( vtx3[i] );
			}
		}
		DebugLog( $"Queue MDF ID {id}! v: {vertices.Length}, i: {indices.Length}, t: {indices.Length / 3}" );
		// DumpMesh( vertices, indices, shape );
		GpuBuffer<Vector4> vtxBuffer;
		using ( PerfLog.Scope( id, "Fill Vertex Buffer" ) )
		{
			vtxBuffer = new GpuBuffer<Vector4>( vertices.Length, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
			vtxBuffer.SetData( vertices );
		}
		GpuBuffer<uint> idxBuffer;
		using ( PerfLog.Scope( id, "Fill Index Buffer" ) )
		{
			idxBuffer = new GpuBuffer<uint>( indices.Length, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Index );
			idxBuffer.SetData( indices );
		}
		var meshData = new MeshData()
		{
			Vertices = vtxBuffer,
			Indices = idxBuffer,
			Transform = shape.Body.Transform,
		};
		_mdfBuildQueue[id] = meshData;
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

	private void DumpSeedData( Span<MeshSeedData> seedData, MeshData data )
	{
		for ( int i = 0; i < seedData.Length; i++ )
		{
			var seedDatum = seedData[i];
			Vector4 positionOs = seedDatum.Position;
			Vector4 normal = seedDatum.Normal;
			DebugOverlaySystem.Current.Sphere( new Sphere( positionOs, 1f ), color: Color.Cyan, duration: 5f, transform: data.Transform, overlay: true );
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
