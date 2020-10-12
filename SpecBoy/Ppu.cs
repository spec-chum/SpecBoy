using SFML.Graphics;
using SFML.System;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// Avoid conflict with our Sprite class - I refuse to rename it :D
using SfmlSprite = SFML.Graphics.Sprite;

namespace SpecBoy
{
	enum Mode
	{
		HBlank,
		VBlank,
		OAM,
		LCDTransfer,
		None
	}

	class Ppu
	{
		private const int ScreenWidth = 160;
		private const int ScreenHeight = 144;

		public readonly uint[] colours = { 0xffd0f8e0, 0xff70c088, 0xff566834, 0xff201808, 0xff0000ff };

		public byte Scy;
		public byte Scx;
		public byte Wy;
		public byte Wx;
		public byte Bgp;
		public byte Obp0;
		public byte Obp1;

		public bool HitVSync;

		// SFML
		private readonly RenderWindow window;
		private readonly Texture texture;
		private readonly SfmlSprite framebuffer;

		private readonly int[] scanlineBuffer;
		private readonly byte[] pixels;

		private LcdcReg lcdc;
		private StatReg stat;

		private int currentCycle;
		private byte winY;
		private byte ly;
		private byte lyc;
		private bool lcdJustEnabled;
		private bool onLine153;

		public Ppu(RenderWindow window, int scale)
		{
			VRam = new byte[0x2000];
			Oam = new byte[0xa0];
			pixels = new byte[ScreenWidth * ScreenHeight * sizeof(uint)];
			scanlineBuffer = new int[160];

			this.window = window;
			texture = new Texture(ScreenWidth, ScreenHeight);
			framebuffer = new SfmlSprite(texture)
			{
				Scale = new Vector2f(scale, scale)
			};

			stat.CurrentMode = Mode.OAM;
		}

		private byte[] VRam { get; }

		private byte[] Oam { get; }

		public byte Lcdc
		{
			get
			{
				return lcdc.GetByte();
			}

			set
			{
				bool oldEnabled = lcdc.LcdEnabled;
				lcdc.SetByte(value);
				stat.lcdEnabled = lcdc.LcdEnabled;

				// LCD being switched off?
				if (oldEnabled && !lcdc.LcdEnabled)
				{
					// Reset state
					Ly = 0;
					currentCycle = 0;
					stat.CurrentMode = Mode.HBlank;
				}
				else if (!oldEnabled && lcdc.LcdEnabled)
				{
					// LCD just been switched on
					lcdJustEnabled = true;
					stat.SetLy(0, true);
					stat.CurrentMode = Mode.HBlank;
				}
			}
		}

		public byte Stat
		{
			get
			{
				return stat.GetByte();
			}

			set
			{
				stat.SetByte(value);
			}
		}

		public byte Ly 
		{ 
			get => ly;
			private set
			{
				ly = value;

				// Cmp for Ly = 0 is done in VBlank, so don't do it here
				stat.SetLy(ly, ly != 0);
			}
		}

		public byte Lyc 
		{ 
			get => lyc;
			set
			{
				lyc = value;

				// Cmp for Ly = 0 is done in VBlank, so don't do it here
				stat.SetLyc(lyc, ly != 0);
			}
		}

		public byte ReadVRam(int address)
		{
			if (stat.CurrentMode == Mode.LCDTransfer)
			{
				return 0xff;
			}

			return VRam[address];
		}

		public void WriteVRam(int address, byte value)
		{
			if (stat.CurrentMode != Mode.LCDTransfer)
			{
				VRam[address] = value;
			}
		}

		public byte ReadOam(int address)
		{
			if (stat.CurrentMode == Mode.LCDTransfer || stat.CurrentMode == Mode.OAM)
			{
				return 0xff;
			}

			return Oam[address];
		}

		public void WriteOam(int address, byte value, bool bypass = false)
		{
			if (bypass || stat.CurrentMode != Mode.LCDTransfer && stat.CurrentMode != Mode.OAM)
			{
				Oam[address] = value;
			}
		}

