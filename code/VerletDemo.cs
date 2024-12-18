using Duccsoft;
using Duccsoft.ImGui;
using System;

namespace Sandbox;

public class VerletDemo : Component
{
	[Property, Range(0, 200)]
	public int Iterations 
	{
		get => _iterations;
		set
		{
			_iterations = value;
			if ( Rope is not null )
			{
				Rope.Iterations = value;
			}
		}
	}
	private int _iterations = 3;
	[Property]
	public Vector3 RopeStartOffset { get; set; } = Vector3.Zero;
	public Vector3 RopeStart => WorldPosition + RopeStartOffset;
	[Property]
	public Vector3 RopeEndOffset { get; set; } = Vector3.Right * 50f;
	public Vector3 RopeEnd => WorldPosition + RopeEndOffset;
	[Property, Range( 2, 200 )]
	public int RopePointCount 
	{
		get => _ropePointCount;
		set
		{
			var temp = _ropePointCount;
			_ropePointCount = value;
			if ( temp != value && Rope is not null )
			{
				Rope.PointCount = value;
				Rope.Reset();
			}
		}
	}
	private int _ropePointCount = 32;
	[Property] public bool FixedStart { get; set; } = true;
	[Property] public bool FixedEnd { get; set; } = false;
	[Property] public bool OscillateEnd { get; set; } = false;
	[Property] public Vector2 OscillateEndOffset { get; set; } = new Vector2( 100f, 0f );
	[Property] public Vector2 OscillateEndAmplitude { get; set; } = new Vector2( 0f, 100f );
	[Property] public float OscillateEndPeriod { get; set; } = 1f;
	public VerletRope Rope { get; set; }

	protected override void OnStart()
	{
		RopePhysics.DebugMode = 1;
		Rope = new( RopeStart, RopeEnd, RopePointCount )
		{
			Iterations = Iterations,
			FixedStart = FixedStart,
			FixedEnd = FixedEnd,
			Physics = Scene.PhysicsWorld,
		};
	}

	protected override void OnFixedUpdate()
	{
		Rope.StartPosition = RopeStart;
		Rope.EndPosition = RopeEnd;
		Rope.FixedStart = FixedStart;
		Rope.FixedEnd = FixedEnd || OscillateEnd;
		Rope.Simulate();
		if ( RopePhysics.DebugMode < 1 )
			return;

		var points = Rope.Points.ToArray();
		for ( int i = 0; i < points.Length; i++ )
		{
			var point = points[i];
			DebugOverlay.Sphere( new Sphere( point.Position, 1f ), color: Color.Red, overlay: false );
			if ( RopePhysics.DebugMode == 2 )
			{
				DebugOverlay.Sphere( new Sphere( point.Position, Rope.CollisionRadius ), color: Color.Green.WithAlpha( 0.2f ) );
			}
			if ( i == 0 )
				continue;

			var lastPoint = points[i - 1];
			DebugOverlay.Line( lastPoint.Position, point.Position, color: Color.Blue, overlay: false );
		}
	}

