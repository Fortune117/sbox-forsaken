﻿using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Facepunch.Forsaken;

public partial class ForsakenGame : GameManager
{
	public static ForsakenGame Entity => Current as ForsakenGame;

	[ConVar.Server( "fsk.autosave", Saved = true )]
	public static bool ShouldAutoSave { get; set; } = true;

	private TimeUntil NextDespawnItems { get; set; }
	private TimeUntil NextAutoSave { get; set; }
	private TopDownCamera Camera { get; set; }
	private bool HasLoadedWorld { get; set; }

	public ForsakenGame() : base()
	{

	}

	public override void LoadSavedGame( SavedGame save )
	{
		Log.Info( "[Forsaken] Loading world..." );

		using var s = new MemoryStream( save.Data );
		using var r = new BinaryReader( s );

		PersistenceSystem.LoadAll( r );
	}

	public override void Spawn()
	{
		InventorySystem.Initialize();
		base.Spawn();
	}

	public override void ClientSpawn()
	{
		InventorySystem.Initialize();

		ItemTag.Register( "deployable", "Deployable", ItemColors.Deployable );
		ItemTag.Register( "consumable", "Consumable", ItemColors.Consumable );
		ItemTag.Register( "tool", "Tool", ItemColors.Tool );

		Game.RootPanel?.Delete( true );
		Game.RootPanel = new UI.Hud();

		Camera = new();

		base.ClientSpawn();
	}

	public override void ClientJoined( IClient client )
	{
		InventorySystem.ClientJoined( client );

		var pawn = All.OfType<ForsakenPlayer>()
			.Where( p => p.SteamId == client.SteamId )
			.FirstOrDefault();

		if ( !pawn.IsValid() )
		{
			pawn = new ForsakenPlayer();
			pawn.MakePawnOf( client );
			pawn.Respawn();
		}
		else
		{
			pawn.MakePawnOf( client );
		}

		PersistenceSystem.Load( pawn );

		base.ClientJoined( client );
	}

	public override void MoveToSpawnpoint( Entity pawn )
	{
		if ( pawn is ForsakenPlayer player && player.Bedroll.IsValid() )
		{
			player.Position = player.Bedroll.Position + Vector3.Up * 10f;
			return;
		}

		base.MoveToSpawnpoint( pawn );
	}

	public override void ClientDisconnect( IClient client, NetworkDisconnectionReason reason )
	{
		if ( client.Pawn is ForsakenPlayer player )
		{
			PersistenceSystem.Save( player );
		}

		InventorySystem.ClientDisconnected( client );

		base.ClientDisconnect( client, reason );
	}

	public override bool CanHearPlayerVoice( IClient source, IClient receiver )
	{
		if ( !source.IsValid() || !receiver.IsValid() ) return false;

		var a = source.Pawn as ForsakenPlayer;
		var b = source.Pawn as ForsakenPlayer;

		if ( !a.IsValid() || !b.IsValid() ) return false;

		return a.Position.Distance( b.Position ) <= 3000f;
	}

	public override void OnVoicePlayed( IClient cl )
	{
		cl.Voice.WantsStereo = false;
		base.OnVoicePlayed( cl );
	}

	public override void PostLevelLoaded()
	{
		Game.WorldEntity.Tags.Add( "world" );

		{
			var spawner = new PickupSpawner();
			spawner.SetType<WoodPickup>();
			spawner.MaxPickups = 400;
			spawner.MinPickupsPerSpawn = 200;
			spawner.MaxPickupsPerSpawn = 300;
			spawner.Interval = 120f;
		}

		{
			var spawner = new PickupSpawner();
			spawner.SetType<StonePickup>();
			spawner.MaxPickups = 300;
			spawner.MinPickupsPerSpawn = 100;
			spawner.MaxPickupsPerSpawn = 200;
			spawner.Interval = 180f;
		}

		{
			var spawner = new PickupSpawner();
			spawner.SetType<MetalOrePickup>();
			spawner.MaxPickups = 200;
			spawner.MinPickupsPerSpawn = 100;
			spawner.MaxPickupsPerSpawn = 150;
			spawner.Interval = 300f;
		}

		{
			var spawner = new PickupSpawner();
			spawner.SetType<PlantFiberPickup>();
			spawner.MaxPickups = 250;
			spawner.MinPickupsPerSpawn = 150;
			spawner.MaxPickupsPerSpawn = 200;
			spawner.Interval = 120f;
		}

		NextDespawnItems = 30f;
		HasLoadedWorld = true;
		NextAutoSave = 60f;

		base.PostLevelLoaded();
	}

	[Event.Tick.Server]
	private void ServerTick()
	{
		if ( HasLoadedWorld && NextAutoSave && ShouldAutoSave )
		{
			Log.Info( "[Forsaken] Saving world..." );
			PersistenceSystem.SaveAll();
			NextAutoSave = 60f;
		}

		if ( HasLoadedWorld && NextDespawnItems )
		{
			var items = All.OfType<ItemEntity>();

			foreach ( var item in items )
			{
				if ( item.TimeSinceSpawned >= 1800f )
				{
					item.Delete();
				}
			}

			NextDespawnItems = 30f;
		}
	}

	[Event.Client.Frame]
	private void OnFrame()
	{
		Camera?.Update();
	}
}
