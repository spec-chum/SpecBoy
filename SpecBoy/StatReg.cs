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

		public bool lcdEnabled;

		private byte ly;
		private byte lyc;

		private bool statIntRequest;

		public byte GetLy()
		{
			return ly;
		}

		public void SetLy(byte value, bool compareLy)
		{
			ly = value;
			RequestInterrupt(CurrentMode, compareLy);
		}

		public byte GetLyc()
		{
			return lyc;
		}

		public void SetLyc(byte value, bool compareLy)
		{
			lyc = value;
			RequestInterrupt(CurrentMode, compareLy);
		}

		public byte GetByte()
		{
			RequestInterrupt(CurrentMode);

			return (byte)(0x80
			| (LyCompareInt ? (1 << 6) : 0)
			| (OamInt ? (1 << 5) : 0)
			| (VBlankInt ? (1 << 4) : 0)
			| (HBlankInt ? (1 << 3) : 0)
			| (LyCompareFlag ? (1 << 2) : 0)
			| ((int)CurrentMode & 3));
		}

		public void SetByte(byte value)
		{
			// Bits 0 to 2 are read only
			LyCompareInt = (value & 0x40) != 0;
			OamInt = (value & 0x20) != 0;
			VBlankInt = (value & 0x10) != 0;
			HBlankInt = (value & 0x08) != 0;

			RequestInterrupt(CurrentMode);
		}

		public void RequestInterrupt(Mode mode, bool compareLy = true)
		{
			if (!lcdEnabled)
			{
				return;
			}
			
			bool oldIntRequest = statIntRequest;

			statIntRequest = mode switch
			{
				Mode.HBlank => HBlankInt,
				Mode.VBlank => VBlankInt,
				Mode.OAM => OamInt,
				_ => false,
			};

			// Test for Ly == Lyc if requested
			if (compareLy)
			{
				LyCompareFlag = ly == lyc;
				if (LyCompareInt && LyCompareFlag)
				{
					statIntRequest = true;
				}
			}

			// Only fire on rising edge (STAT IRQ blocking)
			if (statIntRequest && !oldIntRequest)
			{
				Interrupts.StatIrqReq = true;
			}
		}
	}
}
