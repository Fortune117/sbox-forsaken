﻿using Sandbox;
using System.Collections.Generic;

namespace Facepunch.Forsaken;

[GameResource( "Armor", "armor", "A piece of armor or clothing for use with Forsaken.", Icon = "checkroom" )]
[ItemClass( typeof( ArmorItem ) )]
public class ArmorResource : ForsakenItemResource
{
	[Property, Description( "The percentage of damage protection this armor provides." )]
	public float DamageProtection { get; set; } = 5f;

	[Property]
	public HashSet<string> DamageTags { get; set; }

	[Property]
	public string DamageHitbox { get; set; } = string.Empty;

	[Property]
	public ArmorSlot ArmorSlot { get; set; } = ArmorSlot.None;

	[Property, ResourceType( "vmdl" )]
	public string SecondaryModel { get; set; }

	[Property, ResourceType( "vmdl" )]
	public string PrimaryModel { get; set; }

	[Property]
	public float TemperatureModifier { get; set; }
}
