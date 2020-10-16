namespace SpecBoy
{
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

		private byte value;

		public byte GetByte()
		{
			value =  (byte)
				( LcdEnabled.ToIntPower(7)
				| WindowTileMapSelect.ToIntPower(6)
				| WindowEnabled.ToIntPower(5)
				| TileDataSelect.ToIntPower(4)
				| BgTileMapSelect.ToIntPower(3)
				| SpriteSize.ToIntPower(2)
				| SpritesEnabled.ToIntPower(1)
				| BgEnabled.ToInt());

			return value;
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
}
