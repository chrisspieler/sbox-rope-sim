namespace Duccsoft;

public class SignedDistanceField
{
	public SignedDistanceField( int[] data, int textureDims, BBox bounds )
	{
		Bounds = bounds;
		TextureSize = textureDims;
		Data = data;
	}

	public bool IsRebuilding { get; set; }
	public BBox Bounds { get; init; }
	public int TextureSize { get; init; }
	// Assume square bounds for now.
	public float TexelSize => Bounds.Size.x / TextureSize;
	public int[] Data { get; internal set; }
	public int DataSize => TextureSize * TextureSize * TextureSize * sizeof( byte );

	public Vector3Int PositionToTexel( Vector3 localPos )
	{
		if ( Data is null )
			return default;

		var normalized = (localPos - Bounds.Mins) / Bounds.Size;
		var texel = normalized * (TextureSize - 1);
		return (Vector3Int)texel;
	}

	public Vector3 TexelToPosition( Vector3Int texel )
	{
		if ( Data is null )
			return default;

		var normalized = (Vector3)texel / TextureSize;
		return Bounds.Mins + normalized * Bounds.Size;
	}

	public BBox TexelToBounds( Vector3Int texel )
	{
		var mins = TexelToPosition( texel );
		var maxs = mins + TexelSize;
		return new BBox( mins, maxs );
	}

	private int Index3DTo1D( int x, int y, int z )
	{
		return (z * TextureSize * TextureSize) + (y * TextureSize) + x;
	}

	public float this[Vector3Int texel]
	{
		get
		{
			int x = texel.x.Clamp( 0, TextureSize - 1 );
			int y = texel.y.Clamp( 0, TextureSize - 1 );
			int z = texel.z.Clamp( 0, TextureSize - 1 );
			int i = Index3DTo1D( x, y, z );
			int packed = Data[i / 4];
			int shift = (i % 4) * 8;
			byte udByte = (byte)((packed >> shift) & 0xFF);
			float sdByte = (float)udByte - 128;
			// Non-uniform bounds will not be supported in the future - assume VoxelGridDims^3 cube!
			var maxDistance = TextureSize * 0.5f;
			return sdByte.Remap( -128, 127, -maxDistance, maxDistance );
		}
	}

	public Vector3 EstimateSurfaceNormal( Vector3Int texel )
	{
		var xOffset = new Vector3Int( 1, 0, 0 );
		var yOffset = new Vector3Int( 0, 1, 0 );
		var zOffset = new Vector3Int( 0, 0, 1 );
		var gradient = new Vector3()
		{
			x = this[texel + xOffset] - this[texel - xOffset],
			y = this[texel + yOffset] - this[texel - yOffset],
			z = this[texel + zOffset] - this[texel - zOffset],
		};
		return gradient.Normal;
	}
}
