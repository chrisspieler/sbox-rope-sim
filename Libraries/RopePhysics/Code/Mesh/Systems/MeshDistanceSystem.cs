namespace Duccsoft;

public partial class MeshDistanceSystem : GameObjectSystem<MeshDistanceSystem>
{
	[ConVar( "rope_mdf_voxel_density" )]
	public static float VoxelsPerWorldUnit { get; set; } = 1f;
	public static float VoxelSize => 1f / VoxelsPerWorldUnit;

	public MeshDistanceSystem( Scene scene ) : base( scene ) { }

	private readonly Dictionary<int, MeshDistanceField> _meshDistanceFields = new();

	public int MdfCount => _meshDistanceFields.Count;
	public int MdfTotalDataSize { get; private set; }

	internal void AddMdf( int id, MeshDistanceField mdf )
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
			MeshDistanceBuildSystem.Current.StopBuild( id );
			MdfTotalDataSize -= mdf.DataSize;
		}
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
		foreach ( var foundMdf in _meshDistanceFields.Values )
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

		var builder = MeshDistanceBuildSystem.Current;
		// Should we begin building a mesh distance field?
		if ( !builder.IsBuilding( id ) )
		{
			builder.BuildFromPhysicsShape( id, shape );
		}

		return false;
	}


}
