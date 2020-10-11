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
					( lcdEnabled.ToIntPower(7)
					| windowTileMapSelect.ToIntPower(6)
					| windowEnabled.ToIntPower(5)
					| tileDataSelect.ToIntPower(4)
					| bgTileMapSelect.ToIntPower(3)
					| spriteSize.ToIntPower(2)
					| spritesEnabled.ToIntPower(1)
					| bgEnabled.ToInt());
			}

			public void SetByte(byte value)
			{
				lcdEnabled = value.IsBitSet(7);
				windowTileMapSelect = value.IsBitSet(6);
				windowEnabled = value.IsBitSet(5);
				tileDataSelect = value.IsBitSet(4);
				bgTileMapSelect = value.IsBitSet(3);
				spriteSize = value.IsBitSet(2);
				spritesEnabled = value.IsBitSet(1);
				bgEnabled = value.IsBitSet(0);
			}
		}
	}
}
