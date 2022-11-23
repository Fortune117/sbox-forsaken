﻿using Sandbox;

namespace Facepunch.Forsaken;

public partial class EquipmentContainer : InventoryContainer
{
	public EquipmentContainer( Entity owner ) : base( owner )
	{
		SetSlotLimit( 3 );
	}

	public override InventoryContainer GetTransferTarget()
	{
		if ( Entity is ForsakenPlayer player )
		{
			return UI.Storage.Current.IsOpen ? UI.Storage.Current.Container : ForsakenPlayer.Me.Backpack;
		}

		return base.GetTransferTarget();
	}

	public override bool CanGiveItem( ushort slot, InventoryItem item )
	{
		if ( item is not ArmorItem armor )
			return false;

		if ( armor.ArmorSlot == ArmorSlot.Head )
			return slot == 0;

		if ( armor.ArmorSlot == ArmorSlot.Chest )
			return slot == 1;

		if ( armor.ArmorSlot == ArmorSlot.Legs )
			return slot == 2;

		return false;
	}
}
