﻿using Sandbox;

namespace Facepunch.Forsaken;

[GameResource( "Weapon", "weapon", "A weapon for use with Forsaken.", Icon = "crisis_alert" )]
public class WeaponResource : ItemResource
{
	[Property]
	public int WorldModelMaterialGroup { get; set; }

	[Property]
	public int ViewModelMaterialGroup { get; set; }

	[Property, ResourceType( "vmdl" )]
	public string WorldModelPath { get; set; }

	[Property]
	public AmmoType AmmoType { get; set; } = AmmoType.None;

	[Property]
	public int DefaultAmmo { get; set; } = 0;

	[Property]
	public int ClipSize { get; set; } = 0;

	[Property]
	public int Damage { get; set; } = 0;

	[Property]
	public string ClassName { get; set; }
}
