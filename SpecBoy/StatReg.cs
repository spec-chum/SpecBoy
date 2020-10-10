﻿namespace SpecBoy
{
	partial class Ppu
	{
		private struct StatReg
		{
			public bool coincidenceInt;
			public bool oamInt;
			public bool vBlankInt;
			public bool hBlankInt;
			public bool coincidenceFlag;
			public Mode currentMode;

			public byte GetByte()
			{
				return (byte)(0x80
				| (coincidenceInt ? (1 << 6) : 0)
				| (oamInt ? (1 << 5) : 0)
				| (vBlankInt ? (1 << 4) : 0)
				| (hBlankInt ? (1 << 3) : 0)
				| (coincidenceFlag ? (1 << 2) : 0)
				| (int)currentMode);
			}

			public void SetByte(byte value)
			{
				// Bits 0 to 2 are read only
				coincidenceInt = (value & 0x40) != 0;
				oamInt = (value & 0x20) != 0;
				vBlankInt = (value & 0x10) != 0;
				hBlankInt = (value & 0x08) != 0;
			}
		}
	}
}