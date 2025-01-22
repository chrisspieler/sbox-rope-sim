using Sandbox.Diagnostics;
using Sandbox.Utility;
using System.Text.Json.Serialization;

namespace Duccsoft;

public abstract class VerletComponent : Component, Component.ExecuteInEditor
{
	public SimulationData SimData { get; protected set; }

	#region Anchors
	[Property]
	public Vector3 FirstRopePointPosition
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
	public Vector3 LastRopePointPosition
	{
		get
		{
			if ( EndTarget.IsValid() )
			{
				return EndTarget.WorldPosition;
			}
			return SimData?.LastPosition ?? WorldPosition + Vector3.Down * DefaultLength;
		}
	}
	[Property, Change] public bool FixedStart { get; set; } = true;
	private void OnFixedStartChanged( bool oldValue, bool newValue )
	{
		SimData?.AnchorToStart( FixedStart ? StartPosition : null );
	}
	[Property, Change] public bool FixedEnd { get; set; } = false;
	private void OnFixedEndChanged( bool oldValue, bool newValue )
	{
		SimData?.AnchorToEnd( FixedEnd ? EndPosition : null );
	}
	[Property, Change] public GameObject StartTarget { get; set; }
	private void OnStartTargetChanged( GameObject oldValue, GameObject newValue )
	{

	}
	[Property, Change] public GameObject EndTarget { get; set; }
	private void OnEndTargetChanged( GameObject oldValue, GameObject newValue )
	{

	}
	[Property, ReadOnly, JsonIgnore] 
	public Vector3 StartPosition => StartTarget?.WorldPosition ?? WorldPosition;
	[Property, Range( 1f, 1000f)]
	public float DefaultLength { get; set; } = 128f;
	[Property, ReadOnly, JsonIgnore]
	public Vector3 EndPosition
	{
		get
		{
			if ( EndTarget.IsValid() )
				return EndTarget.WorldPosition;

			return EndTarget?.WorldPosition ?? WorldPosition + Vector3.Right * DefaultLength;
		}
	}

	private Vector3 _lastStartPosition;
	private Vector3 _lastEndPosition;

	private void UpdateAnchors()
	{
		if ( SimData is null )
			return;

		Vector3 startTranslation = StartPosition - _lastStartPosition;
		Vector3 endTranslation = EndPosition - _lastEndPosition;
		Vector3 ropeTranslation = SimData.Translation;
		if ( FixedStart && MathF.Abs( ropeTranslation.DistanceSquared( startTranslation ) ) > 0.001f )
		{
			SimData.SetPointPosition( Vector2Int.Zero, StartPosition );
		}

		if ( FixedEnd && MathF.Abs( ropeTranslation.DistanceSquared( endTranslation ) ) > 0.001f )
		{
			SimData.SetPointPosition( new Vector2Int( SimData.PointGridDims.x - 1, 0 ), EndPosition );
		}

		_lastStartPosition = StartPosition;
		_lastEndPosition = EndPosition;
	}
	#endregion

	#region Rendering
	[Property, Change] public bool EnableRendering { get; set; } = true;
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
	#endregion

	#region Simulation

	protected override void OnUpdate()
	{
		UpdateAnchors();
	}

	[ConCmd( "verlet_gpu_sim" )]
	public static void SimulateAllOnGpu( bool simulateOnGpu )
	{
		if ( !Game.ActiveScene.IsValid() )
			return;

		var verletComponents = Game.ActiveScene.GetAllComponents<VerletComponent>();
		foreach ( var verlet in verletComponents )
		{
			verlet.SimulateOnGPU = simulateOnGpu;
		}
	}
	public static bool SimulateOnGPUConVar { get; set; } = true;

	[Property]
	public bool SimulateOnGPU
	{
		get => _simulateOnGpu;
		set
		{
			_simulateOnGpu = value;
			if ( value )
			{
				SimData?.StorePointsToGpu();
			}
			else
			{
				SimData?.LoadPointsFromGpu();
			}
		}
	}
	private bool _simulateOnGpu = true;

	[Property]
	public float TimeStep { get; set; } = 0.01f;

	[Property]
	public float MaxTimeStepPerUpdate { get; set; } = 0.1f;

	[Property, ReadOnly, JsonIgnore]
	public float SegmentLength => SimData?.SegmentLength ?? 1f;
	[Property, Range( 0, 1 )]
	public float Stretchiness
	{
		get => SimData?.AnchorMaxDistanceFactor ?? _stretchiness;
		set
		{
			_stretchiness = value;
			if ( SimData is not null )
			{
				SimData.AnchorMaxDistanceFactor = value;
			}
		}
	}
	private float _stretchiness = 0;

