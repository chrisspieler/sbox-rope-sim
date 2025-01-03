using Sandbox.Diagnostics;

namespace Duccsoft;

internal class ExtractMeshFromModelJob : Job<Model, CpuMeshData>
{
	public ExtractMeshFromModelJob( int id, Model model ) : base( id, model )
	{
		Model = model;
	}

	public Model Model { get; private set; }

	protected override bool RunInternal( out CpuMeshData result )
	{
		Assert.IsValid( Model );

		var indices = Model.GetIndices();
		var vertices = Model.GetVertices()
			.Select( vtx => vtx.Position )
			.ToArray();

		result = new CpuMeshData( vertices, indices );
		return true;
	}
}
