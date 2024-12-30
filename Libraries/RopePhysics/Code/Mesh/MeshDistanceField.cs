namespace Duccsoft;

public class MeshDistanceField
{
	public struct MeshDistanceSample
	{
		public float SignedDistance { get; set; }
		public Vector3 SurfaceNormal { get; set; }
	}

	private MeshDistanceField() { }
	internal MeshDistanceField( int id, GpuMeshData meshData )
	{
		Id = id;
		MeshData = meshData;
	}

	public int Id { get; }
	internal GpuMeshData MeshData { get; }
	public BBox Bounds => MeshData.Bounds;
	public VoxelSdfData VoxelSdf { get; internal set; }
	public int DataSize => VoxelSdf?.DataSize ?? 0;
	
	public SparseVoxelOctree<VoxelSdfData> Octree { get; internal set; }

	public bool IsInBounds( Vector3 localPos ) => Bounds.Contains( localPos );

	public Vector3Int PositionToVoxel( Vector3 localPos )
	{
		var normalized = ( localPos - Bounds.Mins ) / Bounds.Size;
		var voxel = normalized * ( VoxelSdf.VoxelGridDims - 1 );
		return (Vector3Int)voxel;
	}

	public Vector3 VoxelToPositionCenter( Vector3Int voxel )
	{
		var normalized = (Vector3)voxel / VoxelSdf.VoxelGridDims;
		var positionCorner = Bounds.Mins + normalized * Bounds.Size;
		return positionCorner + MeshDistanceSystem.VoxelSize * 0.5f;
	}

	public MeshDistanceSample Sample( Vector3 localSamplePos )
	{
		// Snap sample point to bounds, in case it's out of bounds.
		var closestPoint = Bounds.ClosestPoint( localSamplePos );
		var voxel = PositionToVoxel( closestPoint );
		var signedDistance = VoxelSdf[voxel];
		// If we were out of bounds, add the amount by which we were out of bounds to the distance.
		signedDistance += closestPoint.Distance( localSamplePos );
		var surfaceNormal = VoxelSdf.EstimateVoxelSurfaceNormal( voxel );
		return new MeshDistanceSample()
		{
			SurfaceNormal = surfaceNormal,
			SignedDistance = signedDistance,
		};
	}
}
