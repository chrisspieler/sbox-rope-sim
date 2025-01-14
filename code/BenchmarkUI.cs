using Duccsoft;
using Duccsoft.ImGui;
using Sandbox.Utility;

namespace Sandbox;

public class BenchmarkUI : Component
{
	[Property] public List<VerletComponent> Simulations { get; set; } = [];
	[Property] public GameObject RopeContainer { get; set; }

	private int GpuReadbackInterval = 5;
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
		ImGui.Text( $"CPU Sim Time: {VerletSystem.Current.AverageTotalSimulationCPUTime:F3}ms" );
		ImGui.Text( $"GPU Readback Time: {VerletSystem.Current.AverageTotalGpuReadbackTime:F3}ms" );
		ImGui.Text( $"GPU Readback Interval:" ); ImGui.SameLine();
		var swapInterval = GpuReadbackInterval;
		if ( ImGui.SliderInt( "GPU Readback Interval", ref swapInterval, 1,60 ) )
		{
			SetAllSwapInterval( swapInterval );
		}
		GpuReadbackInterval = swapInterval;
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
		
		ImGui.Text( "Iterations:" ); ImGui.SameLine();
		int iterations = Iterations;
		if ( ImGui.SliderInt( "Iterations", ref iterations, 1, 255 ) )
		{
			SetAllIterationCount( iterations );
		}
		Iterations = iterations;

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
			if ( Simulations.Count > 0 )
			{
				var last = Simulations[^1];
				var pos = last.WorldPosition + Vector3.Right * 4f;
				var rope = CreateRope( pos );
				rope.SimData.GpuReadbackInterval = GpuReadbackInterval;
			}
			else
			{
				CreateRope( RopeContainer.WorldPosition );
			}
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

	private void SetAllSwapInterval( int swapInterval )
	{
		foreach ( var verlet in Simulations )
		{
			verlet.SimData.GpuReadbackInterval = swapInterval;
		}
	}

	private void SetAllIterationCount( int iterations )
	{
		foreach( var verlet in Simulations )
		{
			verlet.Iterations = iterations;
		}
	}

	private void SetAllResolution( float resolution )
	{
		foreach( var verlet in Simulations )
		{
			if ( verlet is not VerletRope rope )
				continue;

			rope.PointSpacing = resolution;
		}
	}

	private VerletRope CreateRope( Vector3 position )
	{
		var ropeGo = new GameObject( true, "Verlet Rope" );
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
