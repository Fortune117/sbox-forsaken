﻿using Sandbox;

namespace Facepunch.Forsaken;

public interface IPurchasableItem : IValid
{
	public int StockStackSize { get; }
	public float StockChance { get; }
	public bool IsPurchasable { get; }
	public int SalvageCost { get; }
	public string UniqueId { get; }
}
