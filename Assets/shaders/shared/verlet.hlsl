struct VerletPoint
{
    float3 Position;
    int Flags;
    float3 LastPosition;
    int Padding;

    bool IsAnchor() { return ( Flags & 1 ) == 1; }
    bool IsRopeLocal() { return ( Flags & 2 ) == 2; }
};

struct VerletPointUpdate
{
    float3 Position;
    int Index;
    int UpdateFlags;
    int PointFlags;
    int2 Padding;
    
    bool ShouldUpdatePosition() { return ( UpdateFlags & 1 ) == 1; }
    bool ShouldUpdateFlags() { return ( UpdateFlags & 2 ) == 2; }
};