namespace Duccsoft;

internal class MeshDistanceField
{
	public struct MeshDistanceSample
	{
		public float SignedDistance { get; set; }
		// TODO: Make sure this is actually signed rather than unsigned distance.
		public Vector3 Direction { get; set; }
	}

	public MeshDistanceField( int id, Texture volumeTex, BBox localBounds )
	{
		Id = id;
		VolumeTexture = volumeTex;
		Bounds = localBounds;
		VoxelSize = Bounds.Size / new Vector3( VolumeTexture.Size, VolumeTexture.Depth );
	}

	public int Id { get; }
	public Texture VolumeTexture { get; }
	public BBox Bounds { get; }
	public Vector3 VoxelSize { get; }
	public bool IsInBounds( Vector3 localPos ) => Bounds.Contains( localPos );

	public Vector3Int PositionToTexel( Vector3 localPos )
	{
		var normal = ( localPos - Bounds.Mins ) / Bounds.Size;
		var texel = normal * new Vector3( VolumeTexture.Size, VolumeTexture.Depth );
		return (Vector3Int)texel;
	}

	public MeshDistanceSample Sample( Vector3 localPos )
	{
		// Clamp sample point to bounds.
		if ( !IsInBounds( localPos ) )
			localPos = Bounds.ClosestPoint( localPos );

		var texel = PositionToTexel( localPos );
		var color = VolumeTexture.GetPixel3D( texel.x, texel.y, texel.z );
		return new MeshDistanceSample()
		{
			SignedDistance = color.a,
			Direction = new Vector3( color.r, color.g, color.g ),
		};
	}
}
