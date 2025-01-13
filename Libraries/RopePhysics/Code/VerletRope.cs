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
	private const int POINT_COUNT_MAX = 1024;
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
		var numPoints = SimData.CpuPoints.Length;
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
			BBox midpointHandle = BBox.FromPositionAndSize( (FirstRopePointPosition + LastRopePointPosition) / 2f, 8f );
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
			Gizmo.Draw.Line( FirstRopePointPosition, LastRopePointPosition );
			Gizmo.Hitbox.AddPotentialLine( FirstRopePointPosition, LastRopePointPosition, 32f );
		}
		if ( Gizmo.Pressed.This )
		{
			Gizmo.Select();
		}

		if ( SimData is null || !Gizmo.IsSelected )
			return;

		if ( DebugDrawPoints )
		{
			Gizmo.Draw.Color = Color.Green;
			foreach ( var point in SimData.CpuPoints )
			{
				Gizmo.Draw.LineSphere( point.Position, SimData.Radius );
			}
		}
	}

	public void SplitRope( Vector3 newRopeStartPosition, float pointSplit )
	{
		var newRope = Duplicate();
		newRope.WorldPosition = newRopeStartPosition;
		EndTarget = newRope.GameObject;
	}

	public VerletRope Duplicate()
	{
		var midGo = new GameObject( true, GameObject.Name );
		midGo.MakeNameUnique();
		var other = midGo.Components.Create<VerletRope>();
		// Anchors
		other.FixedStart = FixedStart;
		other.EndTarget = EndTarget;
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
		var startPos = FirstRopePointPosition;
		var endPos = LastRopePointPosition;
		var pointCount = CalculatePointCount( startPos, endPos );
		var points = RopeGenerator.Generate( startPos, endPos, pointCount, out float segmentLength );
		var simData = new SimulationData( physics, points, 1, segmentLength );
		simData.AnchorToStart( StartPosition );
		simData.AnchorToEnd( EndPosition );
		simData.InitializeGpu();
		return simData;
	}

	#region Rendering
	
	[Property] public Color Color { get; set; } = Color.Black;
	[Property] public bool Wireframe { get; set; } = false;
	[Property, Range( 0.05f, 50f )] public float RenderWidthScale { get; set; } = 1f;
	private SceneRopeObject _so;

	protected override void CreateRenderer()
	{
		if ( !EnableRendering )
			return;

		_so = new SceneRopeObject( Scene.SceneWorld );
	}

	private void EnsureRenderer()
	{
		if ( !_so.IsValid() )
		{
			CreateRenderer();
		}
	}

	protected override void DestroyRenderer()
	{
		_so?.Delete();
		_so = null;
	}

	protected override void UpdateRenderer()
	{
		EnsureRenderer();

		if ( !_so.IsValid() )
			return;

		if ( SimulateOnGPU )
		{
			_so.Vertices = SimData.ReadbackVertices;
		}

		_so.RenderingEnabled = true;
		_so.Transform = WorldTransform;
		_so.Bounds = BBox.FromPositionAndSize( ( FirstRopePointPosition + LastRopePointPosition ) / 2f, 512f );
		_so.Face = SceneRopeObject.FaceMode.Camera;
		_so.StartCap = SceneRopeObject.CapStyle.Rounded;
		_so.EndCap = SceneRopeObject.CapStyle.Rounded;
		_so.Opaque = true;
		_so.EnableLighting = true;
		_so.Wireframe = Wireframe;
		_so.ColorTint = Color;
		_so.Flags.CastShadows = true;
	}

	public override void UpdateCpuVertexBuffer( VerletPoint[] points )
	{
		if ( !_so.IsValid() )
			return;

		int vertexCount = points.Length + 2;
		if ( !_so.Vertices.IsValid() || _so.Vertices.ElementCount != vertexCount )
		{
			_so.Vertices ??= new GpuBuffer<VerletVertex>( vertexCount, GpuBuffer.UsageFlags.Vertex );
		}
		var vertices = new VerletVertex[vertexCount];
		vertices[0] = points[0].AsRopeVertex( this );
		if ( points.Length > 2 )
		{
			for ( int i = 0; i < points.Length; i++ )
			{
				var p = points[i];
				var vtx = p.AsRopeVertex( this );
				vertices[i + 1] = vtx;
			}
		}
		vertices[^1] = points[^1].AsRopeVertex( this );
		_so.Vertices.SetData( vertices );
	}
	#endregion
}
