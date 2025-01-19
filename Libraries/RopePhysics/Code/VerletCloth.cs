using Sandbox;

namespace Duccsoft;

public class VerletCloth : VerletComponent
{
	[Property, Range(4, 64, 1), Change] public int ClothResolution { get; set; } = 16;
	private void OnClothResolutionChanged( int oldValue, int newValue ) => ResetSimulation();
	[Property, Range( 0.05f, 10f )] public float Radius { get; set; } = 1f;
	

	public Model Model { get; private set; }
	private Mesh Mesh { get; set; }
	private SimpleVertex[] Vertices { get; set; }
	private int[] Indices { get; set; }


	protected override void OnUpdate()
	{
		base.OnUpdate();

		UpdateModel();
		SimData.Radius = Radius;
		SimData.Iterations = Iterations;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

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
		var points = ClothGenerator.Generate( StartPosition, EndPosition, ClothResolution, out float spacing );
		return new SimulationData( physics, points, ClothResolution, spacing );
	}
	#region Rendering
	[Property] public Material Material 
	{
		get => _so?.TextureSourceMaterial ?? _textureSourceMaterial;
		set
		{
			_textureSourceMaterial = value;
			if ( _so.IsValid() )
			{
				_so.TextureSourceMaterial = _textureSourceMaterial;
			}
		}
	}
	private Material _textureSourceMaterial;
	[Property] public Color Tint
	{
		get => _so?.Tint ?? _tint;
		set
		{
			_tint = value;
			if ( _so.IsValid() )
			{
				_so.Tint = value;
			}
		}
	}
	private Color _tint = Color.White;
	[Property] public bool Wireframe { get; set; } = false;
	private SceneClothObject _so;
	protected override void CreateRenderer()
	{
		_so = new SceneClothObject( Scene.SceneWorld );
		_so.Flags.IsOpaque = true;
		_so.Flags.CastShadows = true;
		_so.TextureSourceMaterial = _textureSourceMaterial;
		_so.Tint = _tint;
	}

	protected override void DestroyRenderer()
	{
		_so?.Delete();
		_so = null;
	}

	protected override void OnPreRender()
	{
		if ( !_so.IsValid() )
			return;

		_so.Transform = WorldTransform;
		_so.EnableWireframe = Wireframe;
	}

	private void UpdateModel()
	{
		if ( !_so.IsValid() )
			return;

		if ( SimulateOnGPU )
		{
			UpdateModelGPU();
		}
	}

	private void UpdateModelGPU()
	{
		if ( SimData?.ReadbackVertices?.IsValid() != true )
			return;

		_so.Vertices = SimData.ReadbackVertices;
		_so.Indices = SimData.ReadbackIndices;
		_so.Bounds = SimData.Bounds;
	}

	public override void UpdateCpuVertexBuffer( VerletPoint[] points )
	{

	}
	#endregion
}
