namespace Duccsoft;

public class MeshDistanceField
{
	public struct MeshDistanceSample
	{
		public Vector3Int SampleVoxel { get; set; }
		public Vector3 SampleLocalPosition { get; set; }
		public float SignedDistance { get; set; }
		public Vector3 SurfaceNormal { get; set; }
	}


	public MeshDistanceField( int id, int voxelGridDims, int[] voxelSdf, BBox localBounds )
	{
		Id = id;
		Bounds = localBounds;
		VoxelGridDims = voxelGridDims;
		VoxelSdf = voxelSdf;
		VoxelSize = Bounds.Size / VoxelGridDims;
	}

	public int Id { get; }
	public BBox Bounds { get; }
	public Vector3 VoxelSize { get; }
	public int VoxelGridDims { get; init; }
	public int[] VoxelSdf { get; init; }
	public int DataSize => VoxelGridDims * VoxelGridDims * VoxelGridDims * sizeof( byte );

	public float this[Vector3Int voxel]
	{
		get
		{
			int x = voxel.x.Clamp( 0, VoxelGridDims - 1 );
			int y = voxel.y.Clamp( 0, VoxelGridDims - 1 );
			int z = voxel.z.Clamp( 0, VoxelGridDims - 1 );
			int i = Index3DTo1D( x, y, z );
			int packed = VoxelSdf[i / 4];
			int shift = ( i % 4 ) * 8;
			byte udByte = (byte)( ( packed >> shift ) & 0xFF);
			float sdByte = (float)udByte - 128;
			// Non-uniform bounds will not be supported in the future - assume VoxelGridDims^3 cube!
			var maxDistance = VoxelGridDims * 0.5f;
			return sdByte.Remap( -128, 127, -maxDistance, maxDistance );
		}
	}
	private int Index3DTo1D( int x, int y, int z )
	{
		return (z * VoxelGridDims * VoxelGridDims) + (y * VoxelGridDims) + x;
	}

	public bool IsInBounds( Vector3 localPos ) => Bounds.Contains( localPos );

	public Vector3Int PositionToVoxel( Vector3 localPos )
	{
		var normalized = ( localPos - Bounds.Mins ) / Bounds.Size;
		var voxel = normalized * ( VoxelGridDims - 1 );
		return (Vector3Int)voxel;
	}

	public Vector3 VoxelToPositionCenter( Vector3Int voxel )
	{
		var normalized = (Vector3)voxel / VoxelGridDims;
		var positionCorner = Bounds.Mins + normalized * Bounds.Size;
		return positionCorner + VoxelSize * 0.5f;
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
