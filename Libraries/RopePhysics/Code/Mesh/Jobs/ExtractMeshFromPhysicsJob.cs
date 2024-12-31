using Sandbox.Diagnostics;

namespace Duccsoft;

internal class ExtractMeshFromPhysicsJob : Job<PhysicsShape, CpuMeshData>
{
	public ExtractMeshFromPhysicsJob( int id, PhysicsShape shape ) : base( id, shape )
	{
		Shape = shape;
	}

	public PhysicsShape Shape { get; private set; }

    protected override bool RunInternal(out CpuMeshData result)
    {
		Assert.IsValid( Shape );

		Shape.Triangulate( out Vector3[] vertices, out uint[] indices );

		
		result = new CpuMeshData( vertices, indices );
		return true;
	}
}
