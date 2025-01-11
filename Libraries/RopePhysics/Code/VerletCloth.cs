using Sandbox;

namespace Duccsoft;

public class VerletCloth : VerletComponent
{
	[Property, Range(4, 64, 1), Change] public int ClothResolution { get; set; } = 16;
	private void OnClothResolutionChanged( int oldValue, int newValue ) => ResetSimulation();
	[Property, Range( 0.05f, 10f )] public float Radius { get; set; } = 1f;
	[Property] public bool DebugDrawPoints { get; set; } = false;

	public Model Model { get; private set; }
	private Mesh Mesh { get; set; }
	private SimpleVertex[] Vertices { get; set; }
	private int[] Indices { get; set; }


	protected override void OnUpdate()
	{
		base.OnUpdate();

		UpdateModel();
		SimData.Radius = Radius;
		SimData.Iterations = (int)Stiffness.Remap( 0f, 1f, 1, 80 );
	}

	protected override void DrawGizmos()
	{
		using var scope = Gizmo.Scope( "Verlet Cloth" );

		Gizmo.Transform = Scene.LocalTransform;

		if ( !Gizmo.IsSelected )
			return;

		if ( !DebugDrawPoints )
			return;

		Gizmo.Draw.Color = Color.Green;
		for ( int i = 0; i < SimData.CpuPoints.Length; i++ )
		{
			var point = SimData.CpuPoints[i];
			Gizmo.Draw.LineSphere( new Sphere( point.Position, Radius ) );
		}
	}

	protected override SimulationData CreateSimData()
	{
		var physics = Scene.PhysicsWorld;
		var startPos = WorldPosition;
		var endPos = EndPoint?.WorldPosition ?? startPos + Vector3.Right * 128f;
		var points = ClothGenerator.Generate( startPos, endPos, ClothResolution, out float spacing );
		List<VerletStickConstraint> sticks = [];
		for ( int y = 0; y < ClothResolution; y++ )
		{
			for ( int x = 0; x < ClothResolution; x++ )
			{
				var iA = y * ClothResolution + x;
				var iB = y * ClothResolution + x + 1;
				var iC = (y + 1) * ClothResolution + x + 1;
				var iD = (y + 1) * ClothResolution + x;

				if ( iB < points.Length )
				{
					sticks.Add( new VerletStickConstraint( iA, iB, spacing ) );
				}
				if ( iB < points.Length && iC < points.Length )
				{
					sticks.Add( new VerletStickConstraint( iB, iC, spacing ) );
				}
				if ( iC < points.Length && iD < points.Length )
				{
					sticks.Add( new VerletStickConstraint( iC, iD, spacing ) );
				}
				if ( iD < points.Length )
				{
					sticks.Add( new VerletStickConstraint( iD, iA, spacing ) );
				}
			}
		}
		return new SimulationData( physics, points, [.. sticks], ClothResolution, spacing );
	}
	#region Rendering
	[Property] public Material Material { get; set; }
	private SceneModel _so;
	protected override void CreateRenderer()
	{
		_so = new SceneModel( Scene.SceneWorld, Model.Plane, WorldTransform );
		_so.Flags.IsOpaque = true;
		_so.Flags.CastShadows = true;
	}

	protected override void DestroyRenderer()
	{
		_so?.Delete();
		_so = null;
	}

	protected override void OnPreRender()
	{
		if ( _so.IsValid() )
		{
			_so.Transform = WorldTransform;
		}
	}

	private void UpdateModel()
	{
		if ( !_so.IsValid() || SimData?.CpuPoints is null )
			return;

		int numVertices = ClothResolution * ClothResolution;
		bool vertexCountChanged = false;
		if ( Vertices is null || Vertices.Length != numVertices )
		{
			vertexCountChanged = true;
			Vertices = new SimpleVertex[numVertices];
		}

		for ( int y = 0; y < ClothResolution; y++ )
		{
			for ( int x = 0; x < ClothResolution; x++ )
			{
				var i = y * ClothResolution + x;
				var worldPos = SimData.CpuPoints[i].Position;
				// worldPos += worldPos - SimData.CollisionBounds.Center;
				var localPos = WorldTransform.PointToLocal( worldPos );
				var uv = new Vector2( (float)x / ClothResolution, (float)y / ClothResolution );
				var vtx = new SimpleVertex( localPos, Vector3.Up, Vector3.Right, uv );
				Vertices[i] = vtx;
			}
		}

		if ( Indices is null || vertexCountChanged )
		{
			UpdateIndices();
		}

		if ( vertexCountChanged || Mesh is null || Model is null || _so.Model is null )
		{
			Mesh = new Mesh( Material );
			Mesh.CreateVertexBuffer( numVertices, SimpleVertex.Layout, default( Span<SimpleVertex> ) );
			Mesh.CreateIndexBuffer( Indices.Length, Indices );
			Model = Model.Builder.AddMesh( Mesh ).Create();
			_so.Model = Model;
		}
		if ( Mesh.VertexCount != Vertices.Length )
		{
			Mesh.SetVertexBufferSize( Vertices.Length );
		}
		Mesh.LockVertexBuffer<SimpleVertex>( d => Vertices.CopyTo( d ) );
		Mesh.Bounds = BBox.FromPoints( Vertices.Select( v => v.position ) );
	}

	private void UpdateIndices()
	{
		int verticesPerLine = ClothResolution;
		List<int> indices = [];
		// The index buffer loop was copy/pasted from the water plane tesselation used in Sam's water sim on Testbed.
		for ( int i = 0; i < ClothResolution - 1; i++ )
		{
			for ( int j = 0; j < ClothResolution - 1; j++ )
			{
				int start = i * verticesPerLine + j;
				indices.Add( start );
				indices.Add( start + 1 );
				indices.Add( start + verticesPerLine );

				indices.Add( start + verticesPerLine );
				indices.Add( start + 1 );
				indices.Add( start + verticesPerLine + 1 );
			}
		}
		Indices = [.. indices];
	}
	#endregion
}
