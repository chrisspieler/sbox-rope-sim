using Duccsoft.ImGui;

namespace Sandbox;

public class MainMenuUI : Component
{
	private record SceneMetadata( SceneFile Scene, string Name, string Description );
	private List<SceneMetadata> Metadata { get; set; } = [];
	private int SelectedSceneIndex
	{
		get => _selectedSceneIndex.UnsignedMod( Metadata.Count );
		set
		{
			_selectedSceneIndex = value.UnsignedMod( Metadata.Count );
			SelectedScene = Metadata[_selectedSceneIndex];
		}
	}
	private int _selectedSceneIndex;
	private SceneMetadata SelectedScene { get; set; }

	protected override void OnStart()
	{
		var scenes = ResourceLibrary.GetAll<SceneFile>();
		foreach( var scene in scenes )
		{
			var demoName = scene.GetMetadata( nameof( DemoSceneInformation.DemoName ) );
			if ( demoName is null )
				continue;

			var demoDescription = scene.GetMetadata( nameof( DemoSceneInformation.DemoDescription ) );
			SceneMetadata metadata = new( scene, demoName, demoDescription );
			Metadata.Add( metadata );
		}
		if ( Metadata.Count > 0 )
		{
			SelectedSceneIndex = 0;
		}
	}
	protected override void OnUpdate()
	{
		if ( ImGui.Begin( "Main Menu - Rope Simulation Demo" ) )
		{
			PaintWindow();
		}
		ImGui.End();
	}

	private void PaintWindow()
	{
		ImGui.Text( $"INSTRUCTIONS: Choose a scene below. Press ESC to return to this menu." );
		ImGui.NewLine();
		if ( SelectedScene is null )
			return;

		if ( Metadata.Count > 1 && ImGui.Button( "<" ) )
		{
			SelectedSceneIndex--;
		}
		ImGui.SameLine();
		ImGui.Text( SelectedScene.Name ); 
		ImGui.SameLine();
		if ( Metadata.Count > 1 && ImGui.Button( ">") )
		{
			SelectedSceneIndex++;
		}
		ImGui.Text( SelectedScene.Description );
		if ( ImGui.Button( "Play Scene" ) )
		{
			Scene.Load( SelectedScene.Scene );
		}
	}
}
