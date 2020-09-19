using System.Runtime.CompilerServices;

namespace SpecBoy
{
	class Timers
	{
		// 00: 4096 = 1024
		// 01: 262144 = 16
		// 10: 65536 = 64
		// 11: 16384 = 256

		// Counters
		private ushort divCounter;
		private ushort timaCounter;

		private byte div;

		private readonly int[] modulos = new int[] { 1024, 16, 64, 256 };

		public Timers()
		{
			Div = 0;
			Tima = 0;
			Tma = 0;
			Tac = 0;
		}

		// Registers
		public byte Div { get => (byte)((divCounter >> 8) & 0xff); set => div = value; }

		public byte Tima { get; set; }

		public byte Tma { get; set; }

		public byte Tac { get; set; }

		public bool TimaIRQReq { get; set; }

		public void Update()
		{
			if (Utility.IsBitSet(Tac, 2))
			{
				int modulo = modulos[Tac & 3];

				timaCounter += 4;

				while (timaCounter >= modulo)
				{
					timaCounter -= (ushort)modulo;

					if (Tima == 0xff)
					{
						Tima = Tma;
						TimaIRQReq = true;
					}
					else
					{
						Tima++;
					}
				}
			}

			divCounter += 4;
			
			//Div += (byte)(divCounter >> 8);
			//divCounter -= (ushort)(divCounter & 0x100);
		}
	}
}
