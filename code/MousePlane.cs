namespace Sandbox;

public class MousePlane : Component
{
	[Property] public GameObject Cursor { get; set; }
	[Property, Range( 0f, 24f )] public float CursorTranslationSmoothing { get; set; } = 4f;
	[Property, Group( "Trace" )] public bool TwoSided { get; set; } = true;
	[Property, Group( "Trace" )] public float MaxDistance { get; set; } = float.PositiveInfinity;

	public Plane Plane => new Plane( WorldPosition, WorldTransform.Forward );

    protected override void DrawGizmos()
    {
		Gizmo.Draw.Color = Color.Blue;
		var planeBbox = BBox.FromPositionAndSize( Vector3.Zero, new Vector3( 1f, 128f, 128f ) );
		// Plane
		Gizmo.Draw.LineBBox( planeBbox );

		var top = planeBbox.Center.WithZ( planeBbox.Maxs.z * 2 );
		var bottom = planeBbox.Center.WithZ( planeBbox.Mins.z * 2 );
		Gizmo.Draw.Line( top, bottom );
		var left = planeBbox.Center.WithY( planeBbox.Maxs.y * 2 );
		var right = planeBbox.Center.WithY( planeBbox.Mins.y * 2 );
		Gizmo.Draw.Line( left, right );
		Gizmo.Draw.Color = Color.Red;

		// Normal
		Gizmo.Draw.Line( planeBbox.Center, planeBbox.Center + Vector3.Forward * 32f );
    }

    protected override void OnUpdate()
    {
		if ( !Cursor.IsValid() )
			return;

		var mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );
		if ( !Plane.TryTrace( mouseRay, out Vector3 hitPosition, TwoSided, MaxDistance ) )
			return;

		var targetPos = hitPosition;
		if ( CursorTranslationSmoothing > 0f )
		{
			var decay = CursorTranslationSmoothing.Clamp( 0f, 23.9f );
			decay = 24f - decay;
			targetPos = Cursor.WorldPosition.ExpDecayTo( targetPos, decay );
		}
		Cursor.WorldPosition = targetPos;
    }
}
