using System;
using Duccsoft.ImGui;

namespace Sandbox;

public class MainMenuUI : Component
{
	private record SceneMetadata( SceneFile Scene, int Priority, string Name, string Description, Texture Thumbnail );
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
		try
		{
			GetAllSceneMetadata( ResourceLibrary.GetAll<SceneFile>() );
		}
		catch ( InvalidOperationException e )
		{
			Log.Info( $"Working around issue # 7548" );
			GetAllSceneMetadata( ResourceLibrary.GetAll<SceneFile>().ToList() );
			throw;
		}
	}

	private void GetAllSceneMetadata( IEnumerable<SceneFile> scenes )
	{
		foreach( var scene in scenes )
		{
			var demoName = scene.GetMetadata( nameof( DemoSceneInformation.DemoName ) );
			if ( demoName is null )
				continue;

			var demoDescription = scene.GetMetadata( nameof( DemoSceneInformation.DemoDescription ) );
			var demoThumbnailPath = scene.GetMetadata( nameof( DemoSceneInformation.ThumbnailPath ) );
			if ( !int.TryParse( scene.GetMetadata( nameof( DemoSceneInformation.Priority ) ), out int priority ) )
			{
				priority = 100;
			}
			Texture demoThumbnail = null;
			if ( !string.IsNullOrWhiteSpace( demoThumbnailPath ) )
			{
				demoThumbnail = Texture.Load( FileSystem.Mounted, demoThumbnailPath );
			}
			else
			{
				Log.Info( $"null thumnail for: {demoName}" );
			}
			SceneMetadata metadata = new( scene, priority, demoName, demoDescription, demoThumbnail );
			Metadata.Add( metadata );
			Metadata = [.. Metadata.OrderBy( m => m.Priority )];
		}
		if ( Metadata.Count > 0 )
		{
			SelectedSceneIndex = 0;
		}
	}
	
	protected override void OnUpdate()
	{
		var titleText = new TextRendering.Scope( "Rope Physics Demo", Color.White, ImGui.GetTextLineHeight() * 5f, "Poppins" );
		Scene.Camera.Hud.DrawText( titleText, new Vector2( Screen.Width / 2, Screen.Height * 0.15f ) );
		ImGui.SetNextWindowPos( Screen.Size * new Vector2( 0.05f, 0.225f ) );
		if ( ImGui.Begin( "Main Menu - Rope Physics Demo" ) )
		{
			PaintWindow();
		}
		ImGui.End();
	}

	private void PaintWindow()
	{
		ImGui.Text( $"INSTRUCTIONS: Choose a scene below." );
		ImGui.Text( "Press ESC at any time to return to this menu." );
		ImGui.NewLine();
		if ( SelectedScene is null )
			return;

		var sceneText = Metadata.Count == 1 ? "scene" : "scenes";
		ImGui.Text( $"{Metadata.Count} test {sceneText} available so far!" );
		if ( ImGui.Button( " < " ) )
		{
			SelectedSceneIndex--;
		}
		ImGui.SameLine();
		ImGui.Text( SelectedScene.Name ); 
		ImGui.SameLine();
		if ( ImGui.Button( " > " ) )
		{
			SelectedSceneIndex++;
		}
		if ( SelectedScene.Thumbnail is not null )
		{
			ImGui.Image( SelectedScene.Thumbnail, 512 * ImGuiStyle.UIScale, Color.White, ImGui.GetColorU32( ImGuiCol.Border ) );
		}
		ImGui.Text( SelectedScene.Description );
		if ( ImGui.Button( "Play Scene" ) )
		{
			Scene.Load( SelectedScene.Scene );
		}
	}
}
