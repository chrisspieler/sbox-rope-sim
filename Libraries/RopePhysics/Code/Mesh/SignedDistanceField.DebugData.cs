using System.Text;

namespace Duccsoft;

public partial class SignedDistanceField
{
	public class DebugData
	{
		public DebugData( MeshDistanceField mdf, Vector3Int voxel, SignedDistanceField sdf )
		{
			Sdf = sdf;
			OctreeVoxel = voxel;
			Mdf = mdf;
		}

		public MeshDistanceField Mdf;
		public SignedDistanceField Sdf;
		public Vector3Int OctreeVoxel;
		public MeshSeedData[] SeedData;
		public int EmptySeedCount;
		public int[] SeedVoxels;
		public int[] VoxelSeedIds;
		public Vector4[] Gradients;

		public int GetSeedId( Vector3Int texel )
		{
			var i = Index3DTo1D( texel.x, texel.y, texel.z, Sdf.TextureSize );
			return VoxelSeedIds[i];
		}

		public Vector3 GetSeedPosition( Vector3Int texel )
		{
			var seedData = SeedData[GetSeedId( texel )];
			return seedData.Position;
		}

		public Triangle? GetSeedTriangle( int seedId )
		{
			var emptySeedStartId = Mdf.MeshData.TriangleCount * 4;
			if ( seedId < 0 || seedId >= emptySeedStartId )
				return null;

			int centerId = seedId - seedId % 4;
			var v0 = SeedData[centerId + 1].Position;
			var v1 = SeedData[centerId + 2].Position;
			var v2 = SeedData[centerId + 3].Position;
			return new Triangle( v0, v1, v2 );
		}

		public void PrecalculateGradients()
		{
			var voxelCount = Sdf.TextureSize * Sdf.TextureSize * Sdf.TextureSize;
			Gradients = new Vector4[voxelCount];
			for ( int i = 0; i < voxelCount; i++ )
			{
				var i3D = Index1DTo3D( i, Sdf.TextureSize );
				Gradients[i] = new Vector4( Sdf.EstimateSurfaceNormal( i3D ) );
			}
		}

		const string DIR_DUMP = "mdfData";
		const string PATH_DUMP_CPU_MESH = $"{DIR_DUMP}/dump_cpuMesh.txt";
		const string PATH_DUMP_SEED_DATA = $"{DIR_DUMP}/dump_seedData.txt";
		const string PATH_DUMP_SEED_VOXELS = $"{DIR_DUMP}/dump_seedVoxels.txt";
		const string PATH_DUMP_VOXEL_SEED_IDS = $"{DIR_DUMP}/dump_voxelSeedIds.txt";

		public void DumpAllData()
		{
			DumpCpuMesh();
			DumpSeedData();
			DumpSeedVoxels();
			DumpVoxelSeedIds();
			Log.Info( $"Dumped voxel data! Logs at: \"{ FileSystem.OrganizationData.GetFullPath( DIR_DUMP ) }\"" );
		}

		private void DumpCpuMesh()
		{
			var dumpTimer = new MultiTimer();
			using ( dumpTimer.RecordTime() )
			{
				var indices = Mdf.MeshData.CpuMesh.Indices;
				var vertices = Mdf.MeshData.CpuMesh.Vertices;
				var sb = new StringBuilder();
				sb.AppendLine( $"Dumping {indices.Length} indices, {vertices.Length} vertices, {indices.Length / 3.0f} triangles" );
				sb.AppendLine( $"Mesh bounds: {Mdf.MeshData.CpuMesh.Bounds}" );
				for ( int i = 0; i < indices.Length; i += 3 )
				{
					var i0 = indices[i];
					var i1 = indices[i + 1];
					var i2 = indices[i + 2];
					var v0 = vertices[i0];
					var v1 = vertices[i1];
					var v2 = vertices[i2];
					sb.AppendLine( $"tri # {i / 3} (idx {i0},{i1},{i2}) vtx: ({v0}),({v1}),({v2})" );
				}
				var fs = FileSystem.OrganizationData;
				fs.CreateDirectory( DIR_DUMP );
				fs.WriteAllText( PATH_DUMP_CPU_MESH, sb.ToString() );
			}
			Log.Info( $"DumpMesh in {dumpTimer.LastMilliseconds:F3}ms" );
		}

