﻿using Sandbox;

namespace Facepunch.Forsaken;

public abstract partial class NPC : AnimatedEntity
{
	protected virtual bool UseMoveHelper => false;
	protected virtual bool UseGravity => false;

	protected Vector3 TargetLocation { get; set; }
	protected TimeUntil NextWanderTime { get; set; }
	protected Vector3 WishDirection { get; set; }
	protected NavPath Path { get; set; }

	public void RotateOverTime( Vector3 direction )
	{
		var targetRotation = Rotation.LookAt( direction.WithZ( 0f ), Vector3.Up );
		Rotation = Rotation.Lerp( Rotation, targetRotation, Time.Delta * 10f );
	}

	public void RotateOverTime( Entity target )
	{
		var direction = (target.Position - Position).Normal;
		var targetRotation = Rotation.LookAt( direction.WithZ( 0f ), Vector3.Up );
		Rotation = Rotation.Lerp( Rotation, targetRotation, Time.Delta * 10f );
	}

	public override void Spawn()
	{
		Tags.Add( "npc" );

		base.Spawn();
	}

	public bool HasValidPath()
	{
		if ( Path is null ) return false;
		if ( Path.Count == 0 ) return false;
		return true;
	}

	public virtual bool ShouldWander()
	{
		return false;
	}

	public virtual string GetDisplayName()
	{
		return "NPC";
	}

	public virtual float GetMoveSpeed()
	{
		return 80f;
	}

	public virtual float GetIdleDuration()
	{
		return 30f;
	}

	protected void SnapToNavMesh()
	{
		var closest = NavMesh.GetClosestPoint( Position );

		if ( closest.HasValue )
			Position = closest.Value;
	}

	protected bool MoveToLocation( Vector3 position, float stepSize = 24f )
	{
		var closestPoint = NavMesh.GetClosestPoint( position );
		if ( !closestPoint.HasValue ) return false;

		TargetLocation = closestPoint.Value;

		Path = NavMesh.PathBuilder( Position )
			.WithMaxClimbDistance( 0f )
			.WithMaxDropDistance( 128f )
			.WithAgentHull( NavAgentHull.Any )
			.WithPartialPaths()
			.WithStepHeight( stepSize )
			.Build( TargetLocation );

		return (Path?.Count ?? 0) > 0;
	}

	protected bool TryGetNavMeshPosition( float minRadius, float maxRadius, out Vector3 position )
	{
		var targetPosition = NavMesh.GetPointWithinRadius( Position, minRadius, maxRadius );

		if ( targetPosition.HasValue )
		{
			position = targetPosition.Value;
			return true;
		}

		position = default;
		return false;
	}

	[Event.Tick.Server]
	protected virtual void ServerTick()
	{
		if ( LifeState == LifeState.Dead )
		{
			Velocity = Vector3.Zero;
			HandleAnimation();
			return;
		}

		if ( ShouldWander() && !HasValidPath() && NavMesh.IsLoaded )
		{
			if ( NextWanderTime && TryGetNavMeshPosition( 1000f, 5000f, out var targetPosition ) )
			{
				MoveToLocation( targetPosition );
				NextWanderTime = GetIdleDuration();
			}
		}

		if ( UseGravity )
		{
			var trace = Trace.Ray( Position + Vector3.Up * 8f, Position + Vector3.Down * 32f )
				.WorldOnly()
				.Ignore( this )
				.Run();

			GroundEntity = trace.Entity;
			Velocity += Vector3.Down * 700f * Time.Delta;
			Velocity = ApplyFriction( Velocity, 4f );
		}
		else
		{
			GroundEntity = Game.WorldEntity;
			Velocity = ApplyFriction( Velocity, 4f );
		}

		var wishDirection = GetWishDirection();

		UpdateVelocity( wishDirection );
		UpdateRotation( wishDirection );

		HandleAnimation();

		if ( UseMoveHelper )
		{
			var mover = new MoveHelper( Position, Velocity );

			mover.Trace = mover.SetupTrace()
				.WithoutTags( "passplayers", "player" )
				.WithAnyTags( "solid", "playerclip", "passbullets" )
				.Size( GetHull() )
				.Ignore( this );

			mover.MaxStandableAngle = 40f;
			mover.TryMoveWithStep( Time.Delta, 24f );

			Position = mover.Position;
			Velocity = mover.Velocity;
		}
		else
		{
			Position += Velocity * Time.Delta;
		}
	}

	protected virtual void UpdateRotation( Vector3 direction )
	{
		if ( direction.Length > 0f )
		{
			RotateOverTime( direction );
		}
	}

	protected virtual void UpdateVelocity( Vector3 direction )
	{
		Velocity = Accelerate( Velocity, direction, GetMoveSpeed(), 0f, 8f );
	}

	protected virtual Vector3 ApplyFriction( Vector3 velocity, float amount = 1f )
	{
		var speed = Velocity.Length;
		if ( speed < 0.1f ) return velocity;

		var control = (speed < 100f) ? 100f : speed;
		var newSpeed = speed - (control * Time.Delta * amount);

		if ( newSpeed < 0 ) newSpeed = 0;
		if ( newSpeed == speed ) return velocity;

		newSpeed /= speed;
		velocity *= newSpeed;

		return velocity;
	}

	protected virtual void HandleAnimation()
	{

	}

	protected virtual TraceResult TraceBBox( Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs )
	{
		var trace = Trace.Ray( start, end )
			.Size( mins, maxs )
			.WithoutTags( "passplayers", "trigger" )
			.WithAnyTags( "solid" )
			.Ignore( this )
			.Run();

		return trace;
	}

	protected virtual BBox GetHull()
	{
		var girth = 12f;
		var mins = new Vector3( -girth, -girth, 0f );
		var maxs = new Vector3( +girth, +girth, 72f );
		return new BBox( mins, maxs );
	}

	protected virtual void OnFinishedPath()
	{
		NextWanderTime = GetIdleDuration();
	}

	protected virtual Vector3 Accelerate( Vector3 velocity, Vector3 wishDir, float wishSpeed, float speedLimit, float acceleration )
	{
		if ( speedLimit > 0 && wishSpeed > speedLimit )
			wishSpeed = speedLimit;

		var currentSpeed = Velocity.Dot( wishDir );
		var addSpeed = wishSpeed - currentSpeed;

		if ( addSpeed <= 0 )
			return velocity;

		var accelSpeed = acceleration * Time.Delta * wishSpeed * 1f;

		if ( accelSpeed > addSpeed )
			accelSpeed = addSpeed;

		velocity += wishDir * accelSpeed;

		return velocity;
	}

	protected virtual Vector3 GetWishDirection()
	{
		if ( !HasValidPath() )
			return Vector3.Zero;

		var firstSegment = Path.Segments[0];

		if ( Position.Distance( firstSegment.Position ) > 10f )
		{
			var direction = (firstSegment.Position - Position).Normal;
			return direction;
		}

		Path.Segments.RemoveAt( 0 );

		if ( Path.Segments.Count == 0 )
		{
			OnFinishedPath();
		}

		return Vector3.Zero;
	}

	[ForsakenEvent.NavBlockerAdded]
	protected virtual void OnNavBlockerAdded( Vector3 position )
	{
		if ( !HasValidPath() ) return;

		SnapToNavMesh();

		if ( !MoveToLocation( TargetLocation ) )
		{
			NextWanderTime = 0f;
		}
	}
}