	[Property, ReadOnly, JsonIgnore]
	public int PointCount
	{
		get
		{
			if ( SimulateOnGPU && SimData?.GpuPoints.IsValid() == true )
			{
				return SimData.GpuPoints.ElementCount;
			}
			else if ( SimData?.CpuPoints is not null )
			{
				return SimData.CpuPoints.Length;
			}
			else
			{
				var distance = StartPosition.Distance( EndPosition );
				return (int)(distance / SegmentLength);
			}
		}
	}
	[Property] public BBox Bounds => SimData?.Bounds ?? default;

	[Property, Range( 4, 80, 1 )]
	public int Iterations { get; set; } = 20;

	#endregion

	#region Collision
	[Property] public bool EnableCollision { get; set; }
	#endregion

	protected override void OnEnabled()
	{
		StartTarget ??= GameObject;
		CreateSimulation();
	}
	protected override void OnDisabled() => DestroySimulation();

	#region Simulation

	[Button]
	public void ResetSimulation()
	{
		DestroySimulation();
		CreateSimulation();
	}

	private void DestroySimulation()
	{
		DestroySimData();
		DestroyRenderer();
	}

	private void CreateSimulation()
	{
		SimData = CreateSimData();
		SimData.Transform = WorldTransform;
		SimData.LastTransform = WorldTransform;
		SimData.AnchorToStart( FixedStart ? StartPosition : null );
		SimData.AnchorToEnd( FixedEnd ? EndPosition : null );
		CreateRenderer();
	}

	protected virtual void DestroySimData() 
	{
		SimData?.DestroyGpuData();
		SimData = null;
	}

	protected abstract SimulationData CreateSimData();

	public void SetPointPosition( Vector2Int pointCoord, Vector3 worldPos ) 
		=> SimData?.SetPointPosition( pointCoord, worldPos );

#endregion
	#region Rendering
	[Property] public bool DebugDrawPoints { get; set; } = false;

	protected virtual void CreateRenderer() { }
	protected virtual void DestroyRenderer() { }
	public abstract void UpdateCpuVertexBuffer( VerletPoint[] points );
	#endregion
	[Property, ReadOnly, JsonIgnore] public string SimulationCpuTime
	{
		get
		{
			if ( !_debugTimes.IsEmpty )
			{
				return $" {_debugTimes.Average():F3}ms";
			}
			return string.Empty;
		}
	}
	private CircularBuffer<double> _debugTimes = new CircularBuffer<double>( 25 );
	[Property, ReadOnly, JsonIgnore] public int GpuPendingUpdates => SimData?.PendingPointUpdates ?? 0;

	protected override void DrawGizmos()
	{
		if ( SimData is null )
			return;

		using var scope = Gizmo.Scope( "VerletComponent" );
		Gizmo.Transform = Scene.LocalTransform;

		var bounds = SimData.Bounds;
		Gizmo.Hitbox.BBox( bounds );

		if ( !Gizmo.IsSelected )
		{
			if ( Gizmo.IsHovered )
			{
				Gizmo.Draw.Color = Color.White.WithAlpha( 0.2f );
				Gizmo.Draw.LineBBox( SimData.Bounds );
				if ( Gizmo.Pressed.This )
				{
					Gizmo.Select();
				}
			}
			return;
		}

		if ( !Gizmo.IsSelected )
			return;

		var alpha = Gizmo.IsHovered ? 1f : 0.2f;
		Gizmo.Draw.Color = Color.White.WithAlpha( 0.2f );
		Gizmo.Draw.LineBBox( SimData.Bounds );
	}

	internal void PushDebugTime( double time )
	{
		_debugTimes.PushFront( time );
	}

	[Button]
	public void DumpSimData()
	{
		if ( SimData is null )
			return;

		var timer = FastTimer.StartNew();
		if ( SimulateOnGPU )
		{
			SimData.LoadPointsFromGpu();
		}

		var elapsedMilliseconds = timer.ElapsedMilliSeconds;
		if ( SimData?.CpuPoints == null )
		{
			Log.Info( $"null points" );
			return;
		}

		for ( int i = 0; i < SimData.CpuPoints.Length; i++ )
		{
			var point = SimData.CpuPoints[i];
			var x = i % SimData.PointGridDims.x;
			var y = i / SimData.PointGridDims.x;
			Log.Info( $"({x},{y}) anchor: {point.IsAnchor}, pos{point.Position}, lastPos: {point.LastPosition}" );
		}

		if ( SimulateOnGPU )
		{
			Log.Info( $"Got {SimData.CpuPoints?.Length ?? 0} points from GPU in {elapsedMilliseconds:F3}ms" );
		}
	}
}
