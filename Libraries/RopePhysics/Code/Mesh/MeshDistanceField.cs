namespace Duccsoft;

public struct MeshSeedData
{
	public Vector4 Position;
	public Vector4 Normal;
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


	public MeshDistanceField( int id, int volumeSize, float[] voxelSdf, BBox localBounds )
	{
		Id = id;
		Bounds = localBounds;
		VolumeSize = volumeSize;
		VoxelSdf = voxelSdf;

		var boundsMax = Math.Max( Math.Max( Bounds.Size.x, Bounds.Size.y ), Bounds.Size.z );
		VoxelSize = boundsMax / VolumeSize;
	}

	public int Id { get; }
	public BBox Bounds { get; }
	public float VoxelSize { get; }
	public int VolumeSize { get; init; }
	public float[] VoxelSdf { get; init; }

	public float this[Vector3Int voxel]
	{
		get
		{
			int x = voxel.x.Clamp( 0, VolumeSize - 1 );
			int y = voxel.y.Clamp( 0, VolumeSize - 1 );
			int z = voxel.z.Clamp( 0, VolumeSize - 1 );
			return VoxelSdf[Index3DTo1D( x, y, z )];
		}
	}
	private int Index3DTo1D( int x, int y, int z )
	{
		return (z * VolumeSize * VolumeSize ) + (y * VolumeSize ) + x;
	}

	public bool IsInBounds( Vector3 localPos ) => Bounds.Contains( localPos );

	public Vector3Int PositionToVoxel( Vector3 localPos )
	{
		var normalized = ( localPos - Bounds.Mins ) / Bounds.Size;
		var voxel = normalized * ( VolumeSize - 1 );
		return (Vector3Int)voxel;
	}

	private Vector3 EstimateVoxelSurfaceNormal( Vector3Int voxel )
	{
		var xOffset = new Vector3Int( 1, 0, 0 );
		var yOffset = new Vector3Int( 0, 1, 0 );
		var zOffset = new Vector3Int( 0, 0, 1 );
		var gradient = new Vector3()
		{
			x = this[ voxel + xOffset ] - this[ voxel - xOffset ],
			y = this[ voxel + yOffset ] - this[ voxel - yOffset ],
			z = this[ voxel + zOffset ] - this[ voxel - zOffset ],
		};
		return gradient.Normal;
	}

	public MeshDistanceSample Sample( Vector3 localSamplePos )
	{
		// Snap sample point to bounds, in case it's out of bounds.
		var closestPoint = Bounds.ClosestPoint( localSamplePos );
		var voxel = PositionToVoxel( closestPoint );
		var signedDistance = this[voxel];
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
