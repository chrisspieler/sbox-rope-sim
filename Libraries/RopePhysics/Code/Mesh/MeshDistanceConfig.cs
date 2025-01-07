namespace Duccsoft;

public class MeshDistanceConfig : Component
{
	public enum MeshSourceType
	{
		Model,
		Collider
	}

	[Property, Range(1, 5, 1)] public int TextureExponent
	{
		get => (int)MathF.Log2( TextureResolution );
		set => TextureResolution = 2 << value;
	}
	[Property, ReadOnly] public int TextureResolution { get; private set; } = 16;
	[Property] public MeshSourceType MeshSource { get; set; } = MeshSourceType.Model;
	[Property, ShowIf( nameof(MeshSource), MeshSourceType.Model)] 
	public Model Model { get; set; }
	[Property, ShowIf( nameof( MeshSource ), MeshSourceType.Collider )]
	public Collider Collider { get; set; }

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
