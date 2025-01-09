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
	private const int POINT_COUNT_MIN = 2;
	private const int POINT_COUNT_MAX = 255;

	[Property] 
	public float TimeStep { get; set; } = 0.01f;

	[Property] 
	public float MaxTimeStepPerUpdate { get; set; } = 0.1f;

	[Property, Range( 0.05f, 32f ), Change] 
	public float PointSpacing { get; set; } = 4f;
	private void OnPointSpacingChanged( float oldValue, float newValue ) => ResetSimulation();

	[Property]
	public float PointCount => SimData?.PointCount ?? 0;

	[Property, Range( 0.01f, 1f )] 
	public float RadiusFraction { get; set; } = 0.25f;

	[Property] 
	public float EffectiveRadius => PointSpacing * ( RadiusFraction * 0.5f );

	[Property, Range( 0f, 1f )] 
	public float Stiffness { get; set; } = 0.2f;


	private int CalculatePointCount( Vector3 startPos, Vector3 endPos )
	{
		float distance = startPos.Distance( endPos );
		int pointCount = (int)(distance / PointSpacing) + 1;
		return pointCount.Clamp( POINT_COUNT_MIN, POINT_COUNT_MAX );
	}

	private void UpdateSimulation()
	{
		SimData.Radius = EffectiveRadius;
		SimData.Iterations = CalculateIterationCount();
	}

	private int CalculateIterationCount()
	{
		var numPoints = SimData.PointCount;
		var min = POINT_COUNT_MIN;
		var max = POINT_COUNT_MAX;

		var minStiffness = MathX.Remap( numPoints, min, max, 0f, 0.025f );
		var stiffness = (Stiffness * 1f).Clamp( minStiffness, 1f );
		var maxIterations = numPoints.Remap( min, max, 1, max );
		return (int)stiffness.Remap( 0.0f, 1f, 1, maxIterations );
	}
	#endregion

	#region Collision
	[Property] public bool EnableCollision { get; set; }
	#endregion

	public RopeSimulationData SimData { get; private set; }

	#region Editor
	private bool _isDraggingMidpointGizmo;
	protected override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( "RopePhysicsDebug" );

		Gizmo.Transform = Scene.LocalTransform;
		var ropeLineColor = Color.Blue;
		using ( Gizmo.Scope( "Midpoint Handle" ) )
		{
			BBox midpointHandle = BBox.FromPositionAndSize( (StartPosition + EndPosition) / 2f, 8f );
			Gizmo.Hitbox.BBox( midpointHandle );
			var drag = Gizmo.GetMouseDrag( midpointHandle.Center, Gizmo.CameraTransform.Backward );
			var wasDraggingMidpointGizmo = _isDraggingMidpointGizmo;
			if ( !Gizmo.IsSelected && Gizmo.Pressed.This )
			{
				Gizmo.Select();
			}
			_isDraggingMidpointGizmo = Gizmo.Pressed.This && drag.Length > 4f;
			var releasedMidpointGizmo = wasDraggingMidpointGizmo && !_isDraggingMidpointGizmo;
			if ( _isDraggingMidpointGizmo || releasedMidpointGizmo )
			{
				midpointHandle = midpointHandle.Translate( -drag );
				ropeLineColor = Color.White;
			}
			if ( releasedMidpointGizmo )
			{
				SplitRope( midpointHandle.Center, 0.5f );
			}
			Gizmo.Draw.Color = Gizmo.IsHovered ? Color.White : Color.Red;
			Gizmo.Draw.LineBBox( midpointHandle );
		}

		Gizmo.Draw.Color = ropeLineColor;
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

	public void SplitRope( Vector3 newRopeStartPosition, float pointSplit )
	{
		var newRope = Duplicate();
		newRope.WorldPosition = newRopeStartPosition;
		EndPoint = newRope.GameObject;
	}

	public VerletRope Duplicate()
	{
		var midGo = new GameObject( true, GameObject.Name );
		midGo.MakeNameUnique();
		var other = midGo.Components.Create<VerletRope>();
		// Anchors
		other.FixedStart = FixedStart;
		other.EndPoint = EndPoint;
		// Simulation
		other.TimeStep = TimeStep;
		other.MaxTimeStepPerUpdate = MaxTimeStepPerUpdate;
		other.PointSpacing = PointSpacing;
		other.RadiusFraction = RadiusFraction;
		other.Stiffness = Stiffness;
		// Collision
		other.EnableCollision = EnableCollision;
		// Rendering
		other.EnableRendering = EnableRendering;
		other.Color = Color;
		return other;
	}
	#endregion

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
		var physics = Scene.PhysicsWorld;
		var startPos = StartPosition;
		var endPos = EndPosition;
		var pointCount = CalculatePointCount( startPos, endPos );
		SimData = new RopeSimulationData( physics, startPos, endPos, pointCount )
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
		Renderer.Width = new Curve( new Curve.Frame( 0f, EffectiveRadius ) );
		Renderer.SplineInterpolation = 8;
		Renderer.VectorPoints = SimData.Points.Select( p => p.Position ).ToList();
	}
	#endregion
}
