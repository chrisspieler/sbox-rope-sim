﻿using Sandbox.Diagnostics;
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
			return SimData?.LastPosition ?? WorldPosition + Vector3.Down * 50f;
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
	[Property] public GameObject StartTarget { get; set; }
	[Property] public GameObject EndTarget { get; set; }
	[Property, ReadOnly, JsonIgnore] 
	public Vector3 StartPosition => StartTarget?.WorldPosition ?? WorldPosition;
	[Property, ReadOnly, JsonIgnore]
	public Vector3 EndPosition
	{
		get
		{
			if ( EndTarget.IsValid() )
				return EndTarget.WorldPosition;

			return EndTarget?.WorldPosition ?? WorldPosition + Vector3.Right * 128f;
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
	#region Rendering
	[Property] public bool DebugDrawPoints { get; set; } = false;

	protected virtual void CreateRenderer() { }
	protected virtual void DestroyRenderer() { }

	protected virtual void UpdateRenderer() { }
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
			Log.Info( $"({x},{y}) anchor: {point.IsAnchor}, pos{point.Position}, lastPos: {point.LastPosition}" );
		}

		if ( SimulateOnGPU )
		{
			Log.Info( $"Got {SimData.CpuPoints?.Length ?? 0} points from GPU in {elapsedMilliseconds:F3}ms" );
		}
	}
}
