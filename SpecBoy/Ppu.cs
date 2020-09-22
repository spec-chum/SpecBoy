using SFML.Graphics;
using SFML.System;

namespace SpecBoy
{
	class Ppu
	{
		public readonly uint[] colours = { 0xF4FFF4, 0xC0D0C0, 0x80A080, 0x001000 };
		public const ushort VBlankIrqVector = 0x40;
		public const ushort StatIrqVector = 0x48;
		public const int VBlankIeBit = 0;
		public const int StatIeBit = 1;

		private const int ScreenWidth = 160;
		private const int ScreenHeight = 144;

		private int currentCycle;
		private Mode currentMode;

		private RenderWindow window;
		private Texture texture;
		private Sprite frameBuffer;

		public Ppu(RenderWindow window)
		{
			VRam = new byte[0x2000];
			Pixels = new byte[ScreenWidth * ScreenHeight * 4];

			this.window = window;
			texture = new Texture(160, 144);
			frameBuffer = new Sprite(texture);
			frameBuffer.Scale = new Vector2f(4, 4);

			currentCycle = 0;
			currentMode = Mode.OAM;
		}

		enum Mode
		{
			HBlank,
			VBlank,
			OAM,
			LCDTransfer
		}

		public byte[] Pixels { get; }

		public byte[] VRam { get; set; }

		public byte Lcdc { get; set; }

		public byte Stat { get; set; }

		public byte Scy { get; set; }

		public byte Scx { get; set; }

		public byte Ly { get; set; }

		public byte Lyc { get; set; }

		public byte Wy { get; set; }

		public byte Wx { get; set; }

		public byte Bgp { get; set; }

		public bool VBlankIrqReq { get; set; }

		public bool StatIrqReq { get; set; }

		public void Tick(int cycles)
		{
			currentCycle++;

			switch (currentMode)
			{
				case Mode.HBlank:
					if (currentCycle == 204)
					{
						currentCycle = 0;
						Ly++;

						if (Ly == 0x90)
						{
							ChangeMode(Mode.VBlank);
						}
						else
						{
							ChangeMode(Mode.OAM);
						}

						CompareLYC();
					}
					break;

				case Mode.VBlank:
					if (currentCycle == 456)
					{
						currentCycle = 0;
						Ly++;

						if (Ly == 154)
						{
							ChangeMode(Mode.OAM);
							Ly = 0;
						}
					}
					break;

				case Mode.OAM:
					if (currentCycle == 80)
					{
						currentCycle = 0;
						ChangeMode(Mode.LCDTransfer);
					}
					break;

				case Mode.LCDTransfer:
					if (currentCycle == 204)
					{
						currentCycle = 0;
						ChangeMode(Mode.HBlank);
					}
					break;
			}
		}

		private void ChangeMode(Mode mode)
		{
			switch (mode)
			{
				case Mode.HBlank:
					RenderBackground();
					currentMode = Mode.HBlank;

					Stat = (byte)(Stat & 0xfc);

					if (Utility.IsBitSet(Stat, 3))
					{
						StatIrqReq = true;
					}

					break;

				case Mode.VBlank:
					RenderBuffer();
					currentMode = Mode.VBlank;
					VBlankIrqReq = true;

					Stat = (byte)((Stat & 0xfc) | 1);

					if (Utility.IsBitSet(Stat, 4))
					{
						StatIrqReq = true;
					}

					break;

				case Mode.OAM:
					currentMode = Mode.OAM;

					Stat = (byte)((Stat & 0xfc) | 2);

					if (Utility.IsBitSet(Stat, 5))
					{
						StatIrqReq = true;
					}

					break;

				case Mode.LCDTransfer:
					currentMode = Mode.LCDTransfer;
					Stat = (byte)((Stat & 0xfc) | 3);
					break;

				default:
					break;
			}
		}

		void CompareLYC()
		{
			if (Ly == Lyc)
			{
				Stat = Utility.SetBit(Stat, 2);

				if (Utility.IsBitSet(Stat, 6))
				{
					StatIrqReq = true;
				}
			}
		}

		private byte ReadByte(int address)
		{
			return VRam[address & 0x1fff];
		}

		private ushort ReadWord(int address)
		{
			return (ushort)(ReadByte(address) | (ReadByte(address + 1) << 8));
		}

		void RenderBackground()
		{
			int framebufferIndex = Ly * 160 * 4;
			ushort tile_y;
			ushort p;
			ushort bgTileMapBase = (ushort)(Utility.IsBitSet(Lcdc, 3) ? 0x9c00 : 0x9800);
			ushort tileDataBase = (ushort)(Utility.IsBitSet(Lcdc, 4) ? 0x8000 : 0x8800);

			ushort x = 0;
			ushort y;

			while (x < 160)
			{
				ushort tileLine = 0;
				ushort tile_x = 0;

				if (Utility.IsBitSet(Lcdc, 0))
				{
					y = (byte)(Ly + Scy);
					tile_y = (ushort)(y & 7);

					p = (ushort)(x + Scx);
					tile_x = (ushort)(p & 7);

					byte tileIndex = ReadByte(bgTileMapBase + (((y >> 3) << 5) & 0x3FF) + ((p >> 3) & 31));

					if (tileDataBase == 0x8000)
					{
						tileLine = ReadWord(tileDataBase + (tileIndex << 4) + (tile_y << 1));
					}
					else
					{
						tileLine = ReadWord((0x9000 + (sbyte)tileIndex * 16) + (tile_y << 1));
					}
				}

				byte highByte = (byte)(tileLine >> 8);
				byte lowByte = (byte)tileLine;

				int colour = (Utility.IsBitSet(highByte, 7 - tile_x) ? 1 << 1 : 0) | (Utility.IsBitSet(lowByte, 7 - tile_x) ? 1 : 0);
				colour = GetColourFromPalette(colour, Bgp);

				Pixels[framebufferIndex + 0] = (byte)(colours[colour] >> 0);
				Pixels[framebufferIndex + 1] = (byte)(colours[colour] >> 8);
				Pixels[framebufferIndex + 2] = (byte)(colours[colour] >> 16);
				Pixels[framebufferIndex + 3] = 0xff;

				x++;
				framebufferIndex += 4;			
			}
		}

		private void RenderBuffer()
		{
			texture.Update(Pixels);
			//window.Clear();
			window.Draw(frameBuffer);
			window.Display();
		}

		private int GetColourFromPalette(int colour, int palette)
		{
			return (palette >> (colour << 1)) & 3;
		}
	}
}
