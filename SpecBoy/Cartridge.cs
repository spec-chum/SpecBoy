using System;
using System.IO;

namespace SpecBoy
{
	class Cartridge
	{
		public ReadDelegate ReadByte;
		public WriteDelegate WriteByte;

		private readonly int bankLimitMask;
		private readonly byte[] rom;
		private readonly byte[] ram;

		private int romBank;
		private int ramBank;
		private int bankHighBits;
		private int romSize;
		private int ramSize;
		private bool bankingMode;
		private bool ramEnabled;

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
			rom = File.ReadAllBytes(romName);
			ram = new byte[0x20000];
			romBank = 1;

			GetRomInfo();
			bankLimitMask = (romSize >> 4) - 1;
		}

		public byte ReadByteRomOnly(int address) => rom[address];

		public void WriteByteRomOnly(int address, byte value)
		{
			return;
		}

		public byte ReadByteMbc1(int address)
		{
			// Split banked and non-banked for less cpu work during address calcs
			return address switch
			{
				// ROM bank0 - Non-banked
				var n when n <= 0x3fff && !bankingMode => rom[address],

				// ROM bank0 - Banked
				var n when n <= 0x3fff => rom[0x4000 * (bankHighBits & bankLimitMask) + address],

				// ROM bank n
				var n when n <= 0x7fff => rom[0x4000 * ((romBank | bankHighBits) & bankLimitMask) + (address & 0x3fff)],

				// Cartridge RAM - Non-banked
				var n when n >= 0xa000 && n <= 0xbfff && ramEnabled && !bankingMode => ram[address & 0x1fff],

				// Cartridge RAM - Banked
				var n when n >= 0xa000 && n <= 0xbfff && ramEnabled => ram[0x2000 * ramBank + (address & 0x1fff)],

				_ => 0xff,
			};
		}

		public void WriteByteMbc1(int address, byte value)
		{
			switch (address)
			{
				// Enable/Disable RAM
				case var n when n <= 0x1fff:
					ramEnabled = (value & 0x0f) == 0x0a;
					break;

				// ROM bank n
				case var n when n <= 0x3fff:
					romBank = (value & 0x1f);

					if (romBank == 0)
					{
						romBank++;
					}

					break;

				// RAM bank or ROM bank high bits
				case var n when n <= 0x5fff:
					if (ramSize >= 3)
					{
						ramBank = value & 3;
					}

					bankHighBits = (value & 3) << 5;
					break;

				// Bank mode select
				case var n when n <= 0x7fff:
					bankingMode = (value & 1) == 1;
					break;

				// RAM - Non-banked
				case var n when n >= 0xa000 && n <= 0xbfff && ramEnabled && !bankingMode:
					ram[address & 0x1fff] = value;
					break;

				// RAM - Banked
				case var n when n >= 0xa000 && n <= 0xbfff && ramEnabled:
					ram[0x2000 * ramBank + (address & 0x1fff)] = value;
					break;

				default:
					break;
			}
		}

		public byte ReadByteMbc3(int address) => address switch
		{
			// ROM bank0
			var n when n <= 0x3fff => rom[address],

			// ROM bank n
			var n when n <= 0x7fff => rom[0x4000 * romBank + (address & 0x3fff)],

			// Cartridge RAM
			var n when n >= 0xa000 && n <= 0xbfff && ramEnabled => ram[ramBank + (address & 0x1fff)],

			_ => 0xff,
		};

		public void WriteByteMbc3(int address, byte value)
		{
			switch (address)
			{
				// Enable/Disable RAM
				case var n when n <= 0x1fff:
					ramEnabled = (value & 0x0f) == 0x0a;
					break;

				// ROM bank n
				case var n when n <= 0x3fff:
					romBank = value & 0x7f;

					if (romBank == 0)
					{
						romBank++;
					}

					break;

				// RAM bank
				case var n when n <= 0x5fff:
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

		public void GetRomInfo()
		{
			Console.Write("ROM name: ");
			for (int i = 0; i < 16; i++)
			{
				Console.Write((char)rom[0x0134 + i]);
			}

			CartType cartType = (CartType)rom[0x147];
			Console.WriteLine($"\nROM type: { Enum.GetName(typeof(CartType), cartType).ToUpper() } ({(int)cartType})");

			switch (cartType)
			{
				case CartType.RomOnly:
				case CartType.RomRam:
				case CartType.RomRamBattery:
					ReadByte = ReadByteRomOnly;
					WriteByte = WriteByteRomOnly;
					break;

				case CartType.Mbc1:
				case CartType.Mbc1Ram:
				case CartType.Mbc1RamBattery:
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

			ramSize = rom[0x149];
		}
	}
}

