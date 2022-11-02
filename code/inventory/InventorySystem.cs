﻿using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;

namespace Facepunch.Forsaken;

public static partial class InventorySystem
{
	public enum NetworkEvent
	{
		SendDirtyItems,
		MoveInventory,
		SplitInventory,
		TransferInventory,
		GiveItem,
		TakeItem
	}

	private static Dictionary<ulong, InventoryContainer> Containers { get; set; } = new();
	private static Dictionary<ulong, InventoryItem> Items { get; set; } = new();
	private static List<InventoryContainer> DirtyList { get; set; } = new();
	private static Queue<ulong> OrphanedItems { get; set; } = new();

	private static ulong NextContainerId { get; set; }
	private static ulong NextItemId { get; set; }

	public static bool IsServer => Host.IsServer;
	public static bool IsClient => Host.IsClient;

	public static void AddToDirtyList( InventoryContainer container )
	{
		DirtyList.Add( container );
	}

	public static ulong Register( InventoryContainer container, ulong inventoryId = 0 )
	{
		if ( inventoryId == 0 )
		{
			inventoryId = NextContainerId++;
		}

		container.SetInventoryId( inventoryId );
		Containers[inventoryId] = container;

		return inventoryId;
	}

	public static List<InventoryItem> Remove( InventoryContainer container, bool destroyItems = false )
	{
		var inventoryId = container.InventoryId;

		if ( Containers.Remove( inventoryId ) )
		{
			var itemList = container.RemoveAll();

			if ( destroyItems )
			{
				for ( var i = 0; i < itemList.Count; i++ )
				{
					RemoveItem( itemList[i] );
				}
			}

			return itemList;
		}

		return null;
	}

	public static InventoryContainer Find( ulong inventoryId )
	{
		if ( Containers.TryGetValue( inventoryId, out var container ) )
		{
			return container;
		}

		return null;
	}

	public static InventoryItem FindInstance( ulong itemId )
	{
		Items.TryGetValue( itemId, out var instance );
		return instance;
	}

	public static T FindInstance<T>( ulong itemId ) where T : InventoryItem
	{
		return (FindInstance( itemId ) as T);
	}

	public static void RemoveItem( InventoryItem instance )
	{
		var itemId = instance.ItemId;

		if ( Items.Remove( itemId ) )
		{
			instance.Container?.Remove( itemId );
			instance.OnRemoved();
		}
	}

	public static InventoryItem CreateDuplicateItem( InventoryItem item )
	{
		var description = TypeLibrary.GetDescription( item.GetType() );
		var duplicate = CreateItem( description.Name );
		
		using ( var writeStream = new MemoryStream() )
		{
			using ( var writer = new BinaryWriter( writeStream ) )
			{
				item.Write( writer );
			}

			var data = writeStream.ToArray();

			using ( var readStream = new MemoryStream( data ) )
			{
				using ( var reader = new BinaryReader( readStream ) )
				{
					duplicate.Read( reader );
				}
			}
		}

		return duplicate;
	}

	public static T CreateItem<T>( ulong itemId = 0 ) where T : InventoryItem
	{
		var description = TypeLibrary.GetDescription( typeof( T ) );
		return (CreateItem( description.Name, itemId ) as T);
	}

	public static InventoryItem CreateItem( string className, ulong itemId = 0 )
	{
		if ( itemId > 0 && Items.TryGetValue( itemId, out var instance ) )
		{
			return instance;
		}

		if ( itemId == 0 )
		{
			itemId = ++NextItemId;
		}

		instance = TypeLibrary.Create<InventoryItem>( className );
		instance.ItemId = itemId;
		instance.IsValid = true;
		instance.StackSize = instance.DefaultStackSize;
		instance.ClassName = className;
		instance.OnCreated();

		Items[itemId] = instance;

		return instance;
	}

	public static void ClientDisconnected( Client client )
	{
		foreach ( var container in Containers.Values )
		{
			if ( container.IsConnected( client ) )
			{
				container.RemoveConnection( client );
			}
		}
	}

