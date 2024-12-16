namespace Duccsoft;

public partial class RopePhysics : Component
{
	[Property] public int SegmentCount => RopePoints.Count - 1;
	[Property] public GameObject RopePointContainer { get; set; }
	public PhysicsBody PhysicsBody { get; set; }
	private List<RopePoint> RopePoints { get; set; } = new();

	protected override void OnEnabled()
	{
		PhysicsBody = new PhysicsBody( Scene.PhysicsWorld );
		PhysicsBody.SetComponentSource( this );
		CreateRope();
	}

	protected override void OnDisabled()
	{
		PhysicsBody?.Remove();
		DestroyRope();
	}

	private void DestroyRope()
	{
		foreach( var point in RopePoints )
		{
			point.DestroyGameObject();
		}
		RopePoints.Clear();
		RopePointContainer?.Destroy();
	}

	private void CreateRope()
	{
		RopePointContainer ??= new GameObject( true, "Rope Point Container" );

		var start = new GameObject( RopePointContainer, true, "Rope Start" ).AddComponent<RopePoint>();
		start.Initialize();
		RopePoints.Add( start );
		start.FixTo( PhysicsBody );
		var end = new GameObject( RopePointContainer, true, "Rope End" ).AddComponent<RopePoint>();
		end.Initialize();
		end.WorldPosition = WorldPosition + Vector3.Right * 20f;
		RopePoints.Add( end );
		end.LinkTo( start );
	}

	protected override void OnUpdate()
	{
		UpdateDebug();
	}

	protected override void OnFixedUpdate()
	{
		if ( PhysicsBody.IsValid() )
		{
			PhysicsBody.Transform = WorldTransform;
		}
	}
}
