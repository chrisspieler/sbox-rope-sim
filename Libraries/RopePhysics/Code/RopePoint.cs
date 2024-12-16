using Sandbox.Diagnostics;

namespace Duccsoft;

[Hide]
internal class RopePoint : Component
{
	[Property] public float Thickness { get; set; } = 1f;
	[Property] public float Length { get; set; } = 2f;
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
				_fixedJoint.Flags	|= ComponentFlags.Hidden | ComponentFlags.NotSaved;
				_springJoint.Flags	|= ComponentFlags.Hidden | ComponentFlags.NotSaved;
			}
			else
			{
				_rigidbody.Flags	&= ~(ComponentFlags.Hidden | ComponentFlags.NotSaved);
				_collider.Flags		&= ~(ComponentFlags.Hidden | ComponentFlags.NotSaved);
				_fixedJoint.Flags	&= ~(ComponentFlags.Hidden | ComponentFlags.NotSaved);
				_springJoint.Flags	&= ~(ComponentFlags.Hidden | ComponentFlags.NotSaved);
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
		_fixedJoint = AddComponent<FixedJoint>( startEnabled: true );
		_fixedJoint.StartBroken = true;
		_springJoint = AddComponent<SpringJoint>( startEnabled: true );
		_springJoint.StartBroken = true;

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
		DeleteComponent( ref _fixedJoint );
		DeleteComponent( ref _springJoint );
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

	public void LinkTo( RopePoint point )
	{
		if ( !HasReachedFirstUpdate )
		{
			_actionQueue += () => LinkTo( point );
			return;
		}

		Assert.IsValid( _springJoint );

		_springJoint.Body = point.GameObject;
		_springJoint.Frequency = 50f;
		_springJoint.Damping = 2f;
		_springJoint.MinLength = point.Thickness;
		_springJoint.MaxLength = Length;
		_springJoint.Enabled = true;
		if ( point.IsValid() )
		{
			_springJoint.Unbreak();
		}
		else
		{
			_springJoint.Break();
		}
	}


	public void FixTo( GameObject gameObject )
	{
		if ( !HasReachedFirstUpdate )
		{
			_actionQueue += () => FixTo( gameObject );
			return;
		}

		Assert.IsValid( _fixedJoint );

		_fixedJoint.Body = gameObject;
		_fixedJoint.AngularFrequency = 0f;
		_fixedJoint.AngularDamping = 0f;
		_fixedJoint.LinearFrequency = 0f;
		_fixedJoint.LinearDamping = 0f;
		_fixedJoint.Enabled = true;
		if ( gameObject.IsValid() )
		{
			_fixedJoint.Unbreak();
		}
		else
		{
			_fixedJoint.Break();
		}
	}
}
