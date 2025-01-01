namespace Duccsoft;

/// <summary>
/// Provides a signed distance field that was calculated using the triangles of a <see cref="PhysicsShape"/>.
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

	public int OctreeSize => Octree?.Size ?? -1;
	public int OctreeLeafDims => Octree?.LeafSize ?? -1;
	public float OctreeLeafSize => OctreeLeafDims * MeshDistanceSystem.VoxelSize;
	public bool IsInBounds( Vector3 localPos ) => Bounds.Contains( localPos );

	public Vector3Int PositionToVoxel( Vector3 localPos )
	{
		return Octree.PositionToVoxel( localPos );
	}

	public Vector3 VoxelToLocalCenter( Vector3Int voxel )
	{
		var pos = Octree.VoxelToPosition( voxel );
		return pos += Octree.LeafSize;
	}

	public BBox VoxelToLocalBounds( Vector3Int voxel ) => Octree.GetVoxelBounds( voxel );

	#region Queries

	public SparseVoxelOctree<VoxelSdfData>.OctreeNode Trace( Vector3 localPos, Vector3 localDir, out float hitDistance )
		=> Trace( localPos, localDir, out hitDistance, new Vector3Int( -1, -1, -1 ) );

	public SparseVoxelOctree<VoxelSdfData>.OctreeNode Trace( Vector3 localPos, Vector3 localDir, out float hitDistance, Vector3Int filter )
	{
		hitDistance = -1f;
		return Octree?.Trace( localPos, localDir, out hitDistance, filter );
	}

	public VoxelSdfData GetSdfTexture( Vector3Int voxel )
	{
		
		return Octree?.Find( voxel )?.Data;
	}

	public bool TrySample( Vector3 localSamplePos, out MeshDistanceSample sample )
	{
		// Snap sample point to bounds, in case it's out of bounds.
		var closestPoint = Bounds.ClosestPoint( localSamplePos );
		if ( Octree is null )
		{
			sample = default;
			return false;
		}
		var octreeVoxel = Octree.Find( closestPoint );
		if ( octreeVoxel?.Data is null )
		{
			// TODO: Trace ray to nearest leaf and sample its SDF instead.
			sample = default;
			return false;
		}
		var sdf = octreeVoxel.Data;
		var sdfVoxel = sdf.PositionToVoxel( closestPoint );
		var signedDistance = sdf[sdfVoxel];
		// If we were out of bounds, add the amount by which we were out of bounds to the distance.
		signedDistance += closestPoint.Distance( localSamplePos );
		var surfaceNormal = sdf.EstimateVoxelSurfaceNormal( sdfVoxel );
		sample = new MeshDistanceSample()
		{
			SurfaceNormal = surfaceNormal,
			SignedDistance = signedDistance,
		};
		return true;
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

	public void DebugDraw( Transform tx, Vector3Int highlightedPosition, Vector3 selectedPosition, int currentSlice )
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
			var ignoreDepth = false;
			if ( node.IsLeaf )
			{
				var isActiveSlice = currentSlice < 0 || node.Position.z == currentSlice;
				if ( node.Position == selectedPosition )
				{
					color = Color.Magenta.WithAlpha( 1f );
					ignoreDepth = true;
				}
				else if ( node.Position == highlightedPosition )
				{
					color = Color.Green.WithAlpha( 1f );
					ignoreDepth = true;
				}
				else if ( node.Data is null )
				{
					if ( isActiveSlice )
					{
						color = Color.Red.WithAlpha( 0.5f );
						ignoreDepth = true;
					}
					else
					{
						color = Color.Red.WithAlpha( 0.01f );
					}
				}
				else
				{
					if ( isActiveSlice )
					{
						color = Color.Yellow.WithAlpha( 0.35f );
						ignoreDepth = true;
					}
					else
					{
						color = Color.Yellow.WithAlpha( 0.01f );
					}
				}
			}
			overlay.Box( bbox, color, transform: tx, overlay: ignoreDepth );
			foreach ( var child in node.Children )
			{
				if ( child is null )
					continue;

				DrawChildren( child );
			}
		}
	}

}
