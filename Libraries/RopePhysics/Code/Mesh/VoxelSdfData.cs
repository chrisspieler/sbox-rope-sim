namespace Duccsoft;

public class VoxelSdfData
{
	public VoxelSdfData( int[] voxelSdf, int voxelGridDims, BBox bounds )
	{
		Bounds = bounds;
		VoxelGridDims = voxelGridDims;
		VoxelSdf = voxelSdf;
	}

	public BBox Bounds { get; init; }
	public int VoxelGridDims { get; init; }
	public int[] VoxelSdf { get; init; }
	public int DataSize => VoxelGridDims * VoxelGridDims * VoxelGridDims * sizeof( byte );

	public Vector3Int PositionToVoxel( Vector3 localPos )
	{
		if ( VoxelSdf is null )
			return default;

		var normalized = (localPos - Bounds.Mins) / Bounds.Size;
		var voxel = normalized * (VoxelGridDims - 1);
		return (Vector3Int)voxel;
	}

	public Vector3 VoxelToPosition( Vector3Int voxel )
	{
		if ( VoxelSdf is null )
			return default;

		var normalized = (Vector3)voxel / VoxelGridDims;
		var positionCorner = Bounds.Mins + normalized * Bounds.Size;
		return positionCorner + MeshDistanceSystem.VoxelSize * 0.5f;
	}

	private int Index3DTo1D( int x, int y, int z )
	{
		return (z * VoxelGridDims * VoxelGridDims) + (y * VoxelGridDims) + x;
	}

	public float this[Vector3Int voxel]
	{
		get
		{
			
			int x = voxel.x.Clamp( 0, VoxelGridDims - 1 );
			int y = voxel.y.Clamp( 0, VoxelGridDims - 1 );
			int z = voxel.z.Clamp( 0, VoxelGridDims - 1 );
			int i = Index3DTo1D( x, y, z );
			int packed = VoxelSdf[i / 4];
			int shift = (i % 4) * 8;
			byte udByte = (byte)((packed >> shift) & 0xFF);
			float sdByte = (float)udByte - 128;
			// Non-uniform bounds will not be supported in the future - assume VoxelGridDims^3 cube!
			var maxDistance = VoxelGridDims * 0.5f;
			return sdByte.Remap( -128, 127, -maxDistance, maxDistance );
		}
	}

	public Vector3 EstimateVoxelSurfaceNormal( Vector3Int voxel )
	{
		var xOffset = new Vector3Int( 1, 0, 0 );
		var yOffset = new Vector3Int( 0, 1, 0 );
		var zOffset = new Vector3Int( 0, 0, 1 );
		var gradient = new Vector3()
		{
			x = this[voxel + xOffset] - this[voxel - xOffset],
			y = this[voxel + yOffset] - this[voxel - yOffset],
			z = this[voxel + zOffset] - this[voxel - zOffset],
		};
		return gradient.Normal;
	}
}
