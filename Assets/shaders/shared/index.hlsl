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