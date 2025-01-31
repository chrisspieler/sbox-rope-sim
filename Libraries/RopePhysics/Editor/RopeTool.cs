using Editor;
using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Duccsoft
{
	/// <summary>
	/// Create, modify, and simulate ropes
	/// </summary>
	[EditorTool]
	[Title( "Rope" )]
	[Icon( "linear_scale")]
	public class RopeTool : EditorTool
	{
		public VerletSystem System
		{
			get
			{
				_system ??= VerletSystem.Get( Scene );
				return _system;
			}
		}
		private VerletSystem _system;

		VerletSystem.SimulationScope SimulationScope
		{
			get => System.SceneSimulationScope;
			set
			{
				System.SceneSimulationScope = value;
			}
		}
		
		public override void OnEnabled()
		{
			WidgetWindow window = new(SceneOverlay, "Rope Tool")
			{
				Layout = Layout.Column()
			};
			window.Layout.Margin = 16;

			Layout row = window.Layout.AddRow();
			row.Spacing = 10;
			Label dropdownLabel = new( "Simulate:" );
			row.Add( dropdownLabel );

			ComboBox simModeDropdown = new( null );
			simModeDropdown.AddItem( "None", icon: "disabled_by_default", onSelected: () => SimulationScope = VerletSystem.SimulationScope.None, selected: SimulationScope == VerletSystem.SimulationScope.None );
			simModeDropdown.AddItem( "Selected", icon: "indeterminate_check_box", onSelected: () => SimulationScope = VerletSystem.SimulationScope.SimulationSet, selected: SimulationScope == VerletSystem.SimulationScope.SimulationSet );
			simModeDropdown.AddItem( "All", icon: "check_box", onSelected: () => SimulationScope = VerletSystem.SimulationScope.All, selected: SimulationScope == VerletSystem.SimulationScope.All );
			row.Add( simModeDropdown );

			AddOverlay( window, TextFlag.RightTop, 10 );
		}
	}
}
