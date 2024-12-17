using Duccsoft;
using Duccsoft.ImGui;

namespace Sandbox;

public class VerletDemo : Component
{
	[Property, Range(0, 20)]
	public int Iterations 
	{
		get => _iterations;
		set
		{
			_iterations = value;
			if ( VerletWorld is not null )
			{
				VerletWorld.Iterations = value;
			}
		}
	}
	private int _iterations = 3;
	[Property]
	public Vector3 RopeStartOffset { get; set; } = Vector3.Zero;
	[Property]
	public Vector3 RopeEndOffset { get; set; } = Vector3.Right * 50f;
	[Property, Range( 2, 50 )]
	public int RopePointCount { get; set; } = 10;
	public Verlet VerletWorld { get; set; }

	protected override void OnStart()
	{
		VerletWorld = new()
		{
			Iterations = Iterations
		};
		GenerateRope();
	}

	protected override void OnFixedUpdate()
	{
		VerletWorld.Tick();
		foreach( var point in VerletWorld.GetPoints() )
		{
			DebugOverlay.Sphere( new Sphere( point.Position, 1f ), color: Color.Red, overlay: true );
		}
		foreach( var segment in VerletWorld.GetSegments() )
		{
			DebugOverlay.Line( segment.PointA.Position, segment.PointB.Position, color: Color.Blue, overlay: true );
		}
	}

	private void GenerateRope()
	{
		var startPos = WorldPosition + RopeStartOffset;
		var endPos = WorldPosition + RopeEndOffset;
		var numPoints = RopePointCount;

		VerletWorld.Clear();
		var ray = new Ray( startPos, (endPos - startPos).Normal );
		var distance = startPos.Distance( endPos );
		var segmentLength = numPoints > 1
			? distance / numPoints - 1
			: 0f;
		Verlet.Point lastPoint = null;
		for ( int i = 0; i < numPoints; i++ )
		{
			var pos = ray.Project( segmentLength * i );
			if ( i == 0 )
			{
				lastPoint = VerletWorld.AddPoint( pos, true );
				continue;
			}

			var point = VerletWorld.AddPoint( pos, false );
			VerletWorld.AddSegment( lastPoint, point, segmentLength );
			lastPoint = point;
		}
	}

	protected override void OnUpdate()
	{
		if ( ImGui.Begin( "Verlet Demo" ) )
		{
			// Debug info
			ImGui.Text( $"Points: {VerletWorld.PointCount}, Links: {VerletWorld.SegmentCount}" );
			ImGui.NewLine();

			ImGui.Text( "Simulation Parameters" );
			var iterations = Iterations;
			ImGui.Text( "Iterations:" ); ImGui.SameLine();
			ImGui.PushID( 0 );
			ImGui.SliderInt( "Iterations", ref iterations, 0, 50 );
			ImGui.PopID();
			Iterations = iterations;
			ImGui.NewLine();

			ImGui.Text( $"Rope Generation" );

			// Start Pos
			ImGui.Text( $"Start Pos:" ); ImGui.SameLine();
			var startOffset = new Vector2( -RopeStartOffset.y, RopeStartOffset.z );
			ImGui.PushID( 1 );
			ImGui.SliderFloat2( "Start Offset", ref startOffset, -300f, 300f );
			ImGui.PopID();
			RopeStartOffset = new Vector3( 0f, -startOffset.x, startOffset.y );

			// End Pos
			ImGui.Text( $"End Pos:" ); ImGui.SameLine();
			var endOffset = new Vector2( -RopeEndOffset.y, RopeEndOffset.z );
			ImGui.PushID( 2 );
			ImGui.SliderFloat2( "End Offset", ref endOffset, -300f, 300f );
			ImGui.PopID();
			RopeEndOffset = new Vector3( 0f, -endOffset.x, endOffset.y );

			// Point Count
			var numPoints = RopePointCount;
			ImGui.Text( "Point Count:" ); ImGui.SameLine();
			ImGui.PushID( 3 );
			ImGui.SliderInt( "Point Count", ref numPoints, 0, 200 );
			ImGui.PopID();
			RopePointCount = numPoints;

			// Generate Rope
			if ( ImGui.Button( $"Generate Rope" ) )
			{
				GenerateRope();
			}
		}
		ImGui.End();
	}
}
