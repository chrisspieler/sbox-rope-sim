namespace Duccsoft;

public partial class VerletRope : VerletComponent
{
	[ConVar( "rope_debug" )]
	public static int DebugMode { get; set; } = 0;

	#region Simulation

	[Property, Range( 0.01f, 1f )] 
	public float RadiusFraction { get; set; } = 0.25f;

	[Property] 
	public float EffectiveRadius => PointSpacing * ( RadiusFraction * 0.5f );
	#endregion

	#region Generation
	private const int POINT_COUNT_MIN = 2;
	private const int POINT_COUNT_MAX = 255;
	[Property, Range( 0.05f, 32f ), Change]
	public float PointSpacing { get; set; } = 4f;
	private void OnPointSpacingChanged( float oldValue, float newValue ) => ResetSimulation();

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
		var numPoints = SimData.Points.Length;
		var min = POINT_COUNT_MIN;
		var max = POINT_COUNT_MAX;

		var minStiffness = MathX.Remap( numPoints, min, max, 0f, 0.025f );
		var stiffness = (Stiffness * 1f).Clamp( minStiffness, 1f );
		var maxIterations = numPoints.Remap( min, max, 1, max );
		return (int)stiffness.Remap( 0.0f, 1f, 1, maxIterations );
	}
	#endregion

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


	protected override void OnUpdate()
	{
		base.OnUpdate();
		UpdateSimulation();
		UpdateRenderer();
	}

	protected override SimulationData CreateSimData()
	{
		var physics = Scene.PhysicsWorld;
		var startPos = StartPosition;
		var endPos = EndPosition;
		var pointCount = CalculatePointCount( startPos, endPos );
		var points = RopeGenerator.Generate( startPos, endPos, pointCount, out float segmentLength );
		var sticks = new VerletStickConstraint[points.Length - 1];
		for( int i = 0; i < sticks.Length; i++ )
		{
			sticks[i] = new VerletStickConstraint( i, i + 1, segmentLength );
		}
		var simData = new SimulationData( physics, points, sticks, 1, segmentLength )
		{
			FixedFirstPosition = FixedStart ? WorldPosition : null,
			FixedLastPosition = EndPoint?.WorldPosition,
		};
		simData.InitializeGpu();
		return simData;
	}

	#region Rendering
	
	[Property] public Color Color { get; set; } = Color.Black;

	private LineRenderer Renderer { get; set; }

	protected override void CreateRenderer()
	{
		if ( !EnableRendering )
			return;

		Renderer?.Destroy();
		Renderer = Components.Create<LineRenderer>();
		Renderer.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;
		Renderer.UseVectorPoints = true;
		Renderer.EndCap = Renderer.StartCap = SceneLineObject.CapStyle.Rounded;
		Renderer.Lighting = true;
	}

	protected override void DestroyRenderer()
	{
		Renderer?.Destroy();
		Renderer = null;
	}

	[Button]
	private void ToggleHideRenderer()
	{
		if ( !Renderer.IsValid() )
			return;

		var wasSet = Renderer.Flags.HasFlag( ComponentFlags.Hidden );
		Renderer.Flags = Renderer.Flags.WithFlag( ComponentFlags.Hidden, !wasSet );
	}

	protected override void UpdateRenderer()
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
