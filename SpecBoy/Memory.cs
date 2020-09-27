using System;
using System.Text;

namespace SpecBoy
{
	class Memory
	{

		private readonly Timers timers;
		private readonly Ppu ppu;
		private readonly Input joypad;

		private int dmaCycles;
		private ushort dmaSrcAddr;
		private byte dmaLastByteWritten;
		private byte interruptFlag;

		public Memory(Timers timers, Ppu ppu, Input joypad)
		{
			this.timers = timers;
			this.ppu = ppu;
			this.joypad = joypad;

			Rom = new byte[0x8000];
			WRam = new byte[0x2000];
			HRam = new byte[0x7f];
		}
		
		public byte[] Rom { get; set; }

		public byte[] WRam { get; set; }

		public byte[] HRam { get; set; }

		public byte IE { get; set; }

		public byte IF
		{
			get
			{
				byte num;
				num  = (byte)(Interrupts.VBlankIrqReq ? (1 << 0) : 0);
				num |= (byte)(Interrupts.StatIrqReq ? (1 << 1) : 0);
				num |= (byte)(Interrupts.TimerIrqReq ? (1 << 2) : 0);
				num |= (byte)(Interrupts.SerialIrqReq ? (1 << 3) : 0);
				num |= (byte)(Interrupts.JoypadIrqReq ? (1 << 4) : 0);

				interruptFlag |= (byte)(num | 0xe0);

				return interruptFlag;
			}
			set
			{
				Interrupts.VBlankIrqReq = Utility.IsBitSet(value, 0);
				Interrupts.StatIrqReq = Utility.IsBitSet(value, 1);
				Interrupts.TimerIrqReq = Utility.IsBitSet(value, 2);
				Interrupts.SerialIrqReq = Utility.IsBitSet(value, 3);
				Interrupts.JoypadIrqReq = Utility.IsBitSet(value, 4);

				interruptFlag = value;
			}
		}

		// Called from OnCycleUpdate in CPU
		public void DoDma()
		{
			// Check if any DMA in progress
			if (dmaCycles == 0)
			{
				return;
			}

			byte index = (byte)(160 - dmaCycles);

			// Account for the 2 cycle delay (do nothing for 2 cycles)
			if (dmaCycles <= 160)
			{
				dmaLastByteWritten = ReadByte(dmaSrcAddr + index, true);
				ppu.Oam[index] = dmaLastByteWritten;
			}

			dmaCycles--;
		}

		public byte ReadByte(int address, bool bypass = false)
		{
			// External bus: $0000-$7FFF, $A000-$FDFF
			// Video ram bus: $8000-$9FFF

			if (!bypass && dmaCycles != 0 && address < 0xff00)
			{
				// Test if in OAM
				if (address >= 0xfe00)
				{
					return 0xff;
				}

				// Test if same bus
				if (busType(address) == busType(dmaSrcAddr))
				{
					return dmaLastByteWritten;
				}

				static int busType(int addr) => addr switch
				{
					var n when n <= 0x7fff || (n >= 0xa000 && n <= 0xfdff) => 0,
					var n when n >= 0x8000 && n <= 0x9fff => 1,
					_ => 2,
				};
			}

			return address switch
			{
				// ROM
				var n when n >= 0x0000 && n <= 0x7fff => Rom[address],

				// VRAM
				var n when n >= 0x8000 && n <= 0x9fff => ppu.VRam[address & 0x1fff],

				// RAM and mirrors
				var n when n >= 0xc000 && n <= 0xfdff => WRam[address & 0x1fff],

				// OAM
				var n when n >= 0xfe00 && n <= 0xfe9f => ppu.Oam[address & 0xff],

				// IO Registers
				var n when n >= 0xff00 && n <= 0xff7f => address switch
				{
					// JOYPAD
					0xff00 => joypad.Joypad,

					// TIMERS
					0xff04 => timers.Div,
					0xff05 => timers.Tima,
					0xff06 => timers.Tma,
					0xff07 => (byte)(timers.Tac | 0xf8),

					// IF
					0xff0f => IF,

					// PPU
					0xff40 => ppu.Lcdc,
					0xff41 => (byte)(ppu.Stat | 0x80),
					0xff42 => ppu.Scy,
					0xff43 => ppu.Scx,					
					0xff44 => ppu.Ly,
					0xff45 => ppu.Lyc,
					0xff46 => (byte)(dmaSrcAddr >> 8),
					0xff47 => ppu.Bgp,
					0xff48 => ppu.Obp0,
					0xff49 => ppu.Obp1,
					0xff4a => ppu.Wy,
					0xff4b => ppu.Wx,

					_ => 0xff,
				},

				var n when n >= 0xff80 && n <= 0xfffe => HRam[address & 0x7f],

				0xffff => IE,

				_ => 0xff,
			};
		}

		public ushort ReadWord(int address) => (ushort)(ReadByte(address) | (ReadByte(address + 1) << 8));

		public void WriteByte(int address, byte value)
		{			
			switch (address)
			{
				// Can't write to ROM (yet)
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

				// OAM
				case var n when n >= 0xfe00 && n <= 0xfe9f:
					ppu.Oam[address & 0xff] = value;
					break;

				// Joypad
				case 0xff00:
					joypad.Joypad = value;
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
					//Console.Write((char)value);
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

				case 0xff45:
					ppu.Lyc = value;
					break;

				// OAM DMA
				case 0xff46:
					dmaSrcAddr = (ushort)(value << 8);

					// Account for delay (160 for DMA and 2 delay cycles)
					dmaCycles = 162;
					break;

				case 0xff47:
					ppu.Bgp = value;
					break;

				case 0xff48:
					ppu.Obp0 = value;
					break;

				case 0xff49:
					ppu.Obp1 = value;
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
