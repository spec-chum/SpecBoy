using System;
using System.IO;

namespace SpecBoy
{
	class Cartridge
	{
		public byte[] rom;
		public byte[] romBank;
		public byte[] ram;

		private int RomBank;
		private int RamBank;
		private int numBanks;

		public enum CartType : byte
		{
			RomOnly = 0x00,
			Mbc1 = 0x01,
			Mbc1Ram = 0x02,
			Mbc1RamBattery = 0x03,
			Mbc2 = 0x05,
			Mbc2Battery = 0x06,
			RomRam = 0x08,
			RomRamBattery = 0x09,
			Mmm01 = 0x0b,
			Mmm01Ram = 0x0c,
			Mmm01RamBattery = 0x0d,
			Mbc3TimerBattery = 0x0f,
			Mbc3TimerRamBattery = 0x10,
			Mbc3 = 0x11,
			Mbc3Ram = 0x12,
			Mbc3RamBattery = 0x13,
			Mbc4 = 0x15,
			Mbc4Ram = 0x16,
			Mbc4RamBattery = 0x17,
			Mbc5 = 0x19,
			Mbc5Ram = 0x1a,
			Mbc5RamBattery = 0x1b,
			Mbc5Rumble = 0x1c,
			Mbc5RumbleRam = 0x1d,
			Mbc5RumbleRamBattery = 0x1e,
			PocketCamera = 0xfc,
			BandaiTama5 = 0xfd,
			HuC3 = 0xfe,
			HuC1RamBattery = 0xff
		}

		public Cartridge()
		{
			romBank = new byte[0x4000];
			ram = new byte[0x8000];
		}

		public byte ReadByte(int address)
		{
			return address switch
			{
				// ROM Bank0
				var n when n >= 0x0000 && n <= 0x3fff => rom[address],

				// ROM Bank n
				var n when n >= 0x4000 && n <= 0x7fff => rom[RomBank + (address & 0x3fff)],

				// Cartridge RAM
				var n when n >= 0xa000 && n <= 0xbfff => ram[RamBank + (address & 0x1fff)],

				_ => 0xff,
			};
		}

		public void WriteByte(int address, byte value)
		{
			switch (address)
			{
				// ROM Bank n
				case var n when n >= 0x2000 && n <= 0x3fff:
					RomBank = value & 0x7f;

					if (RomBank == 0)
					{
						RomBank = 0x4000;
					}
					else
					{ 
						RomBank *= 0x4000;
					}

					break;

				// RAM bank
				case var n when n >= 0x4000 && n <= 0x5fff:
					RamBank = value * 0x2000;
					break;

				// RAM
				case var n when n >= 0xa000 && n <= 0xbfff:
					ram[address & 0x1fff] = value;
					break;

				default:
					break;
			}
		}

		public void Load(string romName)
		{
			rom = File.ReadAllBytes(romName);

			for (int i = 0; i < 16; i++)
			{
				Console.Write((char)rom[0x0134 + i]);
			}

			Console.WriteLine();

			numBanks = 0x8000 << rom[0x0148];
			Console.WriteLine($"ROM size = {0x20 << rom[0x0148]}K");

			switch ((CartType)rom[0x147])
			{
				case CartType.RomOnly:
					Console.WriteLine("ROM Only");
					break;
				case CartType.Mbc1:
					break;
				case CartType.Mbc1Ram:
					break;
				case CartType.Mbc1RamBattery:
					break;
				case CartType.Mbc2:
					break;
				case CartType.Mbc2Battery:
					break;
				case CartType.RomRam:
					break;
				case CartType.RomRamBattery:
					break;
				case CartType.Mmm01:
					break;
				case CartType.Mmm01Ram:
					break;
				case CartType.Mmm01RamBattery:
					break;
				case CartType.Mbc3TimerBattery:
					break;
				case CartType.Mbc3TimerRamBattery:
					break;
				case CartType.Mbc3:
					break;
				case CartType.Mbc3Ram:
					break;
				case CartType.Mbc3RamBattery:
					Console.WriteLine("MBC3 + RAM + Battery");
					break;
				case CartType.Mbc4:
					break;
				case CartType.Mbc4Ram:
					break;
				case CartType.Mbc4RamBattery:
					break;
				case CartType.Mbc5:
					break;
				case CartType.Mbc5Ram:
					break;
				case CartType.Mbc5RamBattery:
					break;
				case CartType.Mbc5Rumble:
					break;
				case CartType.Mbc5RumbleRam:
					break;
				case CartType.Mbc5RumbleRamBattery:
					break;
				case CartType.PocketCamera:
					break;
				case CartType.BandaiTama5:
					break;
				case CartType.HuC3:
					break;
				case CartType.HuC1RamBattery:
					break;
				default:
					break;
			}
		}
	}
}

