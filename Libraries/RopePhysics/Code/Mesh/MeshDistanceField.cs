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
	public float[] SignedDistanceField { get; init; }

	public float this[Vector3Int voxel]
	{
		get
		{
			int x = voxel.x.Clamp( 0, VoxelCount.x - 1 );
			int y = voxel.y.Clamp( 0, VoxelCount.y - 1 );
			int z = voxel.z.Clamp( 0, VoxelCount.z - 1 );
			return SignedDistanceField[Index3DTo1D( x, y, z )];
		}
	}

	public int Index3DTo1D( int x, int y, int z )
	{
		return (z * VoxelCount.y * VoxelCount.x) + (y * VoxelCount.x) + x;
	}
}

public class MeshDistanceField
{
	public struct MeshDistanceSample
	{
		public Vector3Int SampleVoxel { get; set; }
		public Vector3 SampleLocalPosition { get; set; }
		public float SignedDistance { get; set; }
		public Vector3 SurfaceNormal { get; set; }
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

	private Vector3 EstimateVoxelSurfaceNormal( Vector3Int voxel )
	{
		var xOffset = new Vector3Int( 1, 0, 0 );
		var yOffset = new Vector3Int( 0, 1, 0 );
		var zOffset = new Vector3Int( 0, 0, 1 );
		var gradient = new Vector3()
		{
			x = Volume[ voxel + xOffset ] - Volume[ voxel - xOffset ],
			y = Volume[ voxel + yOffset ] - Volume[ voxel - yOffset ],
			z = Volume[ voxel + zOffset ] - Volume[ voxel - zOffset ],
		};
		return gradient.Normal;
	}

	public MeshDistanceSample Sample( Vector3 localSamplePos )
	{
		// Snap sample point to bounds, in case it's out of bounds.
		var closestPoint = Bounds.ClosestPoint( localSamplePos );
		var voxel = PositionToVoxel( closestPoint );
		var signedDistance = Volume[voxel];
		// If we were out of bounds, add the amount by which we were out of bounds to the distance.
		signedDistance += closestPoint.Distance( localSamplePos );
		var surfaceNormal = EstimateVoxelSurfaceNormal( voxel );
		return new MeshDistanceSample()
		{
			SampleVoxel = voxel,
			SampleLocalPosition = closestPoint,
			SurfaceNormal = surfaceNormal,
			SignedDistance = signedDistance,
		};
	}
}
