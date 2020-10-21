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

		public void SetLyForCompare(byte value)
		{
			ly = value;
			CompareLy();
		}

		public void SetLycForCompare(byte value)
		{
			lyc = value;
			CompareLy();
		}

		public byte GetByte()
		{
			RequestInterrupt(CurrentMode);

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

			RequestInterrupt(CurrentMode);
		}

		public void CompareLy()
		{
			if (!lcdEnabled)
			{
				return;
			}

			LyCompareFlag = ly == lyc;

			RequestInterrupt(CurrentMode);
		}

		public void RequestInterrupt(Mode mode)
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

			if (LyCompareInt && LyCompareFlag)
			{
				statIntRequest = true;
			}

			// Only fire on rising edge (STAT IRQ blocking)
			if (statIntRequest && !oldIntRequest)
			{
				Interrupts.StatIrqReq = true;
			}
		}
	}
}
