namespace Duccsoft;

public class SignedDistanceField
{
	public class DebugData
	{
		public DebugData( SignedDistanceField sdf )
		{
			Sdf = sdf;
		}
		public SignedDistanceField Sdf { get; }

		internal CpuMeshData MeshData;
		public MeshSeedData[] SeedData;
		public int EmptySeedCount;
		public int[] SeedVoxels;
		public int[] VoxelSeedIds;

		public int GetSeedId( Vector3Int texel )
		{
			var i = Index3DTo1D( texel.x, texel.y, texel.z, Sdf.TextureSize );
			return VoxelSeedIds[i];
		}

		public Vector3 GetSeedPosition( Vector3Int texel )
		{
			var seedData = SeedData[ GetSeedId( texel ) ];
			return seedData.Position;
		}

		public Triangle? GetSeedTriangle( int seedId )
		{
			var emptySeedStartId = MeshData.TriangleCount * 4;
			if ( seedId < 0 || seedId >= emptySeedStartId )
				return null;

			int centerId = seedId - seedId % 4;
			var v0 = SeedData[centerId + 1].Position;
			var v1 = SeedData[centerId + 2].Position;
			var v2 = SeedData[centerId + 3].Position;
			return new Triangle( v0, v1, v2 );
		}
	}

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
	public DebugData Debug { get; set; }

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

	public static int Index3DTo1D( int x, int y, int z, int size )
	{
		return (z * size * size) + (y * size) + x;
	}

	public static Vector3Int Index1DTo3D( int i, int size )
	{
		int x = i / (size * size);
		int y = (i / size) % size;
		int z = i % size;
		return new Vector3Int( x, y, z );
	}

	public float this[Vector3Int texel]
	{
		get
		{
			int x = texel.x.Clamp( 0, TextureSize - 1 );
			int y = texel.y.Clamp( 0, TextureSize - 1 );
			int z = texel.z.Clamp( 0, TextureSize - 1 );
			int i = Index3DTo1D( x, y, z, TextureSize );
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
