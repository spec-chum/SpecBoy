﻿using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace SpecBoy
{
	class Cartridge
	{
		public ReadDelegate ReadByte;
		public WriteDelegate WriteByte;

		private readonly int[] ramSizes = { 0, 2, 8, 32, 128, 64 };
		private readonly int bankLimitMask;
		private readonly byte[] rom;

		private MemoryMappedViewAccessor ram;

		private int romBank;
		private int ramBank;
		private int bankHighBits;
		private int romSize;
		private int ramSize;
		private bool bankingMode;
		private bool ramEnabled;
		private bool hasBattery;
		private bool hasRam;

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

			GetRomInfo();
			InitiateRam(romName);

			romBank = 1;
			bankLimitMask = (romSize >> 4) - 1;
		}
		private void GetRomInfo()
		{
			Console.Write("ROM name: ");
			for (int i = 0; i < 16; i++)
			{
				char c = (char)rom[0x0134 + i];
				if (c == 0)
				{
					break;
				}
				Console.Write(c);
			}

			Console.WriteLine();

			CartType cartType = (CartType)rom[0x0147];
			string typeString = Enum.GetName(typeof(CartType), cartType);
			Console.WriteLine($"ROM type: {typeString.ToUpper()} ({(int)cartType})");

			romSize = 0x20 << rom[0x0148];
			Console.WriteLine($"ROM size: {romSize}K ({romSize >> 4} banks)");

			hasRam = typeString.Contains("Ram");
			ramSize = ramSizes[rom[0x0149]];

			if (ramSize == 0)
			{
				hasRam = false;
			}

			Console.WriteLine($"Has RAM: {(hasRam ? $"Yes\nRAM size: {ramSize}K" : "No")}");

			hasBattery = typeString.Contains("Battery");
			Console.WriteLine($"Has battery: {(hasBattery ? "Yes" : "No")}");

			switch (typeString)
			{
				case var type when type.Contains("Rom"):
					ReadByte = ReadByteRomOnly;
					WriteByte = WriteByteRomOnly;
					break;

				case var type when type.Contains("Mbc1"):
					ReadByte = ReadByteMbc1;
					WriteByte = WriteByteMbc1;
					break;

				case var type when type.Contains("Mbc3"):
					ReadByte = ReadByteMbc3;
					WriteByte = WriteByteMbc3;
					break;

				default:
					break;
			}
		}

		private void InitiateRam(string romName)
		{
			if (hasRam)
			{
				MemoryMappedFile mmf;

				if (hasBattery)
				{
					mmf = MemoryMappedFile.CreateFromFile(romName.Remove(romName.LastIndexOf(".")) + ".sav", FileMode.OpenOrCreate, null, ramSize * 1024);
				}
				else
				{
					mmf = MemoryMappedFile.CreateNew(null, ramSize * 1024);
				}

				ram = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
			}
		}

		private byte ReadByteRomOnly(int address)
		{
			return rom[address];
		}

		private void WriteByteRomOnly(int address, byte value)
		{
			return;
		}

		private byte ReadByteMbc1(int address)
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
				var n when n >= 0xa000 && n <= 0xbfff && ramEnabled && !bankingMode => ram.ReadByte(address & 0x1fff),

				// Cartridge RAM - Banked
				var n when n >= 0xa000 && n <= 0xbfff && ramEnabled => ram.ReadByte(0x2000 * ramBank + (address & 0x1fff)),

				_ => 0xff,
			};
		}

		private void WriteByteMbc1(int address, byte value)
		{
			switch (address)
			{
				// Enable/Disable RAM
				case var n when n <= 0x1fff:
					ramEnabled = hasRam && (value & 0x0f) == 0x0a;
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
					if (ramSize >= 32)
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
					ram.Write(address & 0x1fff, value);
					break;

				// RAM - Banked
				case var n when n >= 0xa000 && n <= 0xbfff && ramEnabled:
					ram.Write(0x2000 * ramBank + (address & 0x1fff), value);
					break;

				default:
					break;
			}
		}

		private byte ReadByteMbc3(int address) => address switch
		{
			// ROM bank0
			var n when n <= 0x3fff => rom[address],

			// ROM bank n
			var n when n <= 0x7fff => rom[romBank + (address & 0x3fff)],

			// Cartridge RAM
			var n when n >= 0xa000 && n <= 0xbfff && ramEnabled => ram.ReadByte(ramBank + (address & 0x1fff)),

			_ => 0xff,
		};

		private void WriteByteMbc3(int address, byte value)
		{
			switch (address)
			{
				// Enable/Disable RAM
				case var n when n <= 0x1fff:
					ramEnabled = hasRam && (value & 0x0f) == 0x0a;
					break;

				// ROM bank n
				case var n when n <= 0x3fff:
					romBank = value & 0x7f;

					if (romBank == 0)
					{
						romBank++;
					}

					romBank *= 0x4000;

					break;

				// RAM bank
				case var n when n <= 0x5fff:
					if (ramSize >= 32)
					{
						ramBank = (value & 3) * 0x2000;
					}
					break;

				// RAM
				case var n when n >= 0xa000 && n <= 0xbfff && ramEnabled:
					ram.Write(ramBank + (address & 0x1fff), value);
					break;

				default:
					break;
			}
		}
	}
}
