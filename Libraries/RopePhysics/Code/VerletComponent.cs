using Sandbox.Diagnostics;
using Sandbox.Utility;
using System.Text.Json.Serialization;

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

	[ConCmd( "verlet_gpu_sim" )]
	public static void SimulateAllOnGpu( bool simulateOnGpu )
	{
		if ( !Game.ActiveScene.IsValid() )
			return;

		var verletComponents = Game.ActiveScene.GetAllComponents<VerletComponent>();
		foreach( var verlet in verletComponents )
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

	[Property]
	public float PointCount
	{
		get
		{
			if ( SimulateOnGPU )
			{
				return SimData?.GpuPoints?.ElementCount ?? 0;
			}
			else
			{
				return SimData?.CpuPoints?.Length ?? 0;
			}
		}
	}

	[Property, Range( 0.01f, 1f )]
	public float Stiffness { get; set; } = 0.2f;

	[Property, ReadOnly, JsonIgnore]
	public float IterationCount => SimData?.Iterations ?? 0;
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
			var y = i / SimData.PointGridDims.y;
			Log.Info( $"({x},{y}) pos{point.Position}, lastPos: {point.LastPosition}" );
		}

		if ( SimulateOnGPU )
		{
			Log.Info( $"Got {SimData.CpuPoints?.Length ?? 0} points from GPU in {elapsedMilliseconds:F3}ms" );
		}
	}
}
