namespace SpecBoy;

struct LcdcReg
{
	public bool LcdEnabled;
	public bool WindowTileMapSelect;
	public bool WindowEnabled;
	public bool TileDataSelect;
	public bool BgTileMapSelect;
	public bool SpriteSize;
	public bool SpritesEnabled;
	public bool BgEnabled;

	public readonly byte GetByte()
	{
		return (byte)
			( LcdEnabled.ToBytePower(7)
			| WindowTileMapSelect.ToBytePower(6)
			| WindowEnabled.ToBytePower(5)
			| TileDataSelect.ToBytePower(4)
			| BgTileMapSelect.ToBytePower(3)
			| SpriteSize.ToBytePower(2)
			| SpritesEnabled.ToBytePower(1)
			| BgEnabled.ToBytePower(0));
	}

	public void SetByte(byte value)
	{
		LcdEnabled = value.IsBitSet(7);
		WindowTileMapSelect = value.IsBitSet(6);
		WindowEnabled = value.IsBitSet(5);
		TileDataSelect = value.IsBitSet(4);
		BgTileMapSelect = value.IsBitSet(3);
		SpriteSize = value.IsBitSet(2);
		SpritesEnabled = value.IsBitSet(1);
		BgEnabled = value.IsBitSet(0);
	}
}
