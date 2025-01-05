using Sandbox.Diagnostics;

namespace Duccsoft;

/// <summary>
/// Provides a signed distance field that was calculated using the triangles of a <see cref="PhysicsShape"/>.
/// </summary>
public partial class MeshDistanceField
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
	internal void SetOctree( SparseVoxelOctree<SignedDistanceField> octree ) => Octree = octree;
	private SparseVoxelOctree<SignedDistanceField> Octree { get; set; }

	#region Build Jobs
	public int QueuedJumpFloodJobs => JumpFloodJobs.Count;
	public TimeSince SinceBuildStarted { get; set; }
	public TimeSince SinceBuildFinished { get; set; }

	private MeshDistanceBuildSystem BuildSystem { get; set; }

	public static int GetId( PhysicsShape shape )
	{
		return GetId( shape.Collider as Collider );
	}

	public static int GetId( Collider collider )
	{
		return collider.Id.GetHashCode();
	}

	public static int GetId( Model model )
	{
		return model.IsProcedural
			? model.GetHashCode()
			: model.ResourceId;
	}

	internal Job ExtractMeshJob { get; set; }
	internal ConvertMeshToGpuJob ConvertMeshJob { get; set; }
	public bool IsMeshBuilt => MeshData is not null;
	internal CreateMeshOctreeJob CreateOctreeJob { get; set; }
	public bool IsOctreeBuilt => Octree is not null;
	internal Dictionary<Vector3Int, JumpFloodSdfJob> JumpFloodJobs { get; set; } = new();

	public void RebuildFromMesh()
	{
		Octree = null;
		DataSize = 0;
		OctreeLeafCount = 0;

		SinceBuildStarted = 0;
		BuildSystem.AddCreateMeshOctreeJob( this );
	}

	public void RebuildOctreeVoxel( Vector3Int voxel, bool dumpDebugData, Action onCompleted = null )
	{
		if ( GetSdfTexture( voxel ) is SignedDistanceField existingData )
		{
			existingData.IsRebuilding = true;
		}
		var job = BuildSystem.AddJumpFloodSdfJob( this, voxel, dumpDebugData );
		job.OnCompleted += onCompleted;
		JumpFloodJobs[voxel] = job;
	}

	internal void SetOctreeVoxel( Vector3Int voxel, SignedDistanceField data )
	{
		var jobCount = JumpFloodJobs.Count;
		var jobRemoved = JumpFloodJobs.Remove( voxel );
		if ( IsBuilding && jobCount == 1 && jobRemoved )
		{
			SinceBuildFinished = 0;
		}

		var node = Octree.Find( voxel );
		if ( node?.IsLeaf == true && node.Data is not null )
		{
			Assert.AreEqual( data.DataSize, node.Data.DataSize );
			Assert.AreEqual( data.TextureSize, node.Data.TextureSize );
			node.Data.Data = data.Data;
			node.Data.IsRebuilding = false;
			node.Data.Debug = data.Debug;
		}
		else
		{
			Octree.Insert( voxel, data );
			DataSize += data.DataSize;
			OctreeLeafCount++;
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
	public float OctreeLeafSize => OctreeLeafDims * MeshDistanceSystem.TexelSize;
	public bool IsInBounds( Vector3 localPos ) => Bounds.Contains( localPos );
	public bool IsInBounds( Vector3Int voxel )
	{
		if ( !IsOctreeBuilt )
			return false;

		var localPos = Octree.VoxelToPosition( voxel );
		return Octree.ContainsPoint( localPos );
	}

	private BBox OctreeBoundsToLocal( BBox bounds ) => bounds.Translate( MeshData?.Bounds.Center ?? 0 );
	private BBox LocalBoundsToOctree( BBox bounds ) => bounds.Translate( -MeshData?.Bounds.Center ?? 0 );
	private Vector3 OctreePosToLocal( Vector3 octreePos ) => octreePos + MeshData?.Bounds.Center ?? 0;
	private Vector3 LocalPosToOctree( Vector3 pos ) => pos - MeshData?.Bounds.Center ?? 0;

	public Vector3Int PositionToVoxel( Vector3 localPos )
	{
		localPos = LocalPosToOctree( localPos );
		return Octree.PositionToVoxel( localPos );
	}

	public Vector3 VoxelToLocalCenter( Vector3Int voxel )
	{
		var pos = Octree.VoxelToPosition( voxel );
		pos += Octree.LeafSize;
		return OctreePosToLocal( pos );
	}

	public BBox VoxelToLocalBounds( Vector3Int voxel )
	{
		var bounds = Octree.GetLeafBounds( voxel );
		return OctreeBoundsToLocal( bounds );
	}

	public BBox GetTexelBounds( Vector3Int voxel, Vector3Int texel )
	{
		var sdf = GetSdfTexture( voxel );
		return sdf.TexelToBounds( texel );
	}

	private BBox GetNodeBounds( SparseVoxelOctree<SignedDistanceField>.OctreeNode node )
	{
		var bounds = Octree.GetNodeBounds( node );
		return OctreeBoundsToLocal( bounds );
	}
	public void DebugDraw( Transform tx, Vector3Int highlightedPosition, Vector3 selectedPosition, int currentSlice )
	{
		var overlay = DebugOverlaySystem.Current;
		var camera = Game.ActiveScene.Camera;
		var hasSelectedPosition = selectedPosition.x > -1;
		var hasHighlightedPosition = highlightedPosition.x > -1;

		if ( Octree?.RootNode is null || overlay is null )
			return;

		DrawChildren( Octree.RootNode );

		void DrawChildren( SparseVoxelOctree<SignedDistanceField>.OctreeNode node )
		{
			var bbox = GetNodeBounds( node );
			var color = Color.Blue.WithAlpha( 0.05f );
			var ignoreDepth = false;
			var depthOffset = 0f;
			if ( node.IsLeaf )
			{
				var isActiveSlice = currentSlice < 0 || node.Position.z == currentSlice;
				if ( node.Data?.IsRebuilding == true )
				{
					color = Color.Orange.WithAlpha( 1f );
					ignoreDepth = true;
					depthOffset = 0.5f;
				}
				else if ( node.Position == selectedPosition )
				{
					color = Color.Green.WithAlpha( 1f );
					ignoreDepth = true;
					depthOffset = 0.5f;
				}
				else if ( node.Position == highlightedPosition )
				{
					color = Color.Magenta.WithAlpha( 1f );
					ignoreDepth = true;
					depthOffset = 0.5f;
				}
				else if ( node.Data is null )
				{
					if ( isActiveSlice )
					{
						color = Color.Red.WithAlpha( 0.5f );
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
						var camPoint = Bounds.ClosestPoint( tx.PointToLocal( camera.WorldPosition ) );
						var camDir = tx.NormalToLocal( camera.WorldRotation.Forward );
						var closeness = camPoint.Distance( bbox.Center ) / (OctreeSize);
						color = Color.Lerp( Color.Yellow, Color.Gray, closeness );
						color = color.WithAlpha( 1f );
					}
					else
					{
						color = Color.Gray.WithAlpha( 0.05f );
					}
				}
			}
			if ( depthOffset > 0f )
			{
				bbox = bbox.Grow( depthOffset );
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
