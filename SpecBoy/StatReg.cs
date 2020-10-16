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

		//private int currentCycle;
		private byte value;
		private byte ly;
		private byte lyc;
		private bool statIntRequest;

		public void SetLyForCompare(byte value, int currentCycle)
		{
			ly = value;
			CompareLy(currentCycle);
		}

		public void SetLycForCompare(byte value, int currentCycle)
		{
			lyc = value;
			CompareLy(currentCycle);
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

		public void CompareLy(int currentCycle)
		{
			// Compare Flag is always 0 on cycle 0, except on line 0
			if (ly != 0 && currentCycle == 0)
			{
				LyCompareFlag = false;
			}
			else
			{
				LyCompareFlag = ly == lyc;
			}

			RequestInterrupt(CurrentMode, LyCompareFlag);
		}

		public void RequestInterrupt(Mode mode, bool doCompareLy = true)
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
			if (doCompareLy)
			{
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
