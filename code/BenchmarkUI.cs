using Duccsoft;
using Duccsoft.ImGui;
using Sandbox.Utility;

namespace Sandbox;

public class BenchmarkUI : Component
{
	public List<VerletComponent> Simulations { get; set; } = [];
	[Property] public GameObject RopePivot { get; set; }
	[Property] public Oscillator Oscillator { get; set; }
	[Property] public GameObject RopeContainer { get; set; }
	[Property] public GameObject ColliderContainer { get; set; }
	public int SelectedColliderIndex
	{
		get
		{
			if ( !ColliderContainer.IsValid() || ColliderContainer.Children.Count < 1 )
				return 0;

			return _selectedColliderIndex.UnsignedMod( ColliderContainer.Children.Count );
		}
		set
		{
			_selectedColliderIndex = value.UnsignedMod( ColliderContainer?.Children?.Count ?? 1 );
			if ( !ColliderContainer.IsValid() )
				return;

			for ( int i = 0; i < ColliderContainer.Children.Count; i++ )
			{
				var child = ColliderContainer.Children[i];
				child.Enabled = _selectedColliderIndex == i;
				// Don't cause horrible lag
				if ( child.Enabled && child.Tags.Has( "mdf_model" ) )
				{
					Iterations = 1;
					UpdateAllRopeIterations();
				}
			}
		}
	}
	private int _selectedColliderIndex;
	public GameObject SelectedColliderGameObject
	{
		get
		{
			if ( !ColliderContainer.IsValid() || ColliderContainer.Children.Count < 1 )
				return null;

			return ColliderContainer.Children[SelectedColliderIndex];
		}
	}

	private int RopesPerRow = 50;
	private float RopeSpacing = 4f;

	private float RopeWidth = 1f;
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
		if ( ImGui.Button( " < " ) )
		{
			SelectedColliderIndex--;
		}
		ImGui.SameLine();
		ImGui.Text( $"{SelectedColliderGameObject?.Name ?? "None"}" ); ImGui.SameLine();
		if ( ImGui.Button( " > " ) )
		{
			SelectedColliderIndex++;
		}
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
		ImGui.Text( "Rope Width:" ); ImGui.SameLine();
		float ropeWidth = RopeWidth;
		if ( ImGui.SliderFloat( "Rope Width", ref ropeWidth, 0.25f, 4f ) )
		{
			RopeWidth = ropeWidth;
			foreach( var verlet in Simulations )
			{
				if ( verlet is not VerletRope rope )
					continue;

				rope.RadiusFraction = RopeWidth / PointSpacing;
			}
		}
		ImGui.Text( "Ropes Per Row:" ); ImGui.SameLine();
		int ropesPerRow = RopesPerRow;
		if ( ImGui.SliderInt( "Ropes Per Row", ref ropesPerRow, 1, 300 ) )
		{
			RopesPerRow = ropesPerRow;
			ResetRopePositions();
		}
		ImGui.Text( "Rope Spacing:" ); ImGui.SameLine();
		float ropeSpacing = RopeSpacing;
		if ( ImGui.SliderFloat( "Rope Spacing", ref ropeSpacing, 1f, 8f ) )
		{
			RopeSpacing = ropeSpacing;
			ResetRopePositions();
		}
		ImGui.Text( "Point Spacing:" ); ImGui.SameLine();
		float pointSpacing = PointSpacing;
		if ( ImGui.SliderFloat( "Point Spacing", ref pointSpacing, 0.5f, 25f ) )
		{
			PointSpacing = pointSpacing;
			foreach ( var verlet in Simulations )
			{
				if ( verlet is not VerletRope rope )
					continue;

				rope.RadiusFraction = RopeWidth / PointSpacing;
				rope.PointSpacing = PointSpacing;
			}
			ResetAllSims();
		}

		ImGui.Text( "Iterations:" ); ImGui.SameLine();
		int iterations = Iterations;
		if ( ImGui.SliderInt( "Iterations", ref iterations, 1, 20 ) )
		{
			Iterations = iterations;
			UpdateAllRopeIterations();
		}

		ImGui.NewLine();
		ImGui.Text( "Position:" ); ImGui.SameLine();
		Vector3 containerPos = RopePivot.WorldPosition * new Vector3( 1, -1, 1 );
		if ( ImGui.SliderFloat3( "Oscillator Position", ref containerPos, -100, 100 ) )
		{
			RopePivot.WorldPosition = containerPos * new Vector3( 1, -1, 1 );
		}
		ImGui.Text( "Osc. Amplitude:" ); ImGui.SameLine();
		Vector3 amplitude = Oscillator.Amplitude;
		if ( ImGui.SliderFloat3( "Oscillator Amplitude", ref amplitude, -20, 20 ) )
		{
			Oscillator.Amplitude = amplitude;
		}
		ImGui.Text( "Osc. Period:" ); ImGui.SameLine();
		Vector3 period = Oscillator.Period;
		if ( ImGui.SliderFloat3( "Oscillator Period", ref period, 0.01f, 5f ) )
		{
			Oscillator.Period = period;
		}

		bool infestationMode = VerletSystem.InfestationMode;
		if ( ImGui.Checkbox( "INFESTATION MODE", ref infestationMode ) )
		{
			VerletSystem.InfestationMode = infestationMode;
		}

		ImGui.NewLine();
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

	private Vector3 GetPositionForRope( int i )
	{
		var width = RopesPerRow * RopeSpacing - 1;
		var length = Simulations.Count / RopesPerRow * RopeSpacing;
		var height = RopeLength;
		var bounds = BBox.FromPositionAndSize( RopeContainer.WorldPosition, new Vector3( length, width, height ) );
		var startPos = bounds.Mins.WithZ( bounds.Size.z );
		var x = i / RopesPerRow * RopeSpacing;
		var y = i % RopesPerRow * RopeSpacing;
		return startPos + new Vector3( x, y, 0 );
	}

	private void ResetRopePositions()
	{
		for ( int i = 0; i < Simulations.Count; i++ )
		{
			var verlet = Simulations[i];
			verlet.WorldPosition = GetPositionForRope( i );
		}
		ResetAllSims();
	}

	private void UpdateAllRopeIterations()
	{
		foreach ( var verlet in Simulations )
		{
			verlet.Iterations = Iterations;
		}
	}

	private void AddRope( int count )
	{
		for ( int i = 0; i < count; i++ )
		{
			VerletRope rope;
			var ropePos = GetPositionForRope( i + Simulations.Count );
			rope = CreateRope( ropePos );
			rope.DefaultLength = RopeLength;
			rope.PointSpacing = PointSpacing;
			rope.Iterations = Iterations;
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
		var ropeGo = new GameObject( RopeContainer, true, "Verlet Rope" );
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
