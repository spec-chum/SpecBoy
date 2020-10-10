namespace SpecBoy
{
	struct StatReg
	{
		public bool LyCompareInt;
		public bool OamInt;
		public bool VBlankInt;
		public bool HBlankInt;
		public bool LyCompareFlag;
		public Mode CurrentMode;

		public byte GetByte()
		{
			return (byte)(0x80
			| (LyCompareInt ? (1 << 6) : 0)
			| (OamInt ? (1 << 5) : 0)
			| (VBlankInt ? (1 << 4) : 0)
			| (HBlankInt ? (1 << 3) : 0)
			| (LyCompareFlag ? (1 << 2) : 0)
			| (int)CurrentMode);
		}

		public void SetByte(byte value)
		{
			// Bits 0 to 2 are read only
			LyCompareInt = (value & 0x40) != 0;
			OamInt = (value & 0x20) != 0;
			VBlankInt = (value & 0x10) != 0;
			HBlankInt = (value & 0x08) != 0;
		}
	}
}
