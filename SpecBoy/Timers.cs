namespace SpecBoy
{
	class Timers
	{
		public const ushort TimerIrqVector = 0x50;
		public const int TimerIeBit = 2;

		private ushort divCounter;

		private bool lastResult = false;

		public Timers()
		{
			Div = 0;
			Tima = 0;
			Tma = 0;
			Tac = 0;
			TimaIrqReq = false;
			ReloadTima = false;
		}

		// Registers

		// Writing to Div always sets it to 0
		public byte Div { get => (byte)((divCounter >> 8) & 0xff); set => divCounter = 0; }

		public byte Tima { get; set; }

		public byte Tma { get; set; }

		public byte Tac { get; set; }

		public bool TimaIrqReq { get; set; }

		public bool ReloadTima { get; set; }

		public byte OldTima { get; set; }

		public void Update()
		{
			if (ReloadTima && OldTima == Tima)
			{
				ReloadTima = false;
				Tima = Tma;
				TimaIrqReq = true;
			}

			divCounter += 4;

			var divBit = (Tac & 0x03) switch
			{

				0 => 9,
				1 => 3,
				2 => 5,
				_ => 7,
			};

			bool result = ((Tac & (1 << 2)) != 0) & ((divCounter & (1 << divBit)) != 0);

			if (lastResult && !result)
			{
				Tima++;

				if (Tima == 0)
				{
					ReloadTima = true;
				}
			}

			OldTima = Tima;
			lastResult = result;
		}
	}
}
