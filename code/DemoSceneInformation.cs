namespace Sandbox;

public class DemoSceneInformation : Component, ISceneMetadata
{
	[Property] public string DemoName { get; set; }
	[Property] public string DemoDescription { get; set; }
	[Property, ImageAssetPath] public string ThumbnailPath { get; set; }
	[Property] public int Priority { get; set; }

	public Dictionary<string, string> GetMetadata()
	{
		return new() {
			{ nameof(DemoName), DemoName},
			{ nameof(DemoDescription), DemoDescription },
			{ nameof(ThumbnailPath), ThumbnailPath },
			{ nameof(Priority), Priority.ToString() }
		};
	}
}
