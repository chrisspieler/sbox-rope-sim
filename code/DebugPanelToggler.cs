using Duccsoft.ImGui;

namespace Sandbox;

public class DebugPanelToggler : Component
{
	[ConCmd( "verlet_debug_menu" )]
	public static void DisplayDebugMenu()
	{
		if ( !Game.IsPlaying || Game.ActiveScene is not Scene scene )
			return;

		var component = scene.GetAllComponents<DebugPanelToggler>().FirstOrDefault();

		if ( !component.IsValid() )
		{
			var menuGo = new GameObject( scene, true, "Debug Menu" );
			component = menuGo.AddComponent<DebugPanelToggler>();
		}

		component.ShowDebugMenu = true;
	}

	[Property] public bool ShowDebugMenu { get; set; }
	[Property] public bool ShowStatsWindow { get; set; }

	protected override void OnUpdate()
	{
		if ( ShowDebugMenu )
		{
			ImGui.SetNextWindowPos( new Vector2( 0.05f ) );
			if ( ImGui.Begin( "Debug Panels" ) )
			{
				this.ImGuiInspector();
			}
			ImGui.End();
		}

		if ( ShowStatsWindow )
		{
			DebugPanels.StatsWindow();
		}
	}
}
