using Sandbox.Diagnostics;

namespace Duccsoft;

public class MeshDistanceSystem : GameObjectSystem<MeshDistanceSystem>
{
	[ConVar( "rope_collision_mdf_build_rate" )]
	public static int MaxBuildsPerUpdate { get; set; } = 10;
	[ConVar( "rope_collision_mdf_voxel_density")]
	public static float VoxelsPerWorldUnit { get; set; } = 1;
	[ConVar( "rope_collision_mdf_debug" )]
	public static bool DebugLog { get; set; } = true;
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
	public int MdfTotalBytes { get; private set; }

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
			MdfTotalBytes += mdf.DataSize;
		}
		else if ( previousMdf != mdf )
		{
			MdfTotalBytes += mdf.DataSize - previousMdf.DataSize;
		}
	}

	public void RemoveMdf( int id )
	{
		if ( _meshDistanceFields.TryGetValue( id, out var mdf ) )
		{
			_meshDistanceFields.Remove( id );
			MdfTotalBytes -= mdf.DataSize;
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
		FinalizeOutput,
		DebugNormalized
	}
	private void BuildMdf( int id, MeshData data )
	{
		var sw = FastTimer.StartNew();
		var bounds = data.CalculateMeshBounds();
		Log.Info( $"Calculate Mesh Bounds: {sw.ElapsedMilliSeconds:F3}" );
		int size = (int)Math.Max( Math.Max( bounds.Size.x, bounds.Size.y ), bounds.Size.z );

		if ( size > MaxVoxelDimension )
		{
			if ( DebugLog )
			{
				Log.Info( $"Reducing MDF ID {id} max voxel dimension from {size} to {MaxVoxelDimension}" );
			}
			size = MaxVoxelDimension;
		}

		if ( DebugLog )
		{
			Log.Info( $"Build MDF ID {id}! Bounds: {bounds}, Volume: {size}x{size}x{size}" );
		}
		var voxelSdf = DispatchBuildShader( size, data, bounds );
		sw.Start();
		var mdf = new MeshDistanceField( id, size, voxelSdf, bounds );
		Log.Info( $"Instantiate MDF: {sw.ElapsedMilliSeconds:F3}" );
		sw.Start();
		AddMdf( id, mdf );
		Log.Info( $"Added MDF: {sw.ElapsedMilliSeconds:F3}" );
	}

	private float[] DispatchBuildShader( int size, MeshData data, BBox bounds )
	{
		int triCount = data.Indices.ElementCount / 3;
		int voxelCount = size * size * size;

		var sw = FastTimer.StartNew();
		// Set the attributes for the signed distance field.
		var voxelSdfGpu = new GpuBuffer<float>( voxelCount, GpuBuffer.UsageFlags.Structured );
		Log.Info( $"Create VoxelSdf Buffer: {sw.ElapsedMilliSeconds:F3}" );
		_meshSdfCs.Attributes.Set( "VoxelMinsOs", bounds.Mins );
		_meshSdfCs.Attributes.Set( "VoxelMaxsOs", bounds.Maxs );
		_meshSdfCs.Attributes.Set( "VoxelVolumeDims", new Vector3( size ) );
		_meshSdfCs.Attributes.Set( "VoxelSdf", voxelSdfGpu );

		
		var voxelSeedsGpu = new GpuBuffer<int>( voxelCount, GpuBuffer.UsageFlags.Structured );
		sw.Start();
		// Initialize each texel of the volume texture as having no associated seed index.
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.InitializeVolume );
		_meshSdfCs.Attributes.Set( "VoxelSeeds", voxelSeedsGpu );
		_meshSdfCs.Dispatch( size, size, size );
		Log.Info( $"Dispatch InitializeVolume: {sw.ElapsedMilliSeconds:F3}" );

		var seedCount = triCount * 4;
		var seedDataGpu = new GpuBuffer<MeshSeedData>( seedCount, GpuBuffer.UsageFlags.Structured );
		sw.Start();
		// For each triangle, write its object space position and normal to the seed data.
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.FindSeeds );
		_meshSdfCs.Attributes.Set( "Vertices", data.Vertices );
		_meshSdfCs.Attributes.Set( "Indices", data.Indices );
		_meshSdfCs.Attributes.Set( "Seeds", seedDataGpu );
		_meshSdfCs.Dispatch( threadsX: triCount );
		Log.Info( $"Dispatch FindSeeds: {sw.ElapsedMilliSeconds:F3}" );

		sw.Start();
		var seedData = new MeshSeedData[seedCount];
		seedDataGpu.GetData( seedData );
		Log.Info( $"Get Seed Data: {sw.ElapsedMilliSeconds:F3}" );

		// DumpSeedData( seedData, data );
		sw.Start();
		// For each seed we found, write it to the seed data.
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.InitializeSeeds );
		_meshSdfCs.Dispatch( threadsX: seedCount );
		Log.Info( $"Dispatch InitializeSeeds: {sw.ElapsedMilliSeconds:F3}" );

		// Run a jump flooding algorithm to find the nearest seed index for each texel/voxel
		// and calculate the signed distance to that seed's object space position.
		_meshSdfCs.Attributes.SetComboEnum( "D_STAGE", MdfBuildStage.JumpFlood );
		for ( int step = size / 2; step > 0; step /= 2 )
		{
			sw.Start();
			_meshSdfCs.Attributes.Set( "JumpStep", step );
			_meshSdfCs.Dispatch( size, size, size );
			Log.Info( $"Dispatch JumpFlood {step}: {sw.ElapsedMilliSeconds:F3}" );
		}

		//var voxelSeeds = new int[voxelCount];
		//voxelSeedsGpu.GetData( voxelSeeds );
		//DumpVoxelSeeds( voxelSeeds );

		sw.Start();
		var voxelSdf = new float[voxelCount];
		voxelSdfGpu.GetData( voxelSdf );
		Log.Info( $"Get VoxelSdf Data: {sw.ElapsedMilliSeconds:F3}" );
		// DumpVoxelSdf( voxelSdf );

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
		var sw = FastTimer.StartNew();
		shape.Triangulate( out Vector3[] vtx3, out uint[] indices );
		Log.Info( $"Triangulate PhysicsShape: {sw.ElapsedMilliSeconds:F3}" );
		sw = FastTimer.StartNew();
		var vertices = new Vector4[vtx3.Length];
		for ( int i = 0; i < vtx3.Length; i++ )
		{
			vertices[i] = new Vector4( vtx3[i] );
		}
		if ( DebugLog )
		{
			Log.Info( $"Queue MDF ID {id}! v: {vertices.Length}, i: {indices.Length}, t: {indices.Length / 3}" );
		}
		Log.Info( $"Convert Vector3 -> Vector4: {sw.ElapsedMilliSeconds:F3}" );
		sw.Start();
		// DumpMesh( vertices, indices, shape );
		var vtxBuffer = new GpuBuffer<Vector4>( vertices.Length, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
		vtxBuffer.SetData( vertices );
		Log.Info( $"Fill vertex buffer: {sw.ElapsedMilliSeconds:F3}" );
		sw.Start();
		var idxBuffer = new GpuBuffer<uint>( indices.Length, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Index );
		idxBuffer.SetData( indices );
		Log.Info( $"Fill index buffer: {sw.ElapsedMilliSeconds:F3}" );
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

	private void DumpVoxelSdf( Span<float> voxelSdf )
	{
		for ( int i = 0; i < voxelSdf.Length; i++ )
		{
			var signedDistance = voxelSdf[i];
			Log.Info( $"voxel {i} signedDistance: {signedDistance}" );
		}
	}
}
