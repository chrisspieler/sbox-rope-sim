using Duccsoft;
using Duccsoft.ImGui;

namespace Sandbox;

public class MdfModelViewer : Component
{
	[Property] public bool ShouldDrawOctree { get; set; } = true;
	[Property] public bool ShowTextureViewer { get; set; } = false;
	public GameObject SelectedMeshGameObject { get; set; }
	public MeshDistanceField Mdf { get; set; }

	protected override void OnUpdate()
	{
		if ( Mdf is null )
			return;

		if ( ShowTextureViewer )
		{
			PaintTextureViewer();
		}

		if ( !SelectedMeshGameObject.IsValid() )
			return;

		UpdateInput();
		PaintInstanceViewer();
		DrawInstanceViewerOverlay();
	}

	#region GameObject Viewer

	private float MouseSignedDistance { get; set; }
	private void UpdateInput()
	{

	}

	private void PaintInstanceViewer()
	{
		if ( ImGui.Begin( $"Mesh Distance Field ({SelectedMeshGameObject?.Name ?? "null"})" ) )
		{
			if ( !SelectedMeshGameObject.IsValid() )
			{
				ImGui.Text( "No instance is selected." );
				return;
			}

			if ( Mdf is null )
			{
				ImGui.Text( "No mesh distance field exists for this instance." );
				return;
			}

			PaintInstanceStats();
			if ( ImGui.Button( "Regenerate MDF" ) )
			{
				MeshDistanceSystem.Current.RemoveMdf( Mdf.Id );
			}
		}
		ImGui.End();
	}

	private void PaintInstanceStats()
	{
		if ( Mdf.IsBuilding )
		{
			ImGui.Text( $"Building, elapsed time: {Mdf.SinceBuildStarted.Relative:F3}ms" );
		}
		else
		{
			float buildTime = Mdf.SinceBuildStarted - Mdf.SinceBuildFinished;
			ImGui.Text( $"Build complete in: {buildTime:F3}ms" );
		}
		if ( Mdf.VertexCount < 0 )
			return;
		ImGui.Text( $"size: {Mdf.Bounds.Size.x:F2},{Mdf.Bounds.Size.y:F2},{Mdf.Bounds.Size.z:F2} tris: {Mdf.TriangleCount}" );
		var drawOctree = ShouldDrawOctree;
		ImGui.Checkbox( "Draw Octree", ref drawOctree );
		ShouldDrawOctree = drawOctree;
		ImGui.Text( $"Octree Leaves: {Mdf.OctreeLeafCount}/{Mdf.OctreeLeafCount + Mdf.QueuedJumpFloodJobs}" );
		ImGui.Text( $"Data Size: {Mdf.DataSize.FormatBytes():F3}" );
	}

	private void DrawInstanceViewerOverlay()
	{
		if ( Mdf is null || !SelectedMeshGameObject.IsValid() )
			return;

		var tx = SelectedMeshGameObject.WorldTransform;

		if ( ShouldDrawOctree )
		{
			// Draw the octree
			Mdf.DebugDraw( tx );
		}
	}
	#endregion

	#region Texture Viewer
	private void PaintTextureViewer()
	{
		if ( Mdf is null )
			return;
	}

	#endregion
}
