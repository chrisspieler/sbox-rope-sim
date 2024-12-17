using Duccsoft;
using Duccsoft.ImGui;

namespace Sandbox;

public class RopeModificationDemo : Component
{
	[Property] public RopePhysics TargetRope { get; set; }

	private int _ropePointIndex = 1;

    protected override void OnUpdate()
    {
		ImGui.SetNextWindowPos( new Vector2( 50, 50 ) * ImGuiStyle.UIScale );
        if ( ImGui.Begin( "Rope Modification" ) )
		{
			WindowContents();
		}
		ImGui.End();
    }

	private void WindowContents()
	{
		if ( !TargetRope.IsValid() )
		{
			ImGui.Text( $"No {nameof( RopePhysics )} was assigned to {nameof( TargetRope )}" );
			return;
		}

		// Debug mode
		var enableDebug = RopePhysics.DebugMode > 0;
		ImGui.Checkbox( "Debug", ref enableDebug );
		RopePhysics.DebugMode = enableDebug ? 1 : 0;

		// Debug info
		ImGui.Text( $"Rope point count: {TargetRope.PointCount}" );
		ImGui.NewLine();

		// Transform
		ImGui.Text( "Position:" ); ImGui.SameLine();
		var localPos = new Vector2( -TargetRope.LocalPosition.y, TargetRope.LocalPosition.z );
		ImGui.SliderFloat2( "LocalPosition", ref localPos, -100f, 100f );
		TargetRope.LocalPosition = new Vector3( 0f, -localPos.x, localPos.y );

		// Rope point index
		_ropePointIndex = _ropePointIndex.Clamp( 0, TargetRope.PointCount );
		ImGui.Text( "Index:" ); ImGui.SameLine();
		ImGui.SliderInt( "RopePointIndex", ref _ropePointIndex, 0, TargetRope.PointCount );

		// Function buttons
		ImGui.Text( "Add rope at..." );
		if ( ImGui.Button( "Specified Index" ) )
		{
			TargetRope.AddRopePoint( _ropePointIndex );
		}
		ImGui.SameLine();
		if ( ImGui.Button( "Rope Start" ) )
		{
			TargetRope.AddRopeBeginning();
		}
		ImGui.SameLine();
		if ( ImGui.Button( "Rope End" ) )
		{
			TargetRope.AddRopeEnd();
		}
		ImGui.SameLine();
	}
}
