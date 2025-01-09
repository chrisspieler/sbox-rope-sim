namespace Duccsoft;

public partial class VerletRope : Component
{
	[ConVar( "rope_debug" )]
	public static int DebugMode { get; set; } = 0;

	#region Anchors
	[Property]
	public Vector3 StartPosition
	{
		get
		{
			if ( FixedStart )
			{
				return WorldPosition;
			}
			return SimData?.FirstPosition ?? WorldPosition;
		}
	}
	[Property]
	public Vector3 EndPosition
	{
		get
		{
			if ( EndPoint.IsValid() )
			{
				return EndPoint.WorldPosition;
			}
			return SimData?.LastPosition ?? WorldPosition + Vector3.Down * 50f;
		}
	}
	[Property] public bool FixedStart { get; set; } = true;
	[Property] public GameObject EndPoint { get; set; }

	private void UpdateAnchors()
	{
		if ( SimData is null )
			return;

		SimData.FixedFirstPosition = FixedStart ? WorldPosition : null;
		SimData.FixedLastPosition = EndPoint?.WorldPosition;
	}
	#endregion

	#region Simulation
	[Property] public float TimeStep { get; set; } = 0.01f;
	[Property] public float MaxTimeStepPerUpdate { get; set; } = 0.1f;
	[Property, Range(0, 255, 1)] public int MaxPoints { get; set; } = 96;
	[Property] public float Radius { get; set; } = 1f;
	#endregion

	#region Collision
	[Property] public bool EnableCollision { get; set; }
	#endregion

	public RopeSimulationData SimData { get; private set; }

	protected override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( "RopePhysicsDebug" );

		Gizmo.Transform = Scene.LocalTransform;
		Gizmo.Draw.Color = Color.Blue;
		Gizmo.Draw.Line( StartPosition, EndPosition );
		BBox midpointHandle = BBox.FromPositionAndSize( (StartPosition + EndPosition) / 2f, 8f );
		Gizmo.Hitbox.BBox( midpointHandle );
		Gizmo.Draw.Color = Gizmo.IsHovered ? Color.White : Color.Red;
		Gizmo.Draw.LineBBox( midpointHandle );

		if ( SimData is not null )
		{
			Gizmo.Draw.Color = Color.Green;
			foreach( var point in SimData.Points )
			{
				Gizmo.Draw.LineSphere( point.Position, SimData.Radius );
			}
		}
	}

	protected override void OnEnabled() => CreateRope();
	protected override void OnDisabled() => DestroyRope();
	protected override void OnUpdate()
	{
		UpdateAnchors();
		UpdateRenderer();
	}

	private void CreateRope()
	{
		SimData = new RopeSimulationData( Scene.PhysicsWorld, StartPosition, EndPosition, MaxPoints )
		{
			FixedFirstPosition = FixedStart ? WorldPosition : null,
			FixedLastPosition = EndPoint?.WorldPosition,
		};
		CreateRenderer();
	}

	private void DestroyRope()
	{
		SimData = null;
		DestroyRenderer();
	}

	#region Rendering
	[Property, Change] public bool EnableRendering { get; set; } = true;
	private LineRenderer Renderer { get; set; }

	private void CreateRenderer()
	{
		if ( !EnableRendering )
			return;

		Renderer?.Destroy();
		Renderer = Components.Create<LineRenderer>();
		Renderer.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;
		Renderer.UseVectorPoints = true;
		Renderer.Width = new Curve( new Curve.Frame( 0f, Radius * 2 ) );
		Renderer.EndCap = SceneLineObject.CapStyle.Triangle;
	}

	private void DestroyRenderer()
	{
		Renderer?.Destroy();
		Renderer = null;
	}

	private void OnEnableRenderingChanged( bool oldValue, bool newValue )
	{
		if ( newValue )
		{
			CreateRenderer();
		}
		else
		{
			DestroyRenderer();
		}
	}

	[Button]
	private void ToggleHideRenderer()
	{
		if ( !Renderer.IsValid() )
			return;

		var wasSet = Renderer.Flags.HasFlag( ComponentFlags.Hidden );
		Renderer.Flags = Renderer.Flags.WithFlag( ComponentFlags.Hidden, !wasSet );
	}

	private void UpdateRenderer()
	{
		if ( !Renderer.IsValid() )
			return;

		Renderer.VectorPoints = SimData.Points.Select( p => p.Position ).ToList();
	}
	#endregion
}
