struct VerletPoint
{
    int Flags;
    float3 Position;
    float3 LastPosition;

    bool IsAnchor() { return ( Flags & 1 ) == 1; }
    bool IsRopeLocal() { return ( Flags & 2 ) == 2; }

};

struct VerletPointUpdate
{
    float3 Position;
    int Index;
    int UpdateFlags;
    int PointFlags;
    
    bool ShouldUpdatePosition() { return ( UpdateFlags & 1 ) == 1; }
    bool ShouldUpdateFlags() { return ( UpdateFlags & 2 ) == 2; }
};