using Duccsoft;
using Duccsoft.ImGui;

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
	private int _ropePointCount = 10;
	[Property] public bool FixedStart { get; set; } = true;
	[Property] public bool FixedEnd { get; set; } = false;
	public VerletRope Rope { get; set; }

	protected override void OnStart()
	{
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
		Rope.FixedEnd = FixedEnd;
		Rope.Simulate();
		var points = Rope.Points.ToArray();
		for ( int i = 0; i < points.Length; i++ )
		{
			var point = points[i];
			DebugOverlay.Sphere( new Sphere( point.Position, 1f ), color: Color.Red, overlay: false );
			if ( i == 0 )
				continue;

			var lastPoint = points[i - 1];
			DebugOverlay.Line( lastPoint.Position, point.Position, color: Color.Blue, overlay: false );
		}
	}

	protected override void OnUpdate()
	{
		if ( ImGui.Begin( "Verlet Demo" ) )
		{
			// Debug info
			ImGui.Text( $"Points: {Rope.PointCount}" );
			ImGui.NewLine();

			ImGui.Text( "Simulation Parameters" );
			var iterations = Iterations;
			ImGui.Text( "Iterations:" ); ImGui.SameLine();
			ImGui.PushID( 0 );
			ImGui.SliderInt( "Iterations", ref iterations, 0, 200 );
			ImGui.PopID();
			Iterations = iterations;
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
				ImGui.SliderFloat2( "Start Offset", ref startOffset, -300f, 300f );
				ImGui.PopID();
				RopeStartOffset = new Vector3( 0f, -startOffset.x, startOffset.y );
			}

			// Fixed End
			var fixedEnd = FixedEnd;
			ImGui.PushID( "Fixed End" );
			ImGui.Checkbox( "Fixed End", ref fixedEnd );
			ImGui.PopID();
			FixedEnd = fixedEnd;
			if ( FixedEnd )
			{
				// End Pos
				ImGui.Text( $"End Pos:" ); ImGui.SameLine();
				var endOffset = new Vector2( -RopeEndOffset.y, RopeEndOffset.z );
				ImGui.PushID( 2 );
				ImGui.SliderFloat2( "End Offset", ref endOffset, -300f, 300f );
				ImGui.PopID();
				RopeEndOffset = new Vector3( 0f, -endOffset.x, endOffset.y );
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
