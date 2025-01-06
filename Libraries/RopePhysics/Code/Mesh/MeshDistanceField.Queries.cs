namespace Duccsoft;

public partial class MeshDistanceField
{
	public struct MeshDistanceSample
	{
		public float SignedDistance { get; set; }
		public Vector3 Gradient { get; set; }
	}

	public SparseVoxelOctree<SignedDistanceField>.OctreeNode Trace( Vector3 localPos, Vector3 localDir, out float hitDistance )
		=> Trace( localPos, localDir, out hitDistance, new Vector3Int( -1, -1, -1 ) );

	public SparseVoxelOctree<SignedDistanceField>.OctreeNode Trace( Vector3 localPos, Vector3 localDir, out float hitDistance, Vector3Int filter )
	{
		localPos = LocalPosToOctree( localPos );
		hitDistance = -1f;
		return Octree?.Trace( localPos, localDir, out hitDistance, filter );
	}

	public SignedDistanceField GetSdfTexture( Vector3Int voxel )
	{
		if ( Octree?.HasLeaf( voxel ) != true )
			return null;

		return Octree?.Find( voxel )?.Data;
	}

	public bool TrySample( Vector3 localSamplePos, out MeshDistanceSample sample )
	{
		// Snap sample point to bounds, in case it's out of bounds.
		var closestPoint = Bounds.ClosestPoint( localSamplePos );
		if ( Octree is null )
		{
			sample = default;
			return false;
		}
		var octreePos = LocalPosToOctree( closestPoint );
		var voxel = Octree.Find( octreePos );
		if ( voxel?.Data is null )
		{
			// TODO: Trace ray to nearest leaf and sample its SDF instead.
			sample = default;
			return false;
		}
		var sdf = voxel.Data;
		var texel = sdf.PositionToTexel( closestPoint );
		var signedDistance = sdf[texel];
		// If we were out of bounds, add the amount by which we were out of bounds to the distance.
		signedDistance += closestPoint.Distance( localSamplePos );
		sample = new MeshDistanceSample()
		{
			Gradient = sdf.CalculateGradient( texel ),
			SignedDistance = signedDistance,
		};
		return true;
	}
}
