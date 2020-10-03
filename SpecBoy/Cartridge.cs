using System;
using System.IO;

namespace SpecBoy
{
	class Cartridge
	{
		public ReadDelegate ReadByte;
		public WriteDelegate WriteByte;

		private int romBank1;
		private int ramBank;
		private int romBankHighBits;
		private int romSize;
		private bool bankingMode;
		private bool ramEnabled;
		private byte[] rom;
		private byte[] ram;

		public delegate byte ReadDelegate(int address);
		public delegate void WriteDelegate(int address, byte value);

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

		public Cartridge(string romName)
		{
			ram = new byte[0x8000];
			romBank1 = 0x4000;

			Load(romName);
		}

		public byte ReadByteRaw(int address)
		{
			return rom[address];
		}

		public void WriteByteRaw(int address, byte value)
		{
			Console.WriteLine("Attempted to write to unwritable ROM!");
			return;
		}

		public byte ReadByteMbc1(int address)
		{
			return address switch
			{
				// ROM bank0
				var n when n >= 0x0000 && n <= 0x3fff => rom[address],

				// ROM bank n
				var n when n >= 0x4000 && n <= 0x7fff => rom[romBank1 + (address & 0x3fff)],

				// Cartridge RAM
				var n when n >= 0xa000 && n <= 0xbfff && ramEnabled => ram[ramBank + (address & 0x1fff)],

				_ => 0xff,
			};
		}

		public void WriteByteMbc1(int address, byte value)
		{
			switch (address)
			{
				// Enable/Disable RAM
				case var n when n >= 0x0000 && n <= 0x1fff:
					ramEnabled = (value & 0x0f) == 0x0a;
					break;

				// ROM bank n
				case var n when n >= 0x2000 && n <= 0x3fff:
					romBank1 = value & 0x1f;

					if (!bankingMode)
					{
						romBank1 |= romBankHighBits;						
					}

					if (romBank1 == 0x00 || romBank1 == 0x20 || romBank1 == 0x40 || romBank1 == 0x60)
					{
						romBank1++;
					}

					romBank1 &= (romSize >> 4) - 1;
					romBank1 *= 0x4000;

					break;

				// RAM bank or ROM bank high bits
				case var n when n >= 0x4000 && n <= 0x5fff:
					if (bankingMode)
					{
						ramBank = (value & 0x03) * 0x2000;
						break;
					}

					romBankHighBits = (value & 0x03) << 5;
					break;

				// Bank mode select
				case var n when n >= 0x6000 && n <= 0x7fff:
					bankingMode = (value & 1) != 0;
					break;

				// RAM
				case var n when n >= 0xa000 && n <= 0xbfff && ramEnabled:
					ram[ramBank + (address & 0x1fff)] = value;
					break;

				default:
					break;
			}
		}

		public byte ReadByteMbc3(int address)
		{
			return address switch
			{
				// ROM bank0
				var n when n >= 0x0000 && n <= 0x3fff => rom[address],

				// ROM bank n
				var n when n >= 0x4000 && n <= 0x7fff => rom[romBank1 + (address & 0x3fff)],

				// Cartridge RAM
				var n when n >= 0xa000 && n <= 0xbfff && ramEnabled => ram[ramBank + (address & 0x1fff)],

				_ => 0xff,
			};
		}

		public void WriteByteMbc3(int address, byte value)
		{
			switch (address)
			{
				// ROM bank n
				case var n when n >= 0x2000 && n <= 0x3fff:
					romBank1 = value & 0x7f;

					if (romBank1 == 0)
					{
						romBank1 = 0x4000;
					}
					else
					{ 
						romBank1 *= 0x4000;
					}

					break;

				// RAM bank
				case var n when n >= 0x4000 && n <= 0x5fff:
					ramBank = value * 0x2000;
					break;

				// RAM
				case var n when n >= 0xa000 && n <= 0xbfff && ramEnabled:
					ram[ramBank + (address & 0x1fff)] = value;
					break;

				default:
					break;
			}
		}

		public void Load(string romName)
		{
			rom = File.ReadAllBytes(romName);

			Console.Write("ROM name: ");
			for (int i = 0; i < 16; i++)
			{
				Console.Write((char)rom[0x0134 + i]);
			}

			CartType cartType = (CartType)rom[0x147];
			Console.WriteLine($"\nROM type: { Enum.GetName(typeof(CartType), cartType).ToUpper() } ({ (int)cartType})");

			switch (cartType)
			{
				case CartType.RomOnly:
				case CartType.RomRam:
				case CartType.RomRamBattery:
					//Console.WriteLine("ROM Only");
					ReadByte = ReadByteRaw;
					WriteByte = WriteByteRaw;
					break;

				case CartType.Mbc1:
				case CartType.Mbc1Ram:
				case CartType.Mbc1RamBattery:
					//Console.WriteLine("MBC!");
					ReadByte = ReadByteMbc1;
					WriteByte = WriteByteMbc1;
					break;

				case CartType.Mbc2:
					break;
				case CartType.Mbc2Battery:
					break;
				case CartType.Mmm01:
					break;
				case CartType.Mmm01Ram:
					break;
				case CartType.Mmm01RamBattery:
					break;

				case CartType.Mbc3TimerBattery:
				case CartType.Mbc3TimerRamBattery:
				case CartType.Mbc3:
				case CartType.Mbc3Ram:
				case CartType.Mbc3RamBattery:
					//Console.WriteLine("MBC3");
					ReadByte = ReadByteMbc3;
					WriteByte = WriteByteMbc3;
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
					Console.WriteLine("Unknown");
					break;
			}

			romSize = 0x20 << rom[0x0148];
			Console.WriteLine($"ROM size: {romSize}K ({romSize >> 4} banks)");
		}
	}
}