	public static void SendTransferInventoryEvent( InventoryContainer from, InventoryContainer to, ushort fromSlot )
	{
		using ( var stream = new MemoryStream() )
		{
			using ( var writer = new BinaryWriter( stream ) )
			{
				writer.Write( fromSlot );
				writer.Write( from.InventoryId );
				writer.Write( to.InventoryId );
				SendEventDataToServer( NetworkEvent.TransferInventory, Convert.ToBase64String( stream.ToArray() ) );
			}
		}
	}

	public static void SendSplitInventoryEvent( InventoryContainer from, InventoryContainer to, ushort fromSlot, ushort toSlot )
	{
		using ( var stream = new MemoryStream() )
		{
			using ( var writer = new BinaryWriter( stream ) )
			{
				writer.Write( fromSlot );
				writer.Write( from.InventoryId );
				writer.Write( toSlot );
				writer.Write( to.InventoryId );
				SendEventDataToServer( NetworkEvent.SplitInventory, Convert.ToBase64String( stream.ToArray() ) );
			}
		}
	}

	public static void SendMoveInventoryEvent( InventoryContainer from, InventoryContainer to, ushort fromSlot, ushort toSlot )
	{
		using ( var stream = new MemoryStream() )
		{
			using ( var writer = new BinaryWriter( stream ) )
			{
				writer.Write( fromSlot );
				writer.Write( from.InventoryId );
				writer.Write( toSlot );
				writer.Write( to.InventoryId );
				SendEventDataToServer( NetworkEvent.MoveInventory, Convert.ToBase64String( stream.ToArray() ) );
			}
		}
	}

	public static void SendGiveItemEvent( To to, InventoryContainer container, ushort slotId, InventoryItem instance )
	{
		using ( var stream = new MemoryStream() )
		{
			using ( var writer = new BinaryWriter( stream ) )
			{
				writer.Write( container.InventoryId );
				writer.WriteInventoryItem( instance );
				writer.Write( slotId );
				SendEventDataToClient( to, NetworkEvent.GiveItem, stream.ToArray() );
			}
		}
	}

	public static void SendTakeItemEvent( To to, InventoryContainer container, ushort slotId )
	{
		using ( var stream = new MemoryStream() )
		{
			using ( var writer = new BinaryWriter( stream ) )
			{
				writer.Write( container.InventoryId );
				writer.Write( slotId );
				SendEventDataToClient( to, NetworkEvent.TakeItem, stream.ToArray() );
			}
		}
	}

	public static void SendDirtyItemsEvent( To to, InventoryContainer container )
	{
		using ( var stream = new MemoryStream() )
		{
			using ( var writer = new BinaryWriter( stream ) )
			{
				writer.Write( container.InventoryId );

				ushort dirtyCount = 0;
				var itemList = container.ItemList;

				for ( var i = 0; i < itemList.Count; i++ )
				{
					var item = itemList[i];

					if ( item != null && item.IsDirty )
					{
						dirtyCount++;
					}
				}

				writer.Write( dirtyCount );

				for ( var i = 0; i < itemList.Count; i++ )
				{
					var item = itemList[i];

					if ( item != null && item.IsDirty )
					{
						writer.WriteInventoryItem( item );
						item.IsDirty = false;
					}
				}

				SendEventDataToClient( to, NetworkEvent.SendDirtyItems, stream.ToArray() );
			}
		}
	}

	private static void ProcessTakeItemEvent( BinaryReader reader )
	{
		var inventoryId = reader.ReadUInt64();
		var container = Find( inventoryId );
		container?.ProcessTakeItemEvent( reader );
	}

	private static void ProcessGiveItemEvent( BinaryReader reader )
	{
		var inventoryId = reader.ReadUInt64();
		var container = Find( inventoryId );
		container?.ProcessGiveItemEvent( reader );
	}

	private static void ProcessTransferInventoryEvent( BinaryReader reader )
	{
		var fromSlot = reader.ReadUInt16();
		var fromId = reader.ReadUInt64();
		var toId = reader.ReadUInt64();
		var fromInventory = Find( fromId );
		var toInventory = Find( toId );

		if ( fromInventory == null )
		{
			return;
		}

		if ( toInventory == null )
		{
			return;
		}

		if ( IsServer )
		{
			var item = fromInventory.GetFromSlot( fromSlot );

			if ( item.IsValid() )
			{
				var remaining = toInventory.Stack( item );

				if ( remaining > 0 )
				{
					item.StackSize = remaining;
					return;
				}

				fromInventory.ClearSlot( fromSlot );
			}
		}
	}

