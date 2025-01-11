using Sandbox.Utility;

namespace Duccsoft;

public abstract class VerletComponent : Component, Component.ExecuteInEditor
{
	public SimulationData SimData { get; protected set; }

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

	[ConVar( "verlet_gpu_sim" )]
	public static bool SimulateOnGPUConVar { get; set; } = true;
	[Property]
	public bool SimulateOnGPU
	{
		get => SimulateOnGPUConVar;
		set
		{
			SimulateOnGPUConVar = value;
		}
	}

	[Property]
	public float TimeStep { get; set; } = 0.01f;

	[Property]
	public float MaxTimeStepPerUpdate { get; set; } = 0.1f;

	[Property]
	public float PointCount => SimData?.Points?.Length ?? 0;

	[Property, Range( 0.01f, 1f )]
	public float Stiffness { get; set; } = 0.2f;
	#endregion

	#region Collision
	[Property] public bool EnableCollision { get; set; }
	#endregion

	protected override void OnEnabled() => CreateSimulation();
	protected override void OnDisabled() => DestroySimulation();

	protected override void OnUpdate()
	{
		UpdateAnchors();
	}

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
		CreateRenderer();
	}

	protected virtual void DestroySimData() 
	{
		SimData?.DestroyGpuData();
		SimData = null;
	}

	protected virtual void DestroyRenderer() { }
	protected abstract SimulationData CreateSimData();
	protected virtual void CreateRenderer() { }

	protected virtual void UpdateRenderer() { }

	[Property] public string SimulationFrameTime
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

	internal void PushDebugTime( double time )
	{
		_debugTimes.PushFront( time );
	}

	[Button]
	public void DumpSimData()
	{
		if ( SimData?.Points == null )
		{
			Log.Info( $"null points" );
			return;
		}

		for ( int i = 0; i < SimData.Points.Length; i++ )
		{
			var point = SimData.Points[i];
			var x = i % SimData.PointGridDims.x;
			var y = i / SimData.PointGridDims.y;
			Log.Info( $"({x},{y}) pos{point.Position}, lastPos: {point.LastPosition}" );
		}
	}
}
