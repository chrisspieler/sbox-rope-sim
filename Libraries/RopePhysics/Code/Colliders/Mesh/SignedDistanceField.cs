namespace Duccsoft;

public partial class SignedDistanceField
{
	public SignedDistanceField( byte[] data, int textureDims, BBox bounds )
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
	public byte[] Data { get; internal set; }
	public int DataSize => Data.Length;
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
			byte udByte = Data[i];
			float sdByte = (float)udByte - 128;
			// Non-uniform bounds will not be supported in the future - assume VoxelGridDims^3 cube!
			var maxDistance = TextureSize * 0.5f;
			return sdByte.Remap( -128, 127, -maxDistance, maxDistance );
		}
	}


	public Vector3 CalculateGradient( Vector3Int texel )
	{
		// This method was adapted from: https://shaderfun.com/2018/07/23/signed-distance-fields-part-8-gradients-bevels-and-noise/

		var d = this[texel];
		var sign = MathF.Sign( d );
		var maxValue = TextureSize * sign;

		// X
		var xOffset = new Vector3Int( 1, 0, 0 );
		var x0 = texel.x > 0 ? this[texel - xOffset] : maxValue;
		var x1 = texel.x < (TextureSize - 1) ? this[texel + xOffset] : maxValue;
		// Y
		var yOffset = new Vector3Int( 0, 1, 0 );
		var y0 = texel.y > 0 ? this[texel - yOffset] : maxValue;
		var y1 = texel.y < (TextureSize - 1) ? this[texel + yOffset] : maxValue;
		// Z
		var zOffset = new Vector3Int( 0, 0, 1 );
		var z0 = texel.z > 0 ? this[texel - zOffset] : maxValue;
		var z1 = texel.z < (TextureSize - 1) ? this[texel + zOffset] : maxValue;

		float ddx = sign * x0 < sign * x1 ? -(x0 - d) : (x1 - d);
		float ddy = sign * y0 < sign * y1 ? -(y0 - d) : (y1 - d);
		float ddz = sign * z0 < sign * z1 ? -(z0 - d) : (z1 - d);

		return new Vector3( ddx, ddy, ddz ).Normal;
	}
}
