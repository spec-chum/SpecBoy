using System.IO;

namespace SpecBoy
{
	class Memory
	{
		private readonly Timers timers;
		private readonly Ppu ppu;
		private readonly Joypad joypad;
		private readonly Cartridge cartridge;

		private readonly byte[] bootRom;

		private int dmaCycles;
		private ushort dmaSrcAddr;
		private byte dmaLastByteWritten;

		public Memory(Timers timers, Ppu ppu, Joypad joypad, Cartridge cartridge)
		{
			this.timers = timers;
			this.ppu = ppu;
			this.joypad = joypad;
			this.cartridge = cartridge;

			Mem = new byte[0x10000];

			try
			{
				BootRomEnabled = true;
				bootRom = File.ReadAllBytes("DMG_ROM.bin");
			}
			catch (FileNotFoundException)
			{
				BootRomEnabled = false;
			}
		}

		public byte[] Mem { get; }

		public bool BootRomEnabled { get; private set; }

		// Called from CycleTick() in CPU
		public void DoDma()
		{
			// Check if any DMA in progress
			if (dmaCycles == 0)
			{
				return;
			}

			// Account for the 2 cycle delay (do nothing for 2 cycles)
			if (dmaCycles <= 160)
			{
				byte index = (byte)(160 - dmaCycles);
				dmaLastByteWritten = ReadByte(dmaSrcAddr + index, true);
				ppu.WriteOam(index, dmaLastByteWritten, true);
			}

			dmaCycles--;
		}

		public byte ReadByte(int address, bool bypass = false)
		{
			if (BootRomEnabled && address < 0x100)
			{
				return bootRom[address];
			}

			if (!bypass && dmaCycles != 0 && address < 0xff00)
			{
				// Block OAM only while DMA writing bytes
				if (dmaCycles <= 160 && address >= 0xfe00)
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
					// External bus: $0000-$7FFF, $A000-$FDFF
					// Video ram bus: $8000-$9FFF
					var n when n <= 0x7fff || (n >= 0xa000 && n <= 0xfdff) => 0,
					var n when n >= 0x8000 && n <= 0x9fff => 1,
					_ => 2,
				};
			}

			return address switch
			{
				// ROM
				var n when n <= 0x7fff => cartridge.ReadByte(address),

				// VRAM
				var n when n <= 0x9fff => ppu.ReadVRam(address & 0x1fff),

				// External RAM
				var n when n <= 0xbfff => cartridge.ReadByte(address),

				// Work RAM banks
				var n when n <= 0xdfff => Mem[address],

				// Work RAM mirrors
				var n when n <= 0xfdff => Mem[address - 0x2000],

				// OAM
				var n when n <= 0xfe9f => ppu.ReadOam(address & 0xff),

				// Not usable, but returns 0 not 0xff
				var n when n <= 0xfeff => 0,

				// IO Registers
				0xff00 => joypad.JoyP,

				// Serial Transfer Control
				0xff02 => (byte)(Mem[address] | 0x7e),

				// Unused
				0xff03 => 0xff,

				// Timers
				0xff04 => timers.Div,
				0xff05 => timers.Tima,
				0xff06 => timers.Tma,
				0xff07 => (byte)(timers.Tac | 0xf8),

				// Unused
				var n when n <= 0xff0e => 0xff,

				0xff0f => (byte)(Interrupts.IF | 0xe0),

				// NR10
				0xff10 => (byte)(Mem[address] | 0x80),
				0xff11 => 0xff,
				// Unused
				0xff15 => 0xff,
				// NR30
				0xff1a => (byte)(Mem[address] | 0x7f),
				// NR32
				0xff1c => (byte)(Mem[address] | 0x9f),
				// Unused
				0xff1f => 0xff,
				// NR41
				0xff20 => (byte)(Mem[address] | 0xc0),
				// NR44
				0xff23 => (byte)(Mem[address] | 0x3f),
				// NR52
				0xff26 => (byte)(Mem[address] | 0x70),

				// Unused
				0xff27 => 0xff,
				0xff28 => 0xff,
				0xff29 => 0xff,

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

				// Unused
				var n when n >= 0xff4c && n <= 0xff7f => 0xff,

				// HRAM
				var n when n <= 0xfffe => Mem[address],

				0xffff => Interrupts.IE,

				_ => Mem[address]
			};
		}

		public ushort ReadWord(int address) => (ushort)(ReadByte(address) | (ReadByte(address + 1) << 8));

		public void WriteByte(int address, byte value)
		{
			switch (address)
			{
				// ROM
				case var n when n <= 0x7fff:
					if (!BootRomEnabled)
					{
						cartridge.WriteByte(address, value);
					}
					break;

				// VRAM
				case var n when n <= 0x9fff:
					ppu.WriteVRam(address & 0x1fff, value);
					break;

				// External RAM
				case var n when n <= 0xbfff:
					cartridge.WriteByte(address, value);
					break;

				// Work RAM
				case var n when n <= 0xdfff:
					Mem[address] = value;
					break;

				// Work RAM mirrors
				case var n when n <= 0xfdff:
					Mem[address - 0x2000] = value;
					break;

				// OAM
				case var n when n <= 0xfe9f:
					if (dmaCycles == 0 && dmaCycles <= 160)
					{
						ppu.WriteOam(address & 0xff, value);
					}

					break;

				case 0xff00:
					joypad.JoyP = value;
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
					Interrupts.IF = value;
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
					// Handle restart - ignore request until previous DMA actually starts
					if (dmaCycles >= 160)
					{
						break;
					}

					// Account for delay (160 for DMA and 2 delay cycles)
					dmaSrcAddr = (ushort)(value << 8);
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

				// Boot ROM
				case 0xff50:
					BootRomEnabled = false;
					break;

				case 0xff4a:
					ppu.Wy = value;
					break;
				case 0xff4b:
					ppu.Wx = value;
					break;

				// HRAM
				case var n when n >= 0xff80 && n <= 0xfffe:
					Mem[address] = value;
					break;

				case 0xffff:
					Interrupts.IE = value;
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
