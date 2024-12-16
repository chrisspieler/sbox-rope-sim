namespace Duccsoft;

public partial class RopePhysics
{
	[ConVar( "rope_debug" )]
	public static int DebugMode { get; set; } = 0;

	private void UpdateDebug()
	{
		if ( DebugMode < 1 )
			return;

		for ( int i = 0; i < RopePoints.Count; i++ )
		{
			var point = RopePoints[i];
			DebugOverlay.Sphere( new Sphere( point.WorldPosition, point.Thickness ), color: Color.Red, overlay: true );
			if ( i > 0 )
			{
				var previous = RopePoints[i - 1];
				DebugOverlay.Line( previous.WorldPosition, point.WorldPosition, color: Color.Blue, overlay: true );
			}
		}
	}
}
