namespace Duccsoft;

internal class GpuMeshData
{
	public GpuMeshData( CpuMeshData cpuMesh, GpuBuffer<Vector4> vertices, GpuBuffer<uint> indices )
	{
		CpuMesh = cpuMesh;
		Vertices = vertices;
		Indices = indices;
	}
	public CpuMeshData CpuMesh { get; }
	public BBox Bounds => CpuMesh.Bounds;
	public GpuBuffer<Vector4> Vertices { get; }
	public GpuBuffer<uint> Indices { get; }
}
