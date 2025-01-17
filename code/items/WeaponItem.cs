﻿using Sandbox;
using System.Collections.Generic;
using System.IO;

namespace Facepunch.Forsaken;

public class WeaponItem : ResourceItem<WeaponResource, WeaponItem>, IContainerItem, ILootSpawnerItem, IPurchasableItem, IRecyclableItem
{
	public override string PrimaryUseHint => InternalWeapon?.PrimaryUseHint ?? "Attack";
	public override string SecondaryUseHint => InternalWeapon?.SecondaryUseHint ?? (ForsakenGame.Isometric ? "(Hold) Aim" : string.Empty);
	public override Color Color => ItemColors.Weapon;

	public virtual int WorldModelMaterialGroup => Resource?.WorldModelMaterialGroup ?? 0;
	public virtual string WeaponName => Resource?.ClassName ?? string.Empty;
	public virtual int DefaultAmmo => Resource?.DefaultAmmo ?? 0;
	public virtual int ClipSize => Resource?.ClipSize ?? 0;
	public virtual int Damage => Resource?.Damage ?? 0;
	public virtual AmmoType AmmoType => Resource?.AmmoType ?? AmmoType.None;
	public virtual Curve DamageFalloff => Resource?.DamageFalloff ?? default;
	public virtual Curve RecoilCurve => Resource?.RecoilCurve ?? default;
	public virtual int StockStackSize => Resource?.StockStackSize.GetValue().CeilToInt() ?? default;
	public virtual int LootStackSize => Resource?.LootStackSize.GetValue().CeilToInt() ?? default;
	public virtual bool OncePerContainer => Resource?.OncePerContainer ?? default;
	public virtual float StockChance => Resource?.StockChance ?? default;
	public virtual float LootChance => Resource?.LootChance ?? default;
	public virtual int SalvageCost => Resource?.SalvageCost ?? default;
	public virtual bool IsPurchasable => Resource?.IsPurchasable ?? default;
	public virtual bool IsLootable => Resource?.IsLootable ?? default;
	public virtual Dictionary<string, int> RecycleOutput => Resource?.RecycleOutput ?? default;
	public virtual float BaseComponentReturn => Resource?.BaseComponentReturn ?? 0.5f;
	public virtual bool IsRecyclable => Resource?.IsRecyclable ?? default;

	public AttachmentContainer Attachments { get; private set; }
	public InventoryContainer Container => Attachments;
	public string ContainerName => "Attachments";

	public Weapon Weapon
	{
		get => InternalWeapon;
		set
		{
			if ( InternalWeapon != value )
			{
				InternalWeapon = value;
				OnWeaponChanged( value );
			}
		}
	}

	public AmmoItem AmmoDefinition
	{
		get => InternalAmmoDefinition;
		set
		{
			if ( InternalAmmoDefinition != value )
			{
				InternalAmmoDefinition = value;
				IsDirty = true;
			}
		}
	}

	public int AmmoCount
	{
		get => InternalAmmoCount;
		set
		{
			if ( InternalAmmoCount != value )
			{
				InternalAmmoCount = value;
				IsDirty = true;
			}
		}
	}

	private AmmoItem InternalAmmoDefinition;
	private Weapon InternalWeapon;
	private int InternalAmmoCount;

	public virtual void OnActiveStart( ForsakenPlayer player )
	{
		foreach ( var attachment in Attachments.FindItems<AttachmentItem>() )
		{
			attachment.OnActiveStart( this, player );
		}
	}

	public virtual void OnActiveEnd( ForsakenPlayer player )
	{
		foreach ( var attachment in Attachments.FindItems<AttachmentItem>() )
		{
			attachment.OnActiveEnd( this, player );
		}
	}

	public override bool CanStackWith( InventoryItem other )
	{
		return false;
	}

	public override bool OnTrySwap( InventoryItem other )
	{
		if ( other is AttachmentItem )
		{
			Attachments.Give( other );
			return false;
		}

		return true;
	}

	public override void Write( BinaryWriter writer )
	{
		writer.Write( Attachments );

		if ( Weapon.IsValid() )
			writer.Write( Weapon.NetworkIdent );
		else
			writer.Write( 0 );

		writer.Write( InternalAmmoCount );

		if ( AmmoDefinition.IsValid() )
		{
			writer.Write( true );
			writer.Write( AmmoDefinition.UniqueId );
		}
		else
		{
			writer.Write( false );
		}

		base.Write( writer );
	}

	public override void Read( BinaryReader reader )
	{
		Attachments = reader.ReadInventoryContainer() as AttachmentContainer;
		Weapon = (Entity.FindByIndex( reader.ReadInt32() ) as Weapon);
		InternalAmmoCount = reader.ReadInt32();

		if ( reader.ReadBoolean() )
			InternalAmmoDefinition = InventorySystem.GetDefinition( reader.ReadString() ) as AmmoItem;
		else
			InternalAmmoDefinition = null;

		base.Read( reader );
	}

	public override void OnCreated()
	{
		if ( Game.IsServer )
		{
			Attachments = new AttachmentContainer();
			Attachments.SetParent( this );
			InventorySystem.Register( Attachments );
		}

		base.OnCreated();
	}

	public override void OnRemoved()
	{
		if ( Game.IsServer && Attachments.IsValid() )
		{
			InventorySystem.Remove( Attachments );
		}

		if ( Game.IsServer && Weapon.IsValid() )
		{
			Weapon.Delete();
		}

		base.OnRemoved();
	}

	protected virtual void OnWeaponChanged( Weapon weapon )
	{
		foreach ( var attachment in Attachments.FindItems<AttachmentItem>() )
		{
			attachment.OnWeaponChanged( weapon );
		}
	}

	protected override void BuildTags( HashSet<string> tags )
	{
		tags.Add( "weapon" );

		base.BuildTags( tags );
	}
}