	protected override void OnUpdate()
	{
		if ( Rope is null )
			return;

		if ( ImGui.Begin( "Verlet Demo" ) )
		{
			// Debug info
			ImGui.Text( $"Points: {Rope.PointCount}" );
			ImGui.NewLine();

			ImGui.Text( "Debug Mode" );
			var debugMode1 = RopePhysics.DebugMode > 0;
			ImGui.PushID( nameof( debugMode1 ) );
			if ( ImGui.Checkbox( "Default", ref debugMode1 ) )
			{
				RopePhysics.DebugMode = RopePhysics.DebugMode switch
				{
					1 => 0,
					_ => 1,
				};
			}
			ImGui.PopID();
			ImGui.SameLine();
			var debugMode2 = RopePhysics.DebugMode > 1;
			ImGui.PushID( nameof( debugMode2 ) );
			if ( ImGui.Checkbox( "Collision Radius", ref debugMode2 ) )
			{
				RopePhysics.DebugMode = RopePhysics.DebugMode switch
				{
					2 => 1,
					_ => 2,
				};
			}
			ImGui.PopID();
			ImGui.NewLine();

			ImGui.Text( "Simulation Parameters" );
			var iterations = Iterations;
			ImGui.Text( "Iterations:" ); ImGui.SameLine();
			ImGui.PushID( 0 );
			ImGui.SliderInt( "Iterations", ref iterations, 0, 200 );
			ImGui.PopID();
			Iterations = iterations;
			ImGui.Text( "Collision Radius Scale:" );
			var collisionRadiusScale = Rope.CollisionRadiusScale;
			ImGui.PushID( "CollisionRadiusScale" );
			ImGui.SliderFloat( "CollisionRadiusScale", ref collisionRadiusScale, 0.1f, 50f );
			ImGui.PopID();
			Rope.CollisionRadiusScale = collisionRadiusScale;
			ImGui.NewLine();

			ImGui.Text( $"Rope Generation" );

			// Fixed Start
			var fixedStart = FixedStart;
			ImGui.PushID( "Fixed Start" );
			ImGui.Checkbox( "Fixed Start", ref fixedStart );
			ImGui.PopID();
			FixedStart = fixedStart;
			if ( FixedStart )
			{
				// Start Pos
				ImGui.Text( $"Start Pos:" ); ImGui.SameLine();
				var startOffset = new Vector2( -RopeStartOffset.y, RopeStartOffset.z );
				ImGui.PushID( 1 );
				ImGui.SliderFloat2( "Start Offset", ref startOffset, -200f, 200f );
				ImGui.PopID();
				RopeStartOffset = new Vector3( 0f, -startOffset.x, startOffset.y );
			}

			// Fixed End
			var fixedEnd = FixedEnd;
			ImGui.PushID( "FixedEnd" );
			if ( ImGui.Checkbox( "Fixed End", ref fixedEnd ) )
			{
				OscillateEnd = false;
			} 
			ImGui.SameLine();
			ImGui.PopID();
			FixedEnd = fixedEnd;
			var oscillateEnd = OscillateEnd;
			ImGui.PushID( "OscillateEnd" );
			if ( ImGui.Checkbox( "Oscillate End", ref oscillateEnd ) )
			{
				FixedEnd = false;
			}
			ImGui.PopID();
			OscillateEnd = oscillateEnd;
			if ( FixedEnd )
			{
				// End Pos
				ImGui.Text( $"End Pos:" ); ImGui.SameLine();
				var endOffset = new Vector2( -RopeEndOffset.y, RopeEndOffset.z );
				ImGui.PushID( 2 );
				ImGui.SliderFloat2( "End Offset", ref endOffset, -200f, 200f );
				ImGui.PopID();
				RopeEndOffset = new Vector3( 0f, -endOffset.x, endOffset.y );
			}
			if ( OscillateEnd )
			{
				var oscEndOffset = OscillateEndOffset;
				ImGui.Text( nameof( oscEndOffset ) + ":" ); ImGui.SameLine();
				ImGui.PushID( nameof(oscEndOffset) );
				ImGui.SliderFloat2( nameof( oscEndOffset ), ref oscEndOffset, -200, 200 );
				ImGui.PopID();
				OscillateEndOffset = oscEndOffset;
				var oscEndAmplitude = OscillateEndAmplitude;
				ImGui.Text( nameof( oscEndAmplitude ) + ":" ); ImGui.SameLine();
				ImGui.PushID( nameof( oscEndAmplitude ) );
				ImGui.SliderFloat2( nameof( oscEndAmplitude ), ref oscEndAmplitude, 0, 500 );
				ImGui.PopID();
				OscillateEndAmplitude = oscEndAmplitude;
				var oscEndPeriod = OscillateEndPeriod;
				ImGui.Text( nameof( oscEndPeriod ) + ":" ); ImGui.SameLine();
				ImGui.PushID( nameof( oscEndPeriod ) );
				ImGui.SliderFloat( nameof( oscEndPeriod ), ref oscEndPeriod, 0.01f, 5 );
				ImGui.PopID();
				OscillateEndPeriod = oscEndPeriod;
				var offset = new Vector3( 0, -OscillateEndOffset.x, OscillateEndOffset.y );
				var maxAmplitudes = new Vector3( 0f, -OscillateEndAmplitude.x, OscillateEndAmplitude.y );
				var currentAmplitude = MathF.Sin( MathF.Abs( Time.Now * MathF.PI / OscillateEndPeriod ) );
				RopeEndOffset = offset + maxAmplitudes * currentAmplitude;
			}

			// Point Count
			var numPoints = RopePointCount;
			ImGui.Text( "Point Count:" ); ImGui.SameLine();
			ImGui.PushID( 3 );
			ImGui.SliderInt( "Point Count", ref numPoints, 0, 200 );
			ImGui.PopID();
			RopePointCount = numPoints;

			// Reset Rope
			if ( ImGui.Button( $"Reset Rope" ) )
			{
				Rope.Reset();
			}
		}
		ImGui.End();
	}
}