	private static void ProcessSplitInventoryEvent( BinaryReader reader )
	{
		var fromSlot = reader.ReadUInt16();
		var fromId = reader.ReadUInt64();
		var toSlot = reader.ReadUInt16();
		var toId = reader.ReadUInt64();
		var fromInventory = Find( fromId );
		var toInventory = Find( toId );

		if ( fromInventory == null )
		{
			return;
		}

		if ( toInventory == null )
		{
			return;
		}

		if ( IsServer )
		{
			fromInventory.Split( toInventory, fromSlot, toSlot );
		}
	}

	private static void ProcessMoveInventoryEvent( BinaryReader reader )
	{
		var fromSlot = reader.ReadUInt16();
		var fromId = reader.ReadUInt64();
		var toSlot = reader.ReadUInt16();
		var toId = reader.ReadUInt64();
		var fromInventory = Find( fromId );
		var toInventory = Find( toId );

		if ( fromInventory == null )
		{
			return;
		}

		if ( toInventory == null )
		{
			return;
		}

		if ( IsServer )
		{
			fromInventory.Move( toInventory, fromSlot, toSlot );
		}
	}

	private static void ProcessSendDirtyItemsEvent( BinaryReader reader )
	{
		var container = Find( reader.ReadUInt64() );
		if ( container == null ) return;

		var itemCount = reader.ReadUInt16();

		for ( var i = 0; i < itemCount; i++ )
		{
			var item = reader.ReadInventoryItem();

			if ( item != null )
			{
				container.InvokeDataChanged( item.SlotId );
			}
		}
	}

	[ConCmd.Server]
	public static void SendEventDataToServer( NetworkEvent type, string data )
	{
		var decoded = Convert.FromBase64String( data );

		using ( var stream = new MemoryStream( decoded ) )
		{
			using ( var reader = new BinaryReader( stream ) )
			{
				switch ( type )
				{
					case NetworkEvent.TransferInventory:
						ProcessTransferInventoryEvent( reader );
						break;
					case NetworkEvent.SplitInventory:
						ProcessSplitInventoryEvent( reader );
						break;
					case NetworkEvent.MoveInventory:
						ProcessMoveInventoryEvent( reader );
						break;
				}
			}
		}
	}

	[ClientRpc]
	public static void SendEventDataToClient( NetworkEvent type, byte[] data )
	{
		using ( var stream = new MemoryStream( data ) )
		{
			using ( var reader = new BinaryReader( stream ) )
			{
				switch ( type )
				{
					case NetworkEvent.SendDirtyItems:
						ProcessSendDirtyItemsEvent( reader );
						break;
					case NetworkEvent.MoveInventory:
						ProcessMoveInventoryEvent( reader );
						break;
					case NetworkEvent.GiveItem:
						ProcessGiveItemEvent( reader );
						break;
					case NetworkEvent.TakeItem:
						ProcessTakeItemEvent( reader );
						break;
				}
			}
		}
	}

	[Event.Tick.Server]
	private static void ServerTick()
	{
		for ( var i = DirtyList.Count - 1; i >= 0; i-- )
		{
			var container = DirtyList[i];
			container.SendDirtyItems();
			container.IsDirty = false;
		}

		DirtyList.Clear();
	}

	[Event.Tick]
	private static void CheckOrphanedItems()
	{
		foreach ( var kv in Items )
		{
			var item = kv.Value;

			if ( !item.Container.IsValid() && !item.WorldEntity.IsValid() )
			{
				OrphanedItems.Enqueue( kv.Key );
				item.IsValid = false;
			}
		}

		var totalOrphanedItems = 0;

		while ( OrphanedItems.Count > 0 )
		{
			var itemId = OrphanedItems.Dequeue();
			Items.Remove( itemId );
			totalOrphanedItems++;
		}
	}
}