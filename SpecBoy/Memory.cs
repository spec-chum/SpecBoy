using System;

namespace SpecBoy
{
	class Memory
	{
		private readonly Timers timers;

		public Memory(Timers timers)
		{
			Mem = new byte[0x10000];
			this.timers = timers;
		}

		public byte[] Mem { get; set; }

		public byte IE { get; set; }

		public byte IF
		{
			get
			{
				// Use local var as more will be added later
				var num = (byte)(timers.TimaIRQReq ? (1 << 2) : 0);
				return num;
			}
			set
			{
				timers.TimaIRQReq = Utility.IsBitSet(value, 2);
			}
		}

		public byte ReadByte(int address)
		{
			return address switch
			{
				0xff04 => timers.Div,
				0xff05 => timers.Tima,
				0xff06 => timers.Tma,
				0xff07 => timers.Tac,
				0xff0f => IF,
				0xff44 => 0x90, // ppu.ly - return 0x90 for testing
				0xffff => IE,
				_ => Mem[address],
			};
		}

		public ushort ReadWord(int address)
		{
			return (ushort)(ReadByte(address) | (ReadByte(address + 1) << 8));
		}

		public void WriteByte(int address, byte value)
		{
			switch (address)
			{
				case 0xff04:
					timers.Div = 0;
					break;

				case 0xff05:
					timers.Tima = value;
					break;

				case 0xff06:
					timers.Tma = value;
					break;

				case 0xff07:
					timers.Tac = value;
					break;

				// Serial IO - print output for testing
				case 0xff01:
					Console.Write((char)value);
					break;

				// IF
				case 0xff0f:
					IF = value;
					break;

				case 0xffff:
					IE = value;
					break;

				default:
					Mem[address] = value;
					break;
			}
		}

		public void WriteWord(int address, ushort value)
		{
			WriteByte(address, (byte)value);
			WriteByte(address + 1, (byte)(value >> 8));
		}
	}
}
