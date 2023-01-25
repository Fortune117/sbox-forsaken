﻿using Sandbox;

namespace Facepunch.Forsaken;

[Library( "weapon_torch" )]
public partial class Torch : MeleeWeapon
{
	public override string PrimaryUseHint => IsIgnited ? "Extinguish" : "Ignite";
	public override string DamageType => "blunt";
	public override float MeleeRange => 80f;
	public override float PrimaryRate => 1.5f;
	public override float Force => 1f;

	[Net] private bool IsIgnited { get; set; }
	private PointLightEntity Light { get; set; }
	private Particles Effect { get; set; }

	public override void AttackPrimary()
	{
		if ( Game.IsServer )
		{
			IsIgnited = !IsIgnited;
		}
	}

	public override void ActiveEnd( Entity ent, bool dropped )
	{
		DestroyLight();

		base.ActiveEnd( ent, dropped );
	}

	protected override void OnDestroy()
	{
		DestroyLight();

		base.OnDestroy();
	}

	private void DestroyLight()
	{
		Effect?.Destroy();
		Effect = null;

		Light?.Delete();
		Light = null;
	}

	private void CreateLight()
	{
		DestroyLight();

		var attachment = GetAttachment( "light" );
		if ( !attachment.HasValue ) return;

		Light = new();
		Light.Position = attachment.Value.Position;
		Light.SetParent( this );
		Light.Range = 800f;
		Light.Color = Color.Orange;
		Light.Brightness = 0.2f;
	}

	[Event.Tick.Client]
	private void ClientTick()
	{
		if ( Owner is not ForsakenPlayer player ) return;
		if ( player.ActiveChild != this ) return;

		if ( IsIgnited )
		{
			var attachment = GetAttachment( "light" );
			if ( !attachment.HasValue ) return;

			if ( !Light.IsValid() )
			{
				CreateLight();
			}

			Effect ??= Particles.Create( "particles/example/int_from_model_example/int_from_model_example.vpcf" );
			Effect?.SetPosition( 0, attachment.Value.Position );
		}
		else
		{
			DestroyLight();
		}
	}
}