		public void Tick()
		{
			if (!lcdc.LcdEnabled)
			{
				return;
			}

			if (Ly == 153 || onLine153)
			{
				Line153();
			}
			else if (Ly == 0)
			{
				Line0();
			}
			else
			{
				Lines1to152();
			}

			currentCycle += 4;
		}

		private void Line0()
		{
			Lines1to152();
		}

		private void Lines1to152()
		{
			switch (stat.CurrentMode)
			{
				case Mode.HBlank:
					if (currentCycle == 204)
					{
						currentCycle = 0;
						Ly++;

						if (Ly == 144)
						{
							ChangeMode(Mode.VBlank);
						}
						else
						{
							ChangeMode(Mode.OAM);
						}
					}

					break;

				case Mode.VBlank:
					if (currentCycle == 456)
					{
						currentCycle = 0;
						Ly++;
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
					if (currentCycle == 172)
					{
						currentCycle = 0;
						ChangeMode(Mode.HBlank);
					}
					break;
			}
		}

		private void Line153()
		{
			switch (currentCycle)
			{
				case 4:
					// Shows Ly as 0 after 4 cycles, but cmp is still against 153
					onLine153 = true;
					Ly = 0;
					stat.SetLy(153, true);
					break;
				case 12:
					// Now cmp Ly == Lyc when Ly is 0
					stat.SetLy(0, true);
					break;
				case 456:
					// We're done
					onLine153 = false;
					currentCycle = 0;
					ChangeMode(Mode.OAM);
					break;

				default:
					break;
			}
		}

		private void ChangeMode(Mode mode)
		{
			stat.CurrentMode = mode;

			switch (mode)
			{
				case Mode.HBlank:
					RenderScanline();

					if (stat.HBlankInt)
					{
						stat.RequestInterrupt(Mode.HBlank);
					}

					break;

				case Mode.VBlank:
					HitVSync = true;
					RenderFrame();

					winY = 0;

					Interrupts.VBlankIrqReq = true;

					if (stat.VBlankInt)
					{
						stat.RequestInterrupt(Mode.VBlank);
					}

					// Also fire OAM interrupt if set
					if (stat.OamInt)
					{
						stat.RequestInterrupt(Mode.OAM);
					}

					break;

				case Mode.OAM:
					if (stat.OamInt)
					{
						stat.RequestInterrupt(Mode.OAM);
					}

					break;

				case Mode.LCDTransfer:
					break;
			}
		}

		private byte ReadByteVRam(int address) => VRam[address & 0x1fff];

		private void RenderBackground()
		{
			int colour = 0;
			ushort tileData = (ushort)(lcdc.TileDataSelect ? 0x8000 : 0x8800);
			ushort bgTilemap = (ushort)(lcdc.BgTileMapSelect ? 0x9c00 : 0x9800);
			ushort windowTilemap = (ushort)(lcdc.WindowTileMapSelect ? 0x9c00 : 0x9800);
			short winX = (short)(Wx - 7);

			bool windowDrawn = false;
			bool canRenderWindow = Wy <= Ly && lcdc.WindowEnabled;

			for (int x = 0; x < 160; x++)
			{
				// Colour is 0 if BG Priority bit not set
				if (lcdc.BgEnabled)
				{
					ushort tilemap;
					byte tx;
					byte ty;

					// Render window if enabled and visible
					if (canRenderWindow && winX <= x)
					{
						windowDrawn = true;

						tx = (byte)(x - winX);
						ty = winY;

						tilemap = windowTilemap;
					}
					// Or render background
					else
					{
						windowDrawn = false;

						tx = (byte)(x + Scx);
						ty = (byte)(Ly + Scy);

						tilemap = bgTilemap;
					}

					byte tileIndex = ReadByteVRam(tilemap + (ty / 8 * 32) + (tx / 8));

					byte tileX = (byte)(tx & 7);
					byte tileY = (byte)(ty & 7);

					ushort tileLocation;
					if (tileData == 0x8000)
					{
						tileLocation = (ushort)(tileData + (tileIndex * 16) + (tileY * 2));
					}
					else
					{
						tileLocation = (ushort)(tileData + 0x0800 + ((sbyte)tileIndex * 16) + (tileY * 2));
					}

					byte lowByte = ReadByteVRam(tileLocation);
					byte highByte = ReadByteVRam(tileLocation + 1);

					colour = (highByte.IsBitSet(7 - tileX) ? (1 << 1) : 0) | (lowByte.IsBitSet(7 - tileX) ? 1 : 0);
				}

				scanlineBuffer[x] = GetColourFromPalette(colour, Bgp);
			}

			// Only update window Y pos if window was drawn
			if (windowDrawn)
			{
				winY++;
			}
		}

		private void RenderSprites()
		{
			// Just return if sprites not enabled
			if (!lcdc.SpritesEnabled)
			{
				return;
			}

			var sprites = new List<Sprite>();
			int spriteSize = lcdc.SpriteSize ? 16 : 8;

			// Search OAM for sprites that appear on scanline (up to 10)
			for (int i = 0; i < 0xa0 && sprites.Count < 10; i += 4)
			{
				short spriteStartY = (short)(Oam[i] - 16);
				short spriteEndY = (short)(spriteStartY + spriteSize);

				if (spriteStartY <= Ly && Ly < spriteEndY)
				{
					sprites.Add(new Sprite(Oam[i], Oam[i + 1], Oam[i + 2], Oam[i + 3]));
				}
			}

			// Just return if there are no sprites to draw
			if (sprites.Count == 0)
			{
				return;
			}

			// Cache of sprite.x values to prioritise sprites
			int[] minX = new int[160];

			foreach (var sprite in sprites)
			{
				// Is any of sprite actually visible?
				if (sprite.X >= 160)
				{
					continue;
				}

				byte tileY;
				ushort tileIndex = 0x8000;
				if (spriteSize == 8)
				{
					tileY = (byte)(sprite.YFlip ? 7 - (Ly - sprite.Y) : (Ly - sprite.Y));
					tileIndex += (ushort)(sprite.TileNum * 16 + (tileY * 2));
				}
				else
				{
					tileY = (byte)(sprite.YFlip ? 15 - (Ly - sprite.Y) : (Ly - sprite.Y));
					tileIndex += (ushort)((sprite.TileNum & 0xfe) * 16 + (tileY * 2));
				}

				byte lowByte = ReadByteVRam(tileIndex);
				byte highByte = ReadByteVRam(tileIndex + 1);

				for (int tilePixel = 0; tilePixel < 8; tilePixel++)
				{
					short currentPixel = (short)(sprite.X + tilePixel);

					// Pixel off screen?
					if (currentPixel < 0)
					{
						continue;
					}

					// Rest of sprite off screen?
					if (currentPixel >= 160)
					{
						break;
					}

					// Lower sprite.x has priority, but ignore if previous pixel was transparent
					if (minX[currentPixel] != 0 && minX[currentPixel] <= sprite.X + 100 && scanlineBuffer[currentPixel] != 0)
					{
						continue;
					}

					byte tileX = (byte)(sprite.XFlip ? 7 - tilePixel : tilePixel);

					// Get colour
					int colour = (highByte.IsBitSet(7 - tileX) ? (1 << 1) : 0) | (lowByte.IsBitSet(7 - tileX) ? 1 : 0);
					int pal = sprite.PalNum ? Obp1 : Obp0;

					// Check priority and only draw pixel if not transparent colour
					if ((!sprite.Priority || scanlineBuffer[currentPixel] == 0) && colour != 0)
					{
						scanlineBuffer[currentPixel] = GetColourFromPalette(colour, pal);
						minX[currentPixel] = sprite.X + 100;
					}
				}
			}
		}

		private void RenderScanline()
		{
			RenderBackground();
			RenderSprites();

			var scanline = MemoryMarshal.Cast<byte, uint>(new Span<byte>(pixels, Ly * (160 * sizeof(uint)), 160 * sizeof(uint)));

			for (int i = 0; i < 160; i++)
			{
				scanline[i] = colours[scanlineBuffer[i]];
			}
		}

		private void RenderFrame()
		{
			texture.Update(pixels);
			window.Draw(framebuffer);
			window.Display();
		}

		private int GetColourFromPalette(int colour, int palette) => (palette >> (colour << 1)) & 3;
	}
}
