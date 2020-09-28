namespace SpecBoy
{
	class Timers
	{
		private ushort divCounter;
		private byte oldTima;
		private bool lastResult;
		private bool reloadTima;

		public Timers()
		{
			Div = 0;
			Tima = 0;
			Tma = 0;
			Tac = 0;
			BusConflict = false;
		}

		// Registers

		// Writing to Div always sets it to 0
		public byte Div { get => (byte)((divCounter >> 8) & 0xff); set => divCounter = 0; }

		public byte Tima { get; set; }

		public byte Tma { get; set; }

		public byte Tac { get; set; }

		public bool BusConflict { get; set; }

		public void Tick()
		{
			BusConflict = false;

			if (reloadTima)
			{
				reloadTima = false;
				BusConflict = true;

				// Check Tima hasn't changed since last cycle which could cancel interrupt
				if (oldTima == Tima)
				{
					Tima = Tma;
					Interrupts.TimerIrqReq = true;
				}
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

			// Detect falling edge
			if (lastResult && !result)
			{
				Tima++;

				if (Tima == 0)
				{
					reloadTima = true;
				}
			}

			oldTima = Tima;
			lastResult = result;
		}
	}
}
