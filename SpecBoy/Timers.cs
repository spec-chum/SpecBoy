namespace SpecBoy
{
	class Timers
	{
		private ushort divCounter;
		private byte oldTima;
		private bool lastResult;
		private bool reloadTima;

		private byte tima;
		private byte tma;
		private byte tac;

		// Registers

		// Writing to Div always sets it to 0
		public byte Div { get => (byte)((divCounter >> 8) & 0xff); set => divCounter = 0; }

		public byte Tima
		{
			get => tima;
			set
			{
				if (!BusConflict)
				{
					tima = value;
					reloadTima = false;
				}
			}
		}

		public byte Tma
		{
			get => tma;
			set
			{
				tma = value;

				if (BusConflict)
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
				var speed = (tac & 0x03) switch
				{
					0 => 1024,
					1 => 16,
					2 => 64,
					_ => 256,
				};

				if (((tac & (1 << 2)) != 0) && (value & 1 << 2) == 0)
				{
					if ((divCounter & (speed >> 1)) != 0)
					{
						System.Console.WriteLine(divCounter & (speed >> 1));
						tima++;

						if (tima == 0)
						{
							reloadTima = true;
						}
					}
				}

				if (((divCounter & (speed >> 1)) !=0) && ((divCounter & (speed >> 1)) == 0))
				{
					System.Console.WriteLine("In 2");
					tima++;

					if (tima == 0)
					{
						reloadTima = true;
					}
				}

				tac = value;
			}
		}

		public bool BusConflict { get; set; }

		public void Tick()
		{
			BusConflict = false;

			if (reloadTima)
			{
				reloadTima = false;
				BusConflict = true;

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
				if (divCounter != 0 && (divBit >> 1 != 0))
				{
					System.Console.WriteLine($"Spurious timer inc on tick {divCounter - 4}, tima: {tima}");
				}

				tima++;

				if (tima == 0)
				{
					System.Console.WriteLine($"Overflow triggered");

					reloadTima = true;
				}
			}

			oldTima = tima;
			lastResult = result;
		}
	}
}
