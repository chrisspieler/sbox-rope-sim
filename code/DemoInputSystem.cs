namespace Sandbox;

public class DemoInputSystem : GameObjectSystem<DemoInputSystem>
{
	public DemoInputSystem( Scene scene ) : base(scene)
	{
		Listen( Stage.StartUpdate, 0, Update, "Update ImGui Input" );
	}

	private void Update()
	{
		Mouse.Visible = true;
	}
}
