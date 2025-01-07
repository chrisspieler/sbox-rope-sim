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
		sample = default;

		if ( !Bounds.Contains( localSamplePos ) || Octree is null )
			return false;

		var octreePos = LocalPosToOctree( localSamplePos );
		var voxel = Octree.Find( octreePos );
		if ( voxel?.Data is null )
			return false;

		var sdf = voxel.Data;
		if ( !sdf.Bounds.Contains( localSamplePos ) )
			return false;

		var texel = sdf.PositionToTexel( localSamplePos );
		var signedDistance = sdf[texel];
		sample = new MeshDistanceSample()
		{
			Gradient = sdf.CalculateGradient( texel ),
			SignedDistance = signedDistance,
		};
		return true;
	}
}
