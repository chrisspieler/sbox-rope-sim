namespace Duccsoft;

public partial class MeshDistanceSystem : GameObjectSystem<MeshDistanceSystem>
{
	[ConVar( "rope_mdf_voxel_density" )]
	public static float VoxelsPerWorldUnit { get; set; } = 1f;
	public static float VoxelSize => 1f / VoxelsPerWorldUnit;

	public int MdfCount => _meshDistanceFields.Count;
	public int MdfTotalDataSize { get; private set; }
	private MeshDistanceBuildSystem BuildSystem { get; set; }

	public MeshDistanceSystem( Scene scene ) : base( scene ) { }

	private readonly Dictionary<int, MeshDistanceField> _meshDistanceFields = new();


	public MeshDistanceField GetMdf( PhysicsShape shape )
	{
		var id = shape.GetHashCode();
		var mdf = GetMdf( id );

		BuildSystem ??= MeshDistanceBuildSystem.Current;
		if ( mdf.ExtractMeshJob is null )
		{
			BuildSystem.AddExtractMeshJob( id, shape );
		}
		return mdf;
	}

	internal MeshDistanceField GetMdf( int id )
	{
		// Did we already build a mesh distance field?
		if ( _meshDistanceFields.TryGetValue( id, out var mdf ) )
			return mdf;

		mdf = new MeshDistanceField( MeshDistanceBuildSystem.Current, id );
		AddMdf( id, mdf );
		return mdf;
	}

	internal MeshDistanceField this[int id] => GetMdf( id );

	internal void AddMdf( int id, MeshDistanceField mdf )
	{
		_meshDistanceFields[id] = mdf;
	}

	public void RemoveMdf( int id )
	{
		if ( _meshDistanceFields.TryGetValue( id, out var mdf ) )
		{
			MeshDistanceBuildSystem.Current.StopBuild( id );
			_meshDistanceFields.Remove( id );
			MdfTotalDataSize -= mdf.DataSize;
		}
	}
}
