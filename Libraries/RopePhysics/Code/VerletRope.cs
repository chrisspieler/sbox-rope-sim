namespace Duccsoft;

public partial class VerletRope : Component, Component.ExecuteInEditor
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
		void UpdateTerminalPositions()
		{
			SimData.FixedFirstPosition = FixedStart ? WorldPosition : null;
			SimData.FixedLastPosition = EndPoint?.WorldPosition;
		}

		if ( SimData is null )
			return;

		if ( Game.IsPlaying )
		{
			UpdateTerminalPositions();
			return;
		}

		var previousFirst = SimData.FixedFirstPosition;
		var previousLast = SimData.FixedLastPosition;
		UpdateTerminalPositions();
		var hasChanged = previousFirst != SimData.FixedFirstPosition || previousLast != SimData.FixedLastPosition;
		if ( hasChanged )
		{
			ResetSimulation();
		}
	}
	#endregion

	#region Simulation
	[Property] public float TimeStep { get; set; } = 0.01f;
	[Property] public float MaxTimeStepPerUpdate { get; set; } = 0.1f;
	[Property, Range(2, 255, 1), Change] public int MaxPoints { get; set; } = 96;
	[Property, Range( 0.05f, 10f)] public float Radius { get; set; } = 1f;
	[Property, Range( 0f, 1f )] public float Stiffness { get; set; } = 0.2f;

	private void OnMaxPointsChanged( int oldValue, int newValue )
	{
		DestroyRope();
		CreateRope();
	}

	private void UpdateSimulation()
	{
		SimData.Radius = Radius;
		var minStiffness = MathX.Remap( MaxPoints, 3, 255, 0f, 0.025f );
		var stiffness = (Stiffness * 1f ).Clamp( minStiffness, 1f );
		var maxIterations = MaxPoints.Remap( 3, 255, 1, 255 );
		SimData.Iterations = (int)stiffness.Remap( 0.0f, 1f, 1, maxIterations );
	}
	#endregion

	#region Collision
	[Property] public bool EnableCollision { get; set; }
	#endregion

	public RopeSimulationData SimData { get; private set; }

	protected override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( "RopePhysicsDebug" );

		Gizmo.Transform = Scene.LocalTransform;
		using ( Gizmo.Scope( "Midpoint Handle" ) )
		{
			BBox midpointHandle = BBox.FromPositionAndSize( (StartPosition + EndPosition) / 2f, 8f );
			Gizmo.Hitbox.BBox( midpointHandle );
			Gizmo.Draw.Color = Gizmo.IsHovered ? Color.White : Color.Red;
			Gizmo.Draw.LineBBox( midpointHandle );
		}

		Gizmo.Draw.Color = Color.Blue;
		using ( Gizmo.Hitbox.LineScope() )
		{
			Gizmo.Draw.Line( StartPosition, EndPosition );
			Gizmo.Hitbox.AddPotentialLine( StartPosition, EndPosition, 32f );
		}
		if ( Gizmo.Pressed.This )
		{
			Gizmo.Select();
		}

		if ( SimData is not null && Gizmo.IsSelected )
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
		UpdateSimulation();
		UpdateRenderer();
	}

	public void ResetSimulation()
	{
		DestroyRope();
		CreateRope();
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
	[Property] public Color Color { get; set; } = Color.Black;

	private LineRenderer Renderer { get; set; }

	private void CreateRenderer()
	{
		if ( !EnableRendering )
			return;

		Renderer?.Destroy();
		Renderer = Components.Create<LineRenderer>();
		Renderer.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;
		Renderer.UseVectorPoints = true;
		Renderer.EndCap = Renderer.StartCap = SceneLineObject.CapStyle.Rounded;
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

		Renderer.Color = new Gradient( new Gradient.ColorFrame( 0f, Color ) );
		Renderer.Width = new Curve( new Curve.Frame( 0f, Radius ) );
		Renderer.SplineInterpolation = 8;
		Renderer.VectorPoints = SimData.Points.Select( p => p.Position ).ToList();
	}
	#endregion
}
