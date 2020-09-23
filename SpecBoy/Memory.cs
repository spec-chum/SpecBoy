using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;

namespace SpecBoy
{
	class Memory
	{
		private readonly Timers timers;
		private readonly Ppu ppu;

		private byte interruptFlag;

		public Memory(Timers timers, Ppu ppu)
		{
			this.timers = timers;
			this.ppu = ppu;

			Rom = new byte[0x8000];
			WRam = new byte[0x2000];
			HRam = new byte[0x80];

			interruptFlag = 0xe0;
		}

		public byte[] Rom { get; set; }

		public byte[] WRam { get; set; }

		public byte[] HRam { get; set; }

		public byte IE { get; set; }

		public byte IF
		{
			get
			{
				var num = (byte)(ppu.VBlankIrqReq ? (1 << 0) : 0);
				num |= (byte)(ppu.StatIrqReq ? (1 << 1) : 0);
				num |= (byte)(timers.TimaIrqReq ? (1 << 2) : 0);

				interruptFlag |= num;

				return interruptFlag;
			}
			set
			{
				ppu.VBlankIrqReq = Utility.IsBitSet(value, 0);
				ppu.StatIrqReq = Utility.IsBitSet(value, 1);
				timers.TimaIrqReq = Utility.IsBitSet(value, 2);

				interruptFlag = (byte)(value | 0xe0);
			}
		}

		public byte ReadByte(int address)
		{
			return address switch
			{
				// ROM
				var n when n >= 0x0000 && n <= 0x7fff => Rom[address],

				// VRAM
				var n when n >= 0x8000 && n <= 0x9fff => ppu.VRam[address & 0x1fff],

				// RAM and mirrors
				var n when n >= 0xc000 && n <= 0xfdff => WRam[address & 0x1fff],

				// IO Registers
				var n when n >= 0xff00 && n <= 0xff7f => address switch
				{
					// TIMERS
					0xff04 => timers.Div,
					0xff05 => timers.Tima,
					0xff06 => timers.Tma,
					0xff07 => timers.Tac,

					// IF
					0xff0f => IF,

					// PPU
					0xff40 => ppu.Lcdc,
					0xff41 => ppu.Stat,
					0xff42 => ppu.Scy,
					0xff43 => ppu.Scx,					
					0xff44 => ppu.Ly,
					0xff45 => ppu.Lyc,
					0xff47 => ppu.Bgp,
					0xff4a => ppu.Wy,
					0xff4b => ppu.Wx,

					_ => 0xff,
				},

				var n when n >= 0xff80 && n <= 0xfffe => HRam[address & 0x7f],

				0xffff => IE,

				_ => 0xff,
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
				// Can't write to ROM
				case var n when n >= 0x0000 && n <= 0x7fff:
					break;

				// VRAM
				case var n when n >= 0x8000 && n <= 0x9fff:
					ppu.VRam[address & 0x1fff] = value;
					break;

				// RAM and mirrors
				case var n when n >= 0xc000 && n <= 0xfdff:
					WRam[address & 0x1fff] = value;
					break;

				// Timers
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

				case 0xff0f:
					IF = value;
					break;

				// PPU
				case 0xff40:
					ppu.Lcdc = value;
					break;

				case 0xff41:
					ppu.Stat = value;
					break;

				case 0xff42:
					ppu.Scy = value;
					break;

				case 0xff43:
					ppu.Scx = value;
					break;

				case 0xff44:
					ppu.Ly = value;
					break;

				case 0xff45:
					ppu.Lyc = value;
					break;

				case 0xff47:
					ppu.Bgp = value;
					break;

				case 0xff4a:
					ppu.Wy = value;
					break;

				case 0xff4b:
					ppu.Wx = value;
					break;

				case var n when n >= 0xff80 && n <= 0xfffe:
					HRam[address & 0x7f] = value;
					break;

				case 0xffff:
					IE = value;
					break;

				default:
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
