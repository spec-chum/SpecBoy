namespace SpecBoy
{
	partial class Ppu
	{
		private struct StatReg
		{
			public bool lyCompareInt;
			public bool oamInt;
			public bool vBlankInt;
			public bool hBlankInt;
			public bool lyCompareFlag;
			public Mode currentMode;

			public byte GetByte()
			{
				return (byte)(0x80
				| lyCompareInt.ToIntPower(6)
				| oamInt.ToIntPower(5)
				| vBlankInt.ToIntPower(4)
				| hBlankInt.ToIntPower(3)
				| lyCompareFlag.ToIntPower(2)
				| (int)currentMode);
			}

			public void SetByte(byte value)
			{
				// Bits 0 to 2 are read only
				lyCompareInt = value.IsBitSet(6);
				oamInt = value.IsBitSet(5);
				vBlankInt = value.IsBitSet(4);
				hBlankInt = value.IsBitSet(3);
			}
		}
	}
}
