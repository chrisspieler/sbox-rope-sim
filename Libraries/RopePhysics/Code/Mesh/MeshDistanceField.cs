namespace Duccsoft;

/// <summary>
/// Provides a signed distance field that was calculated using the triangles of a <see cref="PhysicsShape"/>.
/// 
/// </summary>
public class MeshDistanceField
{
	public struct MeshDistanceSample
	{
		public float SignedDistance { get; set; }
		public Vector3 SurfaceNormal { get; set; }
	}

	private MeshDistanceField() { }
	internal MeshDistanceField( MeshDistanceBuildSystem buildSystem, int id )
	{
		BuildSystem = buildSystem;
		Id = id;
	}

	public int Id { get; }
	public VoxelSdfData VoxelSdf { get; internal set; }
	public int DataSize { get; private set; }
	// TODO: Read this from the octree itself?
	public int OctreeLeafCount { get; private set; }
	
	public BBox Bounds => MeshData?.Bounds ?? BBox.FromPositionAndSize( Vector3.Zero, 16f );

	public int VertexCount => MeshData?.Vertices?.ElementCount ?? -1;
	public int IndexCount => MeshData?.Indices?.ElementCount ?? -1;
	public int TriangleCount => IndexCount / 3;
	internal GpuMeshData MeshData { get; set; }
	internal void SetOctree( SparseVoxelOctree<VoxelSdfData> octree ) => Octree = octree;
	private SparseVoxelOctree<VoxelSdfData> Octree { get; set; }
	private PhysicsShape MeshSource { get; set; }

	#region Build Jobs
	public int QueuedJumpFloodJobs => JumpFloodJobs.Count;
	public TimeSince SinceBuildStarted { get; set; }
	public TimeSince SinceBuildFinished { get; set; }

	private MeshDistanceBuildSystem BuildSystem { get; set; }
	internal ExtractMeshFromPhysicsJob ExtractMeshJob { get; set; }
	internal ConvertMeshToGpuJob ConvertMeshJob { get; set; }
	internal CreateMeshOctreeJob CreateOctreeJob { get; set; }
	internal Dictionary<Vector3Int, JumpFloodSdfJob> JumpFloodJobs { get; set; } = new();

	public void RebuildAll()
	{
		Octree = null;
		DataSize = 0;
		OctreeLeafCount = 0;

		SinceBuildStarted = 0;
		BuildSystem.AddCreateMeshOctreeJob( this );
	}

	internal void RebuildOctreeVoxel( Vector3Int voxel )
	{
		var job = BuildSystem.AddJumpFloodSdfJob( this, voxel );
		JumpFloodJobs[voxel] = job;
	}

	internal void SetOctreeVoxel( Vector3Int voxel, VoxelSdfData data )
	{
		JumpFloodJobs.Remove( voxel );

		var node = Octree.Find( voxel );
		if ( node.IsLeaf && node.Data is not null )
		{
			DataSize -= node.Data.DataSize;
			OctreeLeafCount--;
		}
		Octree.Insert( voxel, data );
		DataSize += data.DataSize;
		OctreeLeafCount++;

		if ( JumpFloodJobs.Count == 0 )
		{
			SinceBuildFinished = 0;
		}
	}

	public bool IsBuilding
	{
		get
		{
			if ( ExtractMeshJob.IsActive() || ConvertMeshJob.IsActive() || CreateOctreeJob.IsActive() )
				return true;

			return JumpFloodJobs.Count > 0;
		}
	}
	#endregion

	public bool IsInBounds( Vector3 localPos ) => Bounds.Contains( localPos );

	public Vector3Int PositionToVoxel( Vector3 localPos )
	{
		if ( VoxelSdf is null )
			return default;

		var normalized = ( localPos - Bounds.Mins ) / Bounds.Size;
		var voxel = normalized * ( VoxelSdf.VoxelGridDims - 1 );
		return (Vector3Int)voxel;
	}

	public Vector3 VoxelToPositionCenter( Vector3Int voxel )
	{
		if ( VoxelSdf is null )
			return default;

		var normalized = (Vector3)voxel / VoxelSdf.VoxelGridDims;
		var positionCorner = Bounds.Mins + normalized * Bounds.Size;
		return positionCorner + MeshDistanceSystem.VoxelSize * 0.5f;
	}

	#region Queries

	public MeshDistanceSample Sample( Vector3 localSamplePos )
	{
		if ( VoxelSdf is null )
			return default;

		// Snap sample point to bounds, in case it's out of bounds.
		var closestPoint = Bounds.ClosestPoint( localSamplePos );
		var voxel = PositionToVoxel( closestPoint );
		var signedDistance = VoxelSdf[voxel];
		// If we were out of bounds, add the amount by which we were out of bounds to the distance.
		signedDistance += closestPoint.Distance( localSamplePos );
		var surfaceNormal = VoxelSdf.EstimateVoxelSurfaceNormal( voxel );
		return new MeshDistanceSample()
		{
			SurfaceNormal = surfaceNormal,
			SignedDistance = signedDistance,
		};
	}
	public struct MdfQueryResult
	{
		public bool Hit;
		public GameObject GameObject;
		public PhysicsShape Shape;
		public RopeColliderType ColliderType;
		public MeshDistanceField Mdf;
		public Vector3 LocalPosition;
	}

	public static MdfQueryResult FindInSphere( Vector3 worldPos, float radius )
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() || !Game.IsPlaying )
			return default;

		var tr = scene.PhysicsWorld
			.Trace
			.Sphere( radius, worldPos, worldPos )
			.WithoutTags( "noblockrope" )
			.RunAll();
		if ( !tr.Any( tr => tr.Hit ) )
			return default;

		// Possibly not the nearest due to how sphere traces ignore the point of collision.
		var nearest = tr.OrderBy( tr => tr.Distance ).First();
		var gameObject = (nearest.Shape?.Collider as Component)?.GameObject;
		var localPos = nearest.Shape.Body.Transform.PointToLocal( worldPos );

		return new MdfQueryResult
		{
			Hit = true,
			GameObject = gameObject,
			Shape = nearest.Shape,
			ColliderType = nearest.ClassifyColliderType(),
			Mdf = MeshDistanceSystem.Current.GetMdf( nearest.Shape ),
			LocalPosition = localPos,
		};
	}

	#endregion

	public void DebugDraw( Transform tx )
	{
		var overlay = DebugOverlaySystem.Current;

		if ( Octree?.RootNode is null || overlay is null )
			return;

		DrawChildren( Octree.RootNode );

		void DrawChildren( SparseVoxelOctree<VoxelSdfData>.OctreeNode node )
		{
			var pos = node.Position - Octree.Size / 2;
			var bbox = new BBox( pos, pos + node.Size );
			var color = Color.Blue.WithAlpha( 0.15f );
			if ( node.IsLeaf )
			{
				if ( node.Data is null )
				{
					color = Color.Red.WithAlpha( 0.5f );
				}
				else
				{
					color = Color.Yellow.WithAlpha( 0.35f );
				}
			}
			overlay.Box( bbox, color, transform: tx );
			foreach ( var child in node.Children )
			{
				if ( child is null )
					continue;

				DrawChildren( child );
			}
		}
	}

}
