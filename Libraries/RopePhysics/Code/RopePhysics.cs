using System;
using System.Drawing;

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

		AddRopeBeginning();
		AddRopeEnd();
	}

	private RopePoint CreateRopePoint( int index )
	{
		var go = new GameObject( RopePointContainer, true, $"Rope Point #{index}" );
		var ropePoint = go.AddComponent<RopePoint>();
		ropePoint.Initialize();
		return ropePoint;
	}

	/// <summary>
	/// Add a RopePoint to the end of this rope, returning its index.
	/// </summary>
	public int AddRopeEnd()
	{
		if ( RopePoints.Count == 0 )
			return AddRopeBeginning();

		var point = CreateRopePoint( RopePoints.Count );

		Line GetMidpointFromPrevious( int index )
		{
			var beforePos2 = RopePoints[index - 2].WorldPosition;
			var beforePos1 = RopePoints[index - 1].WorldPosition;
			var direction = Vector3.Direction( beforePos2, beforePos1 );
			var distance = Vector3.DistanceBetween( beforePos2, beforePos1 );
			return new Line( beforePos1, beforePos1 + direction * distance * 0.5f );
		}

		var line = RopePoints.Count switch
		{
			1 => new Line( WorldPosition, WorldPosition + Vector3.Right * 20f ),
			_ => GetMidpointFromPrevious( RopePoints.Count )
		};
		point.WorldPosition = line.End;
		point.Length = line.Delta.Length;
		point.LinkTo( RopePoints[^1] );
		RopePoints.Add( point );
		return RopePoints.Count - 1;
	}

	/// <summary>
	/// Insert a RopePoint at the specified index, returning its index.
	/// <br/><br/>
	/// The provided index will be clamped between 0 and the RopePoint count (both inclusive),
	/// so the index returned may differ from the index provided.
	/// </summary>
	public int AddRopePoint( int index )
	{
		if ( index <= 0 )
		{
			return AddRopeBeginning();
		}
		else if ( index >= RopePoints.Count )
		{
			return AddRopeEnd();
		}
		var point = CreateRopePoint( index );
		var before = RopePoints[index - 1];
		var after = RopePoints[index];
		RopePoints.Insert( index, point );
		after.LinkTo( point );
		point.LinkTo( before );
		point.WorldPosition = new Line( before.WorldPosition, after.WorldPosition ).Center;
		return index;
	}

	/// <summary>
	/// Add a RopePoint at the beginning of the rope, returning an index of 0.
	/// </summary>
	public int AddRopeBeginning()
	{
		var point = CreateRopePoint( 0 );
		if ( RopePoints.Count > 0 )
		{
			var existing = RopePoints[0];
			existing.FixTo( null );
			existing.LinkTo( point );
		}
		if ( RopePoints.Count > 1 )
		{
			var after = RopePoints[1];
			after.LinkTo( RopePoints[0] );
		}
		point.FixTo( PhysicsBody );
		if ( RopePoints.Count > 0 )
		{
			RopePoints.Insert( 0, point );
		}
		else
		{
			RopePoints.Add( point );
		}
		return 0;
	}

	protected override void OnUpdate()
	{
		var scope = new TextRendering.Scope( "Press E to add rope segment", Color.Yellow, Screen.Size.y * 0.02f, "Consolas" );
		Scene.Camera.Hud.DrawText( scope, new Vector2( Screen.Size.x * 0.5f, Screen.Size.y - Screen.Size.y * 0.05f ) );
		if ( Input.Pressed("use") )
		{
			AddRopePoint( 1 );
		}
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