		private void DumpSeedData()
		{
			var seedDataDumpTimer = new MultiTimer();
			using ( seedDataDumpTimer.RecordTime() )
			{
				var sb = new StringBuilder();
				var triCount = Mdf.MeshData.CpuMesh.TriangleCount;
				var emptySeedStartIdx = triCount * 4;
				sb.AppendLine( $"SeedData dump for {OctreeVoxel}[{OctreeVoxel / Mdf.OctreeLeafSize}] of MDF # {Mdf.Id}" );
				sb.AppendLine( $"{triCount} tris, {EmptySeedCount} empty seeds, data: {SeedData.Length}, expected data: {triCount * 4 + EmptySeedCount}" );
				for ( int i = 0; i < SeedData.Length; i++ )
				{
					if ( i == emptySeedStartIdx )
					{
						sb.AppendLine( "===" );
						sb.AppendLine( $"EMPTY SEED DATA BEGINS NOW!" );
						sb.AppendLine( "===" );
					}
					var seedDatum = SeedData[i];
					Vector4 positionOs = seedDatum.Position;
					Vector4 normal = seedDatum.Normal;
					var line = $"seed # {i} pOs: {positionOs}, nor: {normal}";
					sb.AppendLine( line );
				}
				var fs = FileSystem.OrganizationData;
				fs.CreateDirectory( DIR_DUMP );
				fs.WriteAllText( PATH_DUMP_SEED_DATA, sb.ToString() );
			}
			Log.Info( $"Seed data dump in {seedDataDumpTimer.LastMilliseconds:F3}ms" );
		}

		private void DumpSeedVoxels()
		{
			var seedDataDumpTimer = new MultiTimer();
			using ( seedDataDumpTimer.RecordTime() )
			{
				var sb = new StringBuilder();
				sb.AppendLine( $"Seed voxel dump for {OctreeVoxel}[{OctreeVoxel / Mdf.OctreeLeafDims}] of MDF # {Mdf.Id}" );
				for ( int i = 0; i < SeedVoxels.Length; i++ )
				{
					var i1D = SeedVoxels[i];
					var i3D = Index1DTo3D( i1D, Mdf.OctreeLeafDims );
					var line = $"seed # {i} i1D: {i1D}, i3D: ({i3D.x},{i3D.y},{i3D.z})";
					sb.AppendLine( line );
				}
				var fs = FileSystem.OrganizationData;
				fs.CreateDirectory( DIR_DUMP );
				fs.WriteAllText( PATH_DUMP_SEED_VOXELS, sb.ToString() );
			}
			Log.Info( $"Seed voxel dump in {seedDataDumpTimer.LastMilliseconds:F3}ms" );
		}

		private void DumpVoxelSeedIds()
		{
			var seedDataDumpTimer = new MultiTimer();
			using ( seedDataDumpTimer.RecordTime() )
			{
				var sb = new StringBuilder();
				sb.AppendLine( $"Voxel seed ID dump for {OctreeVoxel}[{OctreeVoxel / Mdf.OctreeLeafDims}] of MDF # {Mdf.Id}" );
				for ( int i = 0; i < VoxelSeedIds.Length; i++ )
				{
					var seedId = VoxelSeedIds[i];
					var i3D = Index1DTo3D( i, Mdf.OctreeLeafDims );
					var line = $"({i3D.x},{i3D.y},{i3D.z}): {seedId}";
					sb.AppendLine( line );
				}
				var fs = FileSystem.OrganizationData;
				fs.CreateDirectory( DIR_DUMP );
				fs.WriteAllText( PATH_DUMP_VOXEL_SEED_IDS, sb.ToString() );
			}
			Log.Info( $"Voxel seed ID dump in {seedDataDumpTimer.LastMilliseconds:F3}ms" );
		}
	}

}
