namespace Duccsoft;

public partial class MeshDistanceSystem : GameObjectSystem<MeshDistanceSystem>
{
	[ConVar( "rope_mdf_texel_density" )]
	public static float TexelPerWorldUnit { get; set; } = 1f;
	public static float TexelSize => 1f / TexelPerWorldUnit;

	public int MdfCount => _meshDistanceFields.Count;
	public int MdfTotalDataSize => _meshDistanceFields.Select( mdf => mdf.Value.DataSize ).Sum();
	private MeshDistanceBuildSystem BuildSystem => MeshDistanceBuildSystem.Current;

	public MeshDistanceSystem( Scene scene ) : base( scene ) { }

	private readonly Dictionary<int, MeshDistanceField> _meshDistanceFields = new();

	public bool Has( int id ) => _meshDistanceFields.ContainsKey( id );
	public bool Has( PhysicsShape shape ) => Has( MeshDistanceField.GetId( shape ) );
	public bool Has( Model model ) => Has( MeshDistanceField.GetId( model ) );

	public MeshDistanceField GetMdf( PhysicsShape shape )
	{
		var id = MeshDistanceField.GetId( shape );
		var mdf = GetMdf( id );

		if ( mdf.ExtractMeshJob is null )
		{
			BuildSystem.AddExtractMeshJob( id, shape );
		}
		return mdf;
	}

	public MeshDistanceField GetMdf( Model model )
	{
		var id = MeshDistanceField.GetId( model );
		var mdf = GetMdf( id );

		if ( mdf.ExtractMeshJob is null )
		{
			BuildSystem.AddExtractMeshJob( id, model );
		}
		return mdf;
	}

	public MeshDistanceField GetMdf( int id )
	{
		// Did we already build a mesh distance field?
		if ( _meshDistanceFields.TryGetValue( id, out var mdf ) )
			return mdf;

		mdf = new MeshDistanceField( MeshDistanceBuildSystem.Current, id );
		AddMdf( id, mdf );
		return mdf;
	}

	public MeshDistanceField this[int id] => GetMdf( id );

	internal void AddMdf( int id, MeshDistanceField mdf )
	{
		_meshDistanceFields[id] = mdf;
	}

	public void RemoveMdf( int id )
	{
		if ( _meshDistanceFields.TryGetValue( id, out var mdf ) )
		{
			MeshDistanceBuildSystem.Current.StopBuild( id );
			_meshDistanceFields.Remove( id );
		}
	}

	public struct MdfQueryResult
	{
		public Vector3 LocalPosition;
		public float Distance;
		public GameObject GameObject;
		public MeshDistanceField Mdf;
		public Collider Collider;
		public Model Model;
	}

	public static IEnumerable<MdfQueryResult> FindInBox( BBox box )
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() || !Game.IsPlaying )
			return default;

		List<MdfQueryResult> results = [];
		results.AddRange( FindPhysicsShapesInBox( box, scene.PhysicsWorld ) );
		results.AddRange( FindModelsInSceneBox( box, scene ) );
		return results;
	}

	public static IEnumerable<MdfQueryResult> FindInSphere( Vector3 worldPos, float radius )
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() || !Game.IsPlaying )
			return default;

		List<MdfQueryResult> results = [];
		results.AddRange( FindPhysicsShapesInSphere( worldPos, radius, scene.PhysicsWorld ) );
		results.AddRange( FindModelsInSceneSphere( worldPos, radius, scene ) );
		return results;
	}

	private static IEnumerable<MdfQueryResult> FindPhysicsShapesInBox( BBox box, PhysicsWorld physicsWorld )
	{
		var trs = physicsWorld
			.Trace
			.Box( box.Size, box.Center, box.Center )
			.WithoutTags( "noblockrope", "mdf_model" )
			.RunAll();

		return GetPhysicsShapesFromTraceResults( trs );
	}

	private static IEnumerable<MdfQueryResult> GetPhysicsShapesFromTraceResults( PhysicsTraceResult[] trs )
	{
		foreach ( var tr in trs )
		{
			var colliderType = tr.ClassifyColliderType();
			if ( !(colliderType == RopeColliderType.Mesh) )
				continue;

			var collider = tr.Shape?.Collider as Collider;
			if ( !collider.IsValid() )
				continue;


			var gameObject = collider.GameObject;
			var localPos = tr.Shape.Body.Transform.PointToLocal( tr.StartPosition );

			yield return new MdfQueryResult
			{
				LocalPosition = localPos,
				Distance = localPos.Length,
				GameObject = gameObject,
				Mdf = Current.GetMdf( tr.Shape ),
				Collider = collider,
			};
		}
	}

	private static IEnumerable<MdfQueryResult> FindPhysicsShapesInSphere( Vector3 center, float radius, PhysicsWorld physicsWorld )
	{
		var trs = physicsWorld
			.Trace
			.Sphere( radius, center, center )
			.WithoutTags( "noblockrope", "mdf_model" )
			.RunAll();

		return GetPhysicsShapesFromTraceResults( trs );
	}

	private static IEnumerable<MdfQueryResult> FindModelsInSceneSphere( Vector3 center, float radius, Scene scene )
	{
		var renderers = scene.GetAllComponents<ModelRenderer>();

		foreach( var renderer in renderers )
		{
			if ( renderer.Model is null )
				continue;

			// TODO: Make a component just for MDF models.
			if ( !renderer.Tags.Has( "mdf_model" ) )
				continue;

			if ( renderer.Tags.Has( "noblockrope" ) )
				continue;

			var distance = renderer.WorldPosition.Distance( center );
			if ( distance > radius )
				continue;

			var localPos = renderer.WorldTransform.PointToLocal( center );
			yield return new MdfQueryResult()
			{
				LocalPosition = localPos,
				Distance = distance,
				GameObject = renderer.GameObject,
				Mdf = Current.GetMdf( renderer.Model ),
				Model = renderer.Model,
			};
		}
	}

	private static IEnumerable<MdfQueryResult> FindModelsInSceneBox( BBox box, Scene scene )
	{
		var renderers = scene.GetAllComponents<ModelRenderer>();

		foreach ( var renderer in renderers )
		{
			if ( renderer.Model is null )
				continue;

			// TODO: Make a component just for MDF models.
			if ( !renderer.Tags.Has( "mdf_model" ) )
				continue;

			if ( renderer.Tags.Has( "noblockrope" ) )
				continue;

			if ( !box.Overlaps( renderer.Bounds ) )
				continue;

			var distance = renderer.WorldPosition.Distance( box.Center );

			var localPos = renderer.WorldTransform.PointToLocal( box.Center );
			yield return new MdfQueryResult()
			{
				LocalPosition = localPos,
				Distance = distance,
				GameObject = renderer.GameObject,
				Mdf = Current.GetMdf( renderer.Model ),
				Model = renderer.Model,
			};
		}
	}
}
