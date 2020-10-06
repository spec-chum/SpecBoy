namespace SpecBoy
{
	partial class Ppu
	{
		private struct LcdcReg
		{
			public bool lcdEnabled;
			public bool windowTileMapSelect;
			public bool windowEnabled;
			public bool tileDataSelect;
			public bool bgTileMapSelect;
			public bool spriteSize;
			public bool spritesEnabled;
			public bool bgEnabled;

			public byte GetByte()
			{
				return (byte)
					( (lcdEnabled ? (1 << 7) : 0)
					| (windowTileMapSelect ? (1 << 6) : 0)
					| (windowEnabled ? (1 << 5) : 0)
					| (tileDataSelect ? (1 << 4) : 0)
					| (bgTileMapSelect ? (1 << 3) : 0)
					| (spriteSize ? (1 << 2) : 0)
					| (spritesEnabled ? (1 << 1) : 0)
					| (bgEnabled ? 1 : 0));
			}

			public void SetByte(byte value)
			{
				lcdEnabled = (value & 0x80) != 0;
				windowTileMapSelect = (value & 0x40) != 0;
				windowEnabled = (value & 0x20) != 0;
				tileDataSelect = (value & 0x10) != 0;
				bgTileMapSelect = (value & 0x08) != 0;
				spriteSize = (value & 0x04) != 0;
				spritesEnabled = (value & 0x02) != 0;
				bgEnabled = (value & 0x01) != 0;
			}
		}
	}
}
