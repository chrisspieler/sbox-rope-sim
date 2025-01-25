using Sandbox.Diagnostics;

namespace Duccsoft;

internal class ConvertMeshToGpuJob : Job<CpuMeshData, GpuMeshData>
{
	public ConvertMeshToGpuJob( int id, CpuMeshData cpuData ) : base( id, cpuData ) { }
	
	protected override bool RunInternal( out GpuMeshData result )
	{
		var cpuVertices = Input?.Vertices;
		var cpuIndices = Input?.Indices;
		Assert.NotNull( cpuVertices);
		Assert.NotNull( cpuIndices );

		Vector4[] vertices;
		int vertexCount = cpuVertices.Length;
		int indexCount = cpuIndices.Length;
		using ( PerfLog.Scope( Id, "Convert Vector3 -> Vector4" ) )
		{
			vertices = new Vector4[vertexCount];
			for ( int i = 0; i < vertexCount; i++ )
			{
				vertices[i] = new Vector4( cpuVertices[i] );
			}
		}
		GpuBuffer<Vector4> vtxBuffer;
		using ( PerfLog.Scope( Id, "Fill Vertex Buffer" ) )
		{
			vtxBuffer = new GpuBuffer<Vector4>( vertexCount, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
			vtxBuffer.SetData( vertices );
		}
		GpuBuffer<uint> idxBuffer;
		using ( PerfLog.Scope( Id, "Fill Index Buffer" ) )
		{
			idxBuffer = new GpuBuffer<uint>( indexCount, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Index );
			idxBuffer.SetData( cpuIndices );
		}
		result = new GpuMeshData( Input, vtxBuffer, idxBuffer );
		return true;
	}
}
