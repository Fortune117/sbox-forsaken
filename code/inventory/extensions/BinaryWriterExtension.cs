﻿using Sandbox;
using System;
using System.IO;

namespace Facepunch.Forsaken;

public static partial class BinaryWriterExtension
{
	public static void Write( this BinaryWriter self, InventoryItem item )
	{
		if ( item != null )
		{
			self.Write( item.UniqueId );
			self.Write( item.StackSize );
			self.Write( item.ItemId );
			self.Write( item.SlotId );

			item.Write( self );
		}
		else
		{
			self.Write( string.Empty );
		}
	}

	public static void Write( this BinaryWriter self, Action<BinaryWriter> wrapper )
	{
		using ( var stream = new MemoryStream() )
		{
			using ( var writer = new BinaryWriter( stream ) )
			{
				wrapper( writer );
			}

			var data = stream.ToArray();

			self.Write( data.Length );
			self.Write( data );
		}
	}

	public static void Write( this BinaryWriter self, InventoryContainer container )
	{
		var typeDesc = TypeLibrary.GetType( container.GetType() );

		self.Write( typeDesc.Name );
		self.Write( container.ParentId );
		self.Write( container.ContainerId );
		self.Write( container.SlotLimit );

		if ( container.Entity.IsValid() )
			self.Write( container.Entity );
		else
			self.Write( -1 );

		for ( var i = 0; i < container.SlotLimit; i++ )
		{
			var instance = container.ItemList[i];

			if ( instance != null )
			{
				self.Write( true );
				self.Write( instance );
			}
			else
			{
				self.Write( false );
			}
		}

		container.Serialize( self );
	}
}
