namespace Duccsoft;

public class MeshDistanceConfig : Component
{
	public enum MeshSourceType
	{
		Model,
		Collider
	}

	[Property, Range( 1, 5, 1 )] public int TextureExponent
	{
		get => (int)MathF.Log2( TextureResolution );
		set => TextureResolution = 2 << value;
	}
	[Property, ReadOnly] public int TextureResolution { get; private set; } = 16;
	[Property] public MeshSourceType MeshSource { get; set; } = MeshSourceType.Model;
	[Property, ShowIf( nameof( MeshSource ), MeshSourceType.Model )]
	public Model Model { get; set; }
	[Property, ShowIf( nameof( MeshSource ), MeshSourceType.Collider )]
	public Collider Collider { get; set; }

	[Property]
	public bool BakeOnStart { get; set; } = false;

	public MeshDistanceField Mdf { get; internal set; }
	private bool _isSelected;

	protected override void DrawGizmos()
	{
		_isSelected = Gizmo.IsSelected;
	}

	protected override void OnStart()
	{
		if ( BakeOnStart )
		{
			BakeMeshDistanceField();
		}
	}

	protected override void OnUpdate()
	{
		if ( Mdf is not null && ( _isSelected || Mdf.IsBuilding ) )
		{
			Mdf.DebugDraw( WorldTransform, -1, -1, -1 );
		}
	}

	[Button("Bake")]
	public void BakeMeshDistanceField()
	{
		Mdf = MeshDistanceSystem.Current.CreateMdf( GetId(), this );
	}

	public int GetId()
	{
		if ( MeshSource == MeshSourceType.Model )
		{
			return MeshDistanceField.GetId( Model );
		}
		else if ( MeshSource == MeshSourceType.Collider )
		{
			return MeshDistanceField.GetId( Collider );
		}

		return GetHashCode();
	}

	public PhysicsShape GetPhysicsShape()
	{
		return Collider?.KeyframeBody?.Shapes?.FirstOrDefault();
	}
}
