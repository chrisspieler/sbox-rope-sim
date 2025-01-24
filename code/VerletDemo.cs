using Duccsoft;
using Duccsoft.ImGui;
using System;

namespace Sandbox;

public class VerletDemo : Component
{
	[Property] public bool ShowWindow { get; set; } = true;
	[Property] public Vector3Int Duplicates { get; set; } = 0;
	[Property] public Vector3 DuplicateOffset { get; set; } = 16f;
	public BBox DuplicateBounds => BBox.FromPositionAndSize( WorldPosition, DuplicateOffset * Duplicates );
	[Property] public bool FollowMouse
	{
		get => _followMouse;
		set
		{
			_followMouse = value;
			FixedStart = true;
			FixedEnd = false;
			OscillateEnd = false;
		}
	}
	private bool _followMouse = true;
	[Property] public bool FixedStart { get; set; } = true;
	[Property] public bool FixedEnd { get; set; } = false;
	[Property] public bool OscillateEnd { get; set; } = false;
	[Property] public Vector2 OscillateEndOffset { get; set; } = new Vector2( 100f, 0f );
	[Property] public Vector2 OscillateEndAmplitude { get; set; } = new Vector2( 0f, 100f );
	[Property] public float OscillateEndPeriod { get; set; } = 1f;
	[Property] public TagSet CollisionInclude { get; set; } = [];
	[Property] public TagSet CollisionExclude { get; set; } = [];
	[Property] public Collider RopeTarget { get; set; }
	public SimulationData Rope { get; set; }

	protected override void OnStart()
	{
		SpawnDuplicates();
	}

	private void SpawnDuplicates()
	{
		for ( int x = 0; x < Duplicates.x; x++ ) 
		{ 
			for ( int y = 0; y < Duplicates.y; y++ ) 
			{ 
				for ( int z = 0; z < Duplicates.z; z++ ) 
				{ 
					SpawnDuplicate( x, y, z );
				}
			}
		}
	}

	private void SpawnDuplicate( int x, int y, int z )
	{
		var pos = DuplicateBounds.Mins + DuplicateOffset * new Vector3( x, y, z );
		var clone = GameObject.Clone( pos );
		var demo = clone.GetComponent<VerletDemo>();
		demo.Duplicates = Vector3Int.Zero;
		demo.ShowWindow = false;
	}

	private void Simulate()
	{
		Rope.CollisionInclude = CollisionInclude;
		Rope.CollisionExclude = CollisionExclude;
		// Rope.Simulate();

		if ( VerletRope.DebugMode < 1 )
			return;

		var points = Rope.Points.ToArray();
		for ( int i = 0; i < points.Length; i++ )
		{
			var point = points[i];
			// DebugOverlay.Sphere( new Sphere( point.Position, Rope.SolidRadius ), color: Color.Red, overlay: true );
			if ( VerletRope.DebugMode == 2 )
			{
				// DebugOverlay.Sphere( new Sphere( point.Position, Rope.CollisionRadius ), color: Color.Green.WithAlpha( 0.05f ) );
			}
			if ( i == 0 )
				continue;

			var lastPoint = points[i - 1];
			DebugOverlay.Line( lastPoint.Position, point.Position, color: Color.Blue, overlay: true );
		}

		if ( VerletRope.DebugMode == 2 )
		{
			DebugOverlay.Box( Rope.Bounds, color: Color.Green.WithAlpha( 0.2f ) );
		}
	}

	protected override void OnFixedUpdate()
	{
		Simulate();
	}

	protected override void OnUpdate()
	{
		if ( Rope is null )
			return;

		if ( !ShowWindow )
			return;

		if ( ImGui.Begin( "Rope Physics Demo" ) )
		{
			DrawWindow();
		}
		ImGui.End();
	}

	private void UpdateInput()
	{
		
	}

	private void DrawWindow()
	{
		
	}
}
