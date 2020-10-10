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

		public byte GetByte()
		{
			return (byte)
				( (LcdEnabled ? (1 << 7) : 0)
				| (WindowTileMapSelect ? (1 << 6) : 0)
				| (WindowEnabled ? (1 << 5) : 0)
				| (TileDataSelect ? (1 << 4) : 0)
				| (BgTileMapSelect ? (1 << 3) : 0)
				| (SpriteSize ? (1 << 2) : 0)
				| (SpritesEnabled ? (1 << 1) : 0)
				| (BgEnabled ? 1 : 0));
		}

		public void SetByte(byte value)
		{
			LcdEnabled = (value & 0x80) != 0;
			WindowTileMapSelect = (value & 0x40) != 0;
			WindowEnabled = (value & 0x20) != 0;
			TileDataSelect = (value & 0x10) != 0;
			BgTileMapSelect = (value & 0x08) != 0;
			SpriteSize = (value & 0x04) != 0;
			SpritesEnabled = (value & 0x02) != 0;
			BgEnabled = (value & 0x01) != 0;
		}
	}
	
}
