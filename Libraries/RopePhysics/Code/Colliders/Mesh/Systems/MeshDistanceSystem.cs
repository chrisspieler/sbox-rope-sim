using System.Security.Cryptography;

namespace Duccsoft;

public partial class MeshDistanceSystem : GameObjectSystem<MeshDistanceSystem>
{
	[ConVar( "rope_mdf_texel_density" )]
	public static float TexelPerWorldUnit { get; set; } = 1f;
	public static float TexelSize => 1f / TexelPerWorldUnit;

	public int MdfCount => _meshDistanceFields.Count;
	public long MdfTotalDataSize => _meshDistanceFields.Select( mdf => mdf.Value.DataSize ).Sum();
	private MeshDistanceBuildSystem BuildSystem => MeshDistanceBuildSystem.Current;

	public MeshDistanceSystem( Scene scene ) : base( scene ) { }

	private readonly Dictionary<int, MeshDistanceField> _meshDistanceFields = [];
	private readonly Dictionary<PhysicsShape, int> _physicsShapes = [];
	private readonly Dictionary<Model, int> _models = [];
	private readonly Dictionary<MeshDistanceConfig, int> _configs = [];

	public bool TryGet( int id, out MeshDistanceField mdf )
	{
		return _meshDistanceFields.TryGetValue( id, out mdf );
	}

	public bool TryGet( PhysicsShape shape, out MeshDistanceField mdf )
	{
		if ( !_physicsShapes.TryGetValue( shape, out int mdfId ) )
		{
			mdf = null;
			return false;
		}

		var result = TryGet( mdfId, out mdf );
		if ( !result )
		{
			_physicsShapes.Remove( shape );
		}
		return result;
	}

	public bool TryGet( Model model, out MeshDistanceField mdf )
	{
		if ( !_models.TryGetValue( model, out int mdfId ) )
		{
			mdf = null;
			return false;
		}

		var result = TryGet( mdfId, out mdf );
		if ( !result )
		{
			_models.Remove( model );
		}
		return result;
	}

	public MeshDistanceField GetOrCreate( MeshDistanceConfig config )
	{
		if ( !config.IsValid() )
			return null;

		if ( !_configs.TryGetValue( config, out int mdfId ) )
		{
			mdfId = config.GetId();
			var mdf = CreateMdf( mdfId, config );
			_configs[config] = mdfId;
			return mdf;
		}
		return GetMdf( mdfId );
	}

	internal MeshDistanceField CreateMdf( int id, MeshDistanceConfig config = null )
	{
		var mdf = new MeshDistanceField( MeshDistanceBuildSystem.Current, id, config );
		_meshDistanceFields[mdf.Id] = mdf;
		if ( config is not null )
		{
			if ( config.MeshSource == MeshDistanceConfig.MeshSourceType.Model )
			{
				if ( config.Model is not null )
				{
					BuildSystem.AddExtractMeshJob( id, config.Model );
					mdf.SourceModel = config.Model;
					_models[config.Model] = id;
				}
			}
			else if ( config.MeshSource == MeshDistanceConfig.MeshSourceType.Collider )
			{
				var shape = config.GetPhysicsShape();
				if ( shape.IsValid() )
				{
					BuildSystem.AddExtractMeshJob( id, shape );
					mdf.SourceShape = shape;
					_physicsShapes[shape] = id;
				}
			}
		}
		return mdf;
	}

	public MeshDistanceField GetMdf( int id )
	{
		_meshDistanceFields.TryGetValue( id, out var mdf );
		return mdf;
	}

	public MeshDistanceField this[int id] => GetMdf( id );

	public void RemoveMdf( int id )
	{
		if ( _meshDistanceFields.TryGetValue( id, out var mdf ) )
		{
			MeshDistanceBuildSystem.Current.StopBuild( id );
			_meshDistanceFields.Remove( id );
			if ( mdf.Config is not null )
			{
				_configs.Remove( mdf.Config );
			}
			if ( mdf.SourceModel is not null )
			{
				_models.Remove( mdf.SourceModel );
			}
			if ( mdf.SourceShape is not null )
			{
				_physicsShapes.Remove( mdf.SourceShape );
			}
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
			return [];

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
			Current.TryGet( tr.Shape, out var mdf );
			if ( mdf is null )
				continue;

			yield return new MdfQueryResult
			{
				LocalPosition = localPos,
				Distance = localPos.Length,
				GameObject = gameObject,
				Mdf = mdf,
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
			Current.TryGet( renderer.Model, out var mdf );

			if ( mdf is null )
				continue;

			yield return new MdfQueryResult()
			{
				LocalPosition = localPos,
				Distance = distance,
				GameObject = renderer.GameObject,
				Mdf = mdf,
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
			Current.TryGet( renderer.Model, out var mdf );

			if ( mdf is null )
				continue;

			yield return new MdfQueryResult()
			{
				LocalPosition = localPos,
				Distance = distance,
				GameObject = renderer.GameObject,
				Mdf = mdf,
				Model = renderer.Model,
			};
		}
	}
}
