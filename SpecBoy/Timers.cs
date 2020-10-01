namespace SpecBoy
{
	class Timers
	{
		private ushort divCounter;
		private bool lastResult;
		private bool reloadTima;
		private bool busConflict;

		// Registers
		private byte tima;
		private byte tma;
		private byte tac;

		public byte Div
		{
			get => (byte)((divCounter >> 8) & 0xff);

			set
			{
				// Writing to Div always sets it to 0
				divCounter = 0;

				// Can't have falling edge if last result was 0
				if (!lastResult)
				{
					return;
				}

				// If timer enabled we have a falling edge
				if ((tac & (1 << 2)) != 0)
				{
					tima++;

					if (tima == 0)
					{
						reloadTima = true;
					}
				}

				lastResult = false;
			}
		}

		public byte Tima
		{
			get => tima;
			set
			{
				// Block write if TIMA is being reloaded
				if (busConflict)
				{
					return;
				}
				
				// Writing to TIMA can cancel IRQ request, do that here
				tima = value;
				reloadTima = false;
			}
		}

		public byte Tma
		{
			get => tma;
			set
			{
				tma = value;

				// TIMA also updated if it's being reloaded when TMA written to
				if (busConflict)
				{
					tima = tma;
				}
			}
		}

		public byte Tac 
		{
			get => tac; 
			set
			{
				tac = value;

				// Can't have falling edge if last result was 0
				if (!lastResult)
				{
					return;
				}

				// If timer now disabled we have a falling edge
				if ((tac & (1 << 2)) == 0)
				{
					tima++;

					if (tima == 0)
					{
						reloadTima = true;
					}
				}

				lastResult = false;
			}
		}

		public void Tick()
		{
			busConflict = false;

			if (reloadTima)
			{
				reloadTima = false;
				busConflict = true;

				tima = tma;
				Interrupts.TimerIrqReq = true;
			}

			divCounter += 4;

			var divBit = (tac & 0x03) switch
			{
				0 => 9,
				1 => 3,
				2 => 5,
				_ => 7,
			};

			bool result = ((tac & (1 << 2)) != 0) && ((divCounter & (1 << divBit)) != 0);

			// Detect falling edge
			if (lastResult && !result)
			{
				tima++;

				if (tima == 0)
				{
					reloadTima = true;
				}
			}

			lastResult = result;
		}
	}
}
