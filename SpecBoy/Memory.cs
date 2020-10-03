﻿using System;

namespace SpecBoy
{
	class Memory
	{
		private readonly Timers timers;
		private readonly Ppu ppu;
		private readonly Input joypad;
		private readonly Cartridge cartridge;

		private int dmaCycles;
		private ushort dmaSrcAddr;
		private byte dmaLastByteWritten;
		private byte[] wRam;
		private byte[] hRam;

		public Memory(Timers timers, Ppu ppu, Input joypad, Cartridge cartridge)
		{
			this.timers = timers;
			this.ppu = ppu;
			this.joypad = joypad;
			this.cartridge = cartridge;

			wRam = new byte[0x2000];
			hRam = new byte[0x7f];
		}

		public byte[] WRam { get => wRam; set => wRam = value; }

		public byte[] HRam { get => hRam; set => hRam = value; }

		// Called from OnCycleUpdate in CPU
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
				var n when n <= 0x9fff => ppu.VRam[address & 0x1fff],

				// External RAM
				var n when n <= 0xbfff => cartridge.ReadByte(address),

				// RAM and mirrors
				var n when n <= 0xfdff => wRam[address & 0x1fff],

				// OAM
				var n when n <= 0xfe9f => ppu.Oam[address & 0xff],

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
					0xff0f => (byte)(Interrupts.IF | 0xe0),

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

				var n when n <= 0xfffe => hRam[address & 0x7f],

				0xffff => Interrupts.IE,

				_ => 0xff,
			};
		}

		public ushort ReadWord(int address) => (ushort)(ReadByte(address) | (ReadByte(address + 1) << 8));

		public void WriteByte(int address, byte value)
		{
			switch (address)
			{
				// ROM
				case var n when n <= 0x7fff:
					cartridge.WriteByte(address, value);
					break;

				// VRAM
				case var n when n <= 0x9fff:
					ppu.VRam[address & 0x1fff] = value;
					break;

				// External RAM
				case var n when n <= 0xbfff:
					cartridge.WriteByte(address, value);
					break;

				// RAM and mirrors
				case var n when n <= 0xfdff:
					wRam[address & 0x1fff] = value;
					break;

				// OAM
				case var n when n <= 0xfe9f:
					if (dmaCycles == 0 && dmaCycles <= 160)
					{
						ppu.Oam[address & 0xff] = value;
					}

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

				case 0xff4a:
					ppu.Wy = value;
					break;

				case 0xff4b:
					ppu.Wx = value;
					break;

				case var n when n >= 0xff80 && n <= 0xfffe:
					hRam[address & 0x7f] = value;
					break;

				case 0xffff:
					Interrupts.IE = value;
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
