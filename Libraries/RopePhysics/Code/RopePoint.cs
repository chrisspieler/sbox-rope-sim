using Sandbox.Physics;

using FixedJoint = Sandbox.Physics.FixedJoint;
using SpringJoint = Sandbox.Physics.SpringJoint;

namespace Duccsoft;

[Hide]
internal class RopePoint : Component
{
	[Property] public float Thickness 
	{
		get => _thickness;
		set
		{
			_thickness = value;
			if ( _springJoint.IsValid() )
			{
				_springJoint.MinLength = value;
			}
		}
	}
	private float _thickness = 1f;
	[Property] public float Length 
	{
		get => _length;
		set
		{
			_length = value;
			if ( _springJoint.IsValid() )
			{
				_springJoint.MaxLength = value;
			}
		}
	}
	private float _length = 20f;
	[Property] public bool HideSubcomponents
	{
		get => _hideSubcomponents;
		set
		{
			_hideSubcomponents = value;

			if ( !Game.IsPlaying || !HasAllSubcomponents )
				return;

			if ( value )
			{
				_rigidbody.Flags	|= ComponentFlags.Hidden | ComponentFlags.NotSaved;
				_collider.Flags		|= ComponentFlags.Hidden | ComponentFlags.NotSaved;
			}
			else
			{
				_rigidbody.Flags	&= ~(ComponentFlags.Hidden | ComponentFlags.NotSaved);
				_collider.Flags		&= ~(ComponentFlags.Hidden | ComponentFlags.NotSaved);
			}
		}
	}

	[Property, Group( "Debug" )]
	public bool LogPosition { get; set; } = false;

	private bool _hideSubcomponents = true;
	private Rigidbody _rigidbody;
	private SphereCollider _collider;
	private FixedJoint _fixedJoint;
	private SpringJoint _springJoint;
	private Action _actionQueue;

	private bool HasAllSubcomponents => _rigidbody is not null && _collider is not null && _fixedJoint is not null && _springJoint is not null;
	private bool HasReachedFirstUpdate { get; set; }
	

	public void Initialize()
	{
		DeleteSubComponents();

		_rigidbody = AddComponent<Rigidbody>();
		_rigidbody.RigidbodyFlags |= RigidbodyFlags.DisableCollisionSounds;
		_actionQueue += () => _rigidbody.PhysicsBody.AutoSleep = false;
		_rigidbody.LinearDamping = 2f;
		_rigidbody.AngularDamping = 5f;
		_collider = AddComponent<SphereCollider>();
		_collider.Radius = Thickness;

		if ( RopePhysics.DebugMode < 1 )
		{
			HideSubcomponents = _hideSubcomponents;
		}
		else
		{
			HideSubcomponents = false;
		}
	}

	private void DeleteSubComponents()
	{
		static void DeleteComponent<T>( ref T component ) where T : Component
		{
			component?.Destroy();
			component = null;
		}

		DeleteComponent( ref _rigidbody );
		DeleteComponent( ref _collider );
	}

	protected override void OnDestroy() => DeleteSubComponents();

	protected override void OnUpdate()
	{
		HasReachedFirstUpdate = true;

		_actionQueue?.Invoke();
		_actionQueue = null;

		if ( LogPosition )
		{
			Log.Info( WorldPosition );
		}
	}

	public void LinkTo( RopePoint point, float? length = null )
	{
		var body1 = _rigidbody?.PhysicsBody;
		var body2 = point?._rigidbody?.PhysicsBody;
		if ( !HasReachedFirstUpdate || !body1.IsValid() )
		{
			_actionQueue += () => LinkTo( point, length );
			return;
		}

		if ( length.HasValue )
		{
			Length = length.Value;
		}

		_springJoint?.Remove();
		if ( !body2.IsValid() )
			return;

		var point1 = PhysicsPoint.Local( body1 );
		var point2 = PhysicsPoint.Local( body2 );
		_springJoint = PhysicsJoint.CreateSpring( point1, point2, Thickness, Length );
		_springJoint.SpringLinear = new( 3f, 0.9f, -1f );
	}


	public void FixTo( PhysicsBody body2 )
	{
		var body1 = _rigidbody?.PhysicsBody;
		if ( !HasReachedFirstUpdate || !body1.IsValid() )
		{
			_actionQueue += () => FixTo( body2 );
			return;
		}

		_fixedJoint?.Remove();
		if ( !body2.IsValid() )
			return;

		var point1 = PhysicsPoint.Local( body1 );
		var point2 = PhysicsPoint.Local( body2 );
		_fixedJoint = PhysicsJoint.CreateFixed( point1, point2 );
		_fixedJoint.SpringLinear = new( 0f, 0f, -1f );
		_fixedJoint.SpringAngular = new( 0f, 0f, -1f );
	}
}
