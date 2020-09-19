using System.Runtime.CompilerServices;

namespace SpecBoy
{
	class Timers
	{
		public const ushort IRQVector = 0x50;

		private readonly ushort[] modulos = new ushort[] { 1024, 16, 64, 256 };
		private ushort divCounter;
		private ushort timaCounter;
		private byte div;

		public Timers()
		{
			Div = 0;
			Tima = 0;
			Tma = 0;
			Tac = 0;
			TimaIRQReq = false;
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
				ushort modulo = modulos[Tac & 0x03];

				divCounter += 4;
				timaCounter += 4;

				if (timaCounter == modulo)
				{
					timaCounter = 0;
					Tima++;

					if (Tima == 0)
					{
						Tima = Tma;
						TimaIRQReq = true;
					}
				}
			}
		}
	}
}
