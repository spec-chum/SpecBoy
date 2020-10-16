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

		private byte value;
		private byte ly;
		private byte lyc;
		private bool statIntRequest;

		public void SetLyForCompare(byte value, bool compareLy)
		{
			ly = value;
			RequestInterrupt(CurrentMode, compareLy);
		}

		public void SetLyc(byte value, bool compareLy)
		{
			lyc = value;
			RequestInterrupt(CurrentMode, compareLy);
		}

		public void ClearLyCompareFlag()
		{
			LyCompareFlag = false;
		}

		public byte GetByte()
		{
			RequestInterrupt(CurrentMode, false);

			value =  (byte)(0x80
				| LyCompareInt.ToIntPower(6)
				| OamInt.ToIntPower(5)
				| VBlankInt.ToIntPower(4)
				| HBlankInt.ToIntPower(3)
				| LyCompareFlag.ToIntPower(2)
				| (int)CurrentMode);

			return value;
		}

		public void SetByte(byte value)
		{
			LyCompareInt = value.IsBitSet(6);
			OamInt = value.IsBitSet(5);
			VBlankInt = value.IsBitSet(4);
			HBlankInt = value.IsBitSet(3);

			RequestInterrupt(CurrentMode, false);
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
