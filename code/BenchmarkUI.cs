using Duccsoft;
using Duccsoft.ImGui;
using Sandbox.Utility;

namespace Sandbox;

public class BenchmarkUI : Component
{
	[Property] public List<VerletComponent> Simulations { get; set; } = [];
	[Property] public GameObject RopeContainer { get; set; }

	private float RopeSpacing = 4f;

	private float RopeLength = 96f;
	private float PointSpacing = 4f;
	private int Iterations = 20;
	
	private readonly CircularBuffer<float> DeltaTimes = new( 60 );

	protected override void OnEnabled()
	{
		RopeContainer ??= GameObject;
	}

	protected override void OnUpdate()
	{
		DeltaTimes.PushBack( Time.Delta );
		ImGui.SetNextWindowPos( Screen.Size * 0.25f * ImGuiStyle.UIScale );
		if ( ImGui.Begin( "Performance Benchmark") )
		{
			PaintWindow();
		}
		ImGui.End();
	}

	private void PaintWindow()
	{
		ImGui.Text( $"FPS: {1 / DeltaTimes.Average():F1}" );
		ImGui.Text( $"CPU Capture Snapshots: {VerletSystem.Current.AverageTotalCaptureSnapshotTime:F3}ms" );
		ImGui.Text( $"GPU Simulation: {VerletSystem.Current.AverageTotalGpuSimulationTime:F3}ms" );
		ImGui.Text( $"GPU Store Points: {VerletSystem.Current.AverageTotalGpuStorePointsTime:F3}ms" );
		ImGui.Text( $"GPU Build Mesh: {VerletSystem.Current.AverageTotalGpuBuildMeshTimes:F3}ms" );
		ImGui.Text( $"GPU Readback Time: {VerletSystem.Current.AverageTotalGpuReadbackTime:F3}ms" );
		ImGui.Text( $"Rope Count: {Simulations.Count}" ); ImGui.SameLine();
		if ( ImGui.Button( " - ") )
		{
			RemoveRope( 1 );
		}
		ImGui.SameLine();
		if ( ImGui.Button( " + (1)" ) )
		{
			AddRope( 1 );
		}
		ImGui.SameLine();
		if ( ImGui.Button( " + (5)" ) )
		{
			AddRope( 5 );
		}
		ImGui.SameLine();
		if ( ImGui.Button( " + (25)" ) )
		{
			AddRope( 25 );
		}
		ImGui.Text( $"Points per rope: {Simulations.FirstOrDefault()?.PointCount ?? 0}" );

		ImGui.Text( "Length:" ); ImGui.SameLine();
		float ropeLength = RopeLength;
		if ( ImGui.SliderFloat( "Rope Length", ref ropeLength, 10f, 500f ) )
		{
			RopeLength = ropeLength;
			foreach( var verlet in Simulations )
			{
				verlet.DefaultLength = RopeLength;
			}
			ResetAllSims();
		}

		ImGui.Text( "Point Spacing:" ); ImGui.SameLine();
		float ropeSpacing = PointSpacing;
		if ( ImGui.SliderFloat( "Point Spacing", ref ropeSpacing, 0.5f, 25f ) )
		{
			PointSpacing = ropeSpacing;
			foreach ( var verlet in Simulations )
			{
				if ( verlet is not VerletRope rope )
					continue;

				rope.RadiusFraction = 1f / PointSpacing;
				rope.PointSpacing = PointSpacing;
			}
			ResetAllSims();
		}

		ImGui.Text( "Iterations:" ); ImGui.SameLine();
		int iterations = Iterations;
		if ( ImGui.SliderInt( "Iterations", ref iterations, 1, 255 ) )
		{
			foreach( var verlet in Simulations )
			{
				verlet.Iterations = iterations;
			}
			Iterations = iterations;
		}

		ImGui.Text( "For each:" );
		ImGui.SameLine();
		if ( ImGui.Button( "Reset" ) )
		{
			ResetAllSims();
		}
		ImGui.SameLine();
		if ( ImGui.Button( "Delete" ) )
		{
			DeleteAllSims();
		}
	}

	private void AddRope( int count )
	{
		for ( int i = 0; i < count; i++ )
		{
			VerletRope rope;
			if ( Simulations.Count > 0 )
			{
				var x = Simulations.Count % 10 * RopeSpacing;
				var y = Simulations.Count / 10 * RopeSpacing;
				var pos = RopeContainer.WorldPosition + new Vector3( x, y, 0 );
				rope = CreateRope( pos );
			}
			else
			{
				rope = CreateRope( RopeContainer.WorldPosition );
			}
			rope.DefaultLength = RopeLength;
			rope.PointSpacing = PointSpacing;
		}
	}

	private void RemoveRope( int count )
	{
		for ( int i = 0; i < count; i++ )
		{
			if ( Simulations.Count > 0 )
			{
				var verlet = Simulations[^1];
				verlet.DestroyGameObject();
				Simulations.RemoveAt( Simulations.Count - 1 );
			}
			else
			{
				return;
			}
		}
	}

	private VerletRope CreateRope( Vector3 position )
	{
		var ropeGo = new GameObject( true, "Verlet Rope" );
		ropeGo.Parent = RopeContainer;
		ropeGo.WorldPosition = position;
		var verlet = ropeGo.AddComponent<VerletRope>();
		Simulations.Add( verlet );
		return verlet;
	}

	private void DeleteAllSims()
	{
		foreach ( var verlet in Simulations )
		{
			verlet.DestroyGameObject();
		}
		Simulations.Clear();
	}

	private void ResetAllSims()
	{
		foreach( var verlet in Simulations )
		{
			verlet.ResetSimulation();
		}
	}
}
