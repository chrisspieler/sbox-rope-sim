using Duccsoft.ImGui;

namespace Sandbox
{
	public class MainMenuSystem : GameObjectSystem
	{
		public MainMenuSystem( Scene scene ) : base( scene )
		{
			Listen( Stage.StartUpdate, 1000, Update, "Update MainMenu" );
		}

		private void Update()
		{
			if ( Scene?.Camera?.IsValid() != true )
				return;

			var text = new TextRendering.Scope( "Press ESC to return to main menu", Color.White, ImGui.GetTextLineHeight(), "Consolas" );
			Scene.Camera.Hud.DrawText( text, new Vector2( Screen.Width / 2, Screen.Height * 0.925f ) );

			if ( Input.EscapePressed )
			{
				Scene.LoadFromFile( $"scenes/main_menu.scene" );
			}
		}
	}
}
