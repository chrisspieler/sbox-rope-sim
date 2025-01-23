uint Convert3DIndexTo1D( uint3 i, uint3 dims )
	{
	return ( i.z * dims.y * dims.x ) + ( i.y * dims.x ) + i.x;
	}

uint3 Convert1DIndexTo3D( uint i, uint3 dims )
{
    int z = i / ( dims.x * dims.y );
    int y = ( i / dims.x ) % dims.x;
    int x = i % dims.x;
    return uint3( x, y, z );
}

uint Convert2DIndexTo1D( uint2 i, uint2 dims )
{
    return ( i.y * dims.x ) + i.x;
}

uint2 Convert1DIndexTo2D( uint i, uint2 dims )
{
    return uint2(
		i % dims.x,
		i / dims.x
	);
}