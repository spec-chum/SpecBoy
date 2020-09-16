using System;
using System.Collections.Generic;
using System.Text;

namespace SpecBoy
{
	class Memory
	{
		public Memory()
		{
			Mem = new byte[0x10000];
		}

		public byte[] Mem { get; set; }

		public byte ReadByte(int address)
		{
			// HACK for testing
			if (address == 0xff44)
			{
				return 0x90;
			}

			return Mem[address];
		}

		public ushort ReadWord(int address)
		{
			return (ushort)((Mem[address + 1] << 8) + Mem[address]);
		}

		public void WriteByte(int address, byte value)
		{
			Mem[address] = value;

			if (address == 0xff01)
			{
				Console.Write((char)value);
			}
		}

		public void WriteWord(int address, ushort value)
		{
			Mem[address] = (byte)value;
			Mem[address + 1] = (byte)(value >> 8);
		}
	}
}
