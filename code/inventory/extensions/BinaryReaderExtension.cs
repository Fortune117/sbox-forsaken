﻿using Sandbox;
using System.IO;

namespace Facepunch.Forsaken;

public static class BinaryReaderExtension
{
	public static InventoryItem ReadInventoryItem( this BinaryReader buffer )
	{
		var uniqueId = buffer.ReadString();

		if ( !string.IsNullOrEmpty( uniqueId ) )
		{
			var stackSize = buffer.ReadUInt16();
			var itemId = buffer.ReadUInt64();
			var slotId = buffer.ReadUInt16();

			var instance = InventorySystem.CreateItem( uniqueId, itemId );

			if ( instance != null )
			{
				instance.StackSize = stackSize;
				instance.SlotId = slotId;
				instance.Read( buffer );
			}

			return instance;
		}
		else
		{
			return null;
		}
	}

	public static InventoryContainer ReadInventoryContainer( this BinaryReader buffer )
	{
		var typeName = buffer.ReadString();
		var parentItemId = buffer.ReadUInt64();
		var inventoryId = buffer.ReadUInt64();
		var slotLimit = buffer.ReadUInt16();
		var entity = buffer.ReadEntity();

		var container = InventorySystem.Find( inventoryId );

		if ( container == null )
		{
			var type = TypeLibrary.GetDescription( typeName );

			if ( type == null )
			{
				Log.Error( $"Unable to create an inventory container with unknown type id ({typeName})!" );
				return null;
			}

			container = type.Create<InventoryContainer>();
			container.SetEntity( entity );
			container.SetParent( parentItemId );
			container.SetSlotLimit( slotLimit );
			InventorySystem.Register( container, inventoryId );
		}
		else
		{
			container.SetEntity( entity );
			container.SetParent( parentItemId );
			container.SetSlotLimit( slotLimit );
		}

		for ( var i = 0; i < slotLimit; i++ )
		{
			var isValid = buffer.ReadBoolean();

			if ( isValid )
			{
				var item = buffer.ReadInventoryItem();
				item.IsValid = true;
				item.Parent = container;

				if ( Host.IsServer )
					container.Replace( (ushort)i, item );
				else
					container.ItemList[i] = item;
			}
			else
			{
				if ( Host.IsServer )
					container.ClearSlot( (ushort)i );
				else
					container.ItemList[i] = null;
			}
		}

		container.Deserialize( buffer );

		return container;
	}

}
