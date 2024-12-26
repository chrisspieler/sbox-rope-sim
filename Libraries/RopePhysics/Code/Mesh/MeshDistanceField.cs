namespace Duccsoft;

public struct MeshSeedData
{
	public Vector4 Position;
	public Vector4 Normal;
}

public class MeshVolumeData
{
	public Texture Texture { get; init; }
	public Vector3Int VoxelCount { get; init; }
	public int[] Voxels { get; init; }
	public MeshSeedData[] Seeds { get; init; }

	public int this[int x, int y, int z] => Voxels[Index3DTo1D( x, y, z )];

	public int Index3DTo1D( int x, int y, int z )
	{
		return (z * VoxelCount.y * VoxelCount.x) + (y * VoxelCount.x) + x;
	}
}

public class MeshDistanceField
{
	public struct MeshDistanceSample
	{
		public Vector3 SamplePosition { get; set; }
		public Vector3 Direction { get; set; }
		public float SignedDistance { get; set; }
		public Vector3 SurfaceNormal { get; set; }
		public Vector3 SurfacePosition => SamplePosition + Direction * ( SignedDistance < 0 ? -SignedDistance : SignedDistance );
	}


	public MeshDistanceField( int id, MeshVolumeData data, BBox localBounds )
	{
		Id = id;
		Volume = data;
		Bounds = localBounds;
		VoxelSize = Bounds.Size / Volume.VoxelCount;
	}

	public int Id { get; }
	public MeshVolumeData Volume { get; }
	public BBox Bounds { get; }
	public Vector3 VoxelSize { get; }
	public bool IsInBounds( Vector3 localPos ) => Bounds.Contains( localPos );

	public Vector3Int PositionToVoxel( Vector3 localPos )
	{
		var normalized = ( localPos - Bounds.Mins ) / Bounds.Size;
		var voxel = normalized * ( Volume.VoxelCount - 1 );
		return (Vector3Int)voxel;
	}

	public MeshDistanceSample Sample( Vector3 localPos )
	{
		var closestPoint = Bounds.ClosestPoint( localPos );
		var extraDistance = closestPoint.Distance( localPos );

		localPos = closestPoint;
		var voxel = PositionToVoxel( localPos );
		var seedId = Volume[voxel.x, voxel.y, voxel.z];
		var sign = seedId >= 0 ? 1 : -1;
		seedId = Math.Abs( seedId );
		var seedData = Volume.Seeds[ seedId ];
		var unsignedDistance = localPos.Distance( seedData.Position ) + extraDistance;
		return new MeshDistanceSample()
		{
			SamplePosition = localPos,
			SurfaceNormal = seedData.Normal,
			SignedDistance = unsignedDistance * sign,
			Direction = Vector3.Direction( localPos, seedData.Position ),
		};
	}
}
