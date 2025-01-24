using Duccsoft.ImGui;
using Duccsoft;

namespace Sandbox;

public static class DebugPanels
{
	public static void StatsWindow()
	{
		ImGui.SetNextWindowPos( Screen.Size * new Vector2( 0.8f, 0.05f ) );
		if ( ImGui.Begin( "Performance Stats" ) )
		{
			ImGui.Text( $"VRAM: {VerletSystem.Current.TotalGpuDataSize.FormatBytes()} FPS: {PerformanceSystem.Current.CurrentFramerate:F1}" );
			ImGui.Text( $"CPU Physics Trace: {VerletSystem.Current.AverageTotalCaptureSnapshotTime:F3}ms" );
			ImGui.Text( $"GPU Simulation: {VerletSystem.Current.AverageTotalGpuSimulationTime:F3}ms" );
			ImGui.Text( $"GPU Store Points: {VerletSystem.Current.AverageTotalGpuStorePointsTime:F3}ms" );
			ImGui.Text( $"GPU Build Mesh: {VerletSystem.Current.AverageTotalGpuBuildMeshTimes:F3}ms" );
			ImGui.Text( $"GPU Readback Time: {VerletSystem.Current.AverageTotalGpuReadbackTime:F3}ms" );
		}
		ImGui.End();
	}
}
