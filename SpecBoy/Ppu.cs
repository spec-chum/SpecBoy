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
		None = 256
	}

	class Ppu
	{
		private const int ScreenWidth = 160;
		private const int ScreenHeight = 144;
		private const int OamCycles = 80;
		private const int LcdTransferCycles = 172;
		private const int LineTotalCycles = 456;

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
		private readonly byte[] vRam;
		private readonly byte[] oam;

		private LcdcReg lcdc;
		private StatReg stat;
		private Mode pendingStatMode;
		private Mode pendingStatInterrupt;

		private int currentCycle;
		private byte winY;
		private byte lyc;
		private bool onLine153;

		public Ppu(RenderWindow window, int scale)
		{
			vRam = new byte[0x2000];
			oam = new byte[0xa0];
			pixels = new byte[ScreenWidth * ScreenHeight * sizeof(uint)];
			scanlineBuffer = new int[160];

			this.window = window;
			texture = new Texture(ScreenWidth, ScreenHeight);
			framebuffer = new SfmlSprite(texture)
			{
				Scale = new Vector2f(scale, scale)
			};

			stat.CurrentMode = Mode.OAM;
			pendingStatInterrupt = Mode.None;
		}

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
					stat.CurrentMode = Mode.None;
				}
				else if (!oldEnabled && lcdc.LcdEnabled)
				{
					// LCD just been switched on, so PPU late
					currentCycle = 4;
					//stat.SetStatLy(0);
					stat.CompareLyc(0);
					ChangeMode(Mode.None);
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

		public byte Ly { get; private set; }

		public byte Lyc 
		{ 
			get => lyc;
			set
			{
				lyc = value;
				stat.Lyc = lyc;
				stat.CompareLyc(Ly);
			}
		}

		public bool CanAccessVRam()
		{
			// Always available when LCD is off
			if (!lcdc.LcdEnabled)
			{
				return true;
			}

			// Inaccessible during mode 3
			if (stat.CurrentMode == Mode.LCDTransfer)
			{
				return false;
			}

			// Inaccessible on cycle before changing to mode 3
			if (stat.CurrentMode == Mode.OAM && currentCycle == 80)
			{
				return false;
			}

			return true;
		}

		public bool CanAccessOam()
		{
			// Always available when LCD is off
			if (!lcdc.LcdEnabled)
			{
				return true;
			}

			// Inaccessible during modes 2 and 3
			if (stat.CurrentMode == Mode.OAM || stat.CurrentMode == Mode.LCDTransfer)
			{
				return false;
			}

			// Inaccessible on first cycle of line
			if (currentCycle == 0)
			{
				return false;
			}

			return true;
		}

		public byte ReadVRam(int address)
		{
			if (CanAccessVRam())
			{
				return vRam[address];
			}

			return 0xff;
		}

		public void WriteVRam(int address, byte value)
		{
			if (CanAccessVRam())
			{
				vRam[address] = value;
			}
		}

		public byte ReadOam(int address)
		{
			if (CanAccessOam())
			{
				return oam[address];
			}

			return 0xff;
		}

		public void WriteOam(int address, byte value, bool dma = false)
		{
			if (dma || CanAccessOam())
			{
				oam[address] = value;
			}
		}

		public void Tick()
		{
			if (!lcdc.LcdEnabled)
			{
				return;
			}

			currentCycle += 4;
			stat.CurrentMode = pendingStatMode;

			if (pendingStatInterrupt != Mode.None)
			{
				stat.RequestInterrupt(pendingStatInterrupt);
				pendingStatInterrupt = Mode.None;
			}

			// Ly will actually be 0 when we're still on 153
			if (onLine153)
			{
				Line153();
			}
			else if (Ly <= 143)
			{
				Line0to143();
			}
			else
			{
				VBlank();
			}
		}

		private void Line0to143()
		{
			if (currentCycle == 4)
			{
				// STAT interrupt never fired for line 0
				stat.CompareLyc(Ly, Ly != 0);
			}
			else if (currentCycle == OamCycles)
			{
				ChangeMode(Mode.LCDTransfer);
			}
			else if (currentCycle == OamCycles + LcdTransferCycles + RoundToMCycles(Scx & 7))
			{
				ChangeMode(Mode.HBlank);
			}
			else if (currentCycle == LineTotalCycles)
			{
				currentCycle = 0;
				Ly++;
				stat.LyCompareFlag = false;

				if (Ly == 144)
				{
					ChangeMode(Mode.VBlank);
				}
				else
				{
					stat.CurrentMode = Mode.None;
					ChangeMode(Mode.OAM);
				}
			}
		}

		private void VBlank()
		{
			if (currentCycle == 4)
			{
				if (Ly == 144)
				{
					Interrupts.VBlankIrqReq = true;
				}

				stat.CompareLyc(Ly);
				//stat.RequestInterrupt(Mode.OAM);
			}
			else if (currentCycle == LineTotalCycles)
			{
				currentCycle = 0;
				Ly++;
				stat.LyCompareFlag = false;

				if (Ly == 153)
				{
					onLine153 = true;
				}
			}

			// STAT IF Flag can always be set to 1 anywhere on VBlank
			stat.RequestInterrupt(Mode.VBlank);
		}

		private void Line153()
		{
			switch (currentCycle)
			{
				case 4:
					Ly = 0;
					stat.CompareLyc(153);
					break;
				case 8:
					stat.LyCompareFlag = false;
					break;
				case 12:
					// Now cmp Ly == Lyc when Ly is 0
					stat.CompareLyc(0);
					break;
				case LineTotalCycles:
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
			pendingStatInterrupt = Mode.None;
			pendingStatMode = mode;

			switch (mode)
			{
				case Mode.None:
					break;

				case Mode.HBlank:
					RenderScanline();
					pendingStatInterrupt = Mode.HBlank;
					break;

				case Mode.VBlank:
					HitVSync = true;
					RenderFrame();
					winY = 0;
					pendingStatInterrupt = Mode.VBlank;
					break;

				case Mode.OAM:
					pendingStatInterrupt = Mode.OAM;
					//stat.RequestInterrupt(Mode.OAM);
					break;

				case Mode.LCDTransfer:
					break;
			}
		}

		private int RoundToMCycles(int value)
		{
			return (value + 3) & -4;
		}

		private byte ReadVRamInternal(int address)
		{
			return vRam[address & 0x1fff];
		}

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

					byte tileIndex = ReadVRamInternal(tilemap + (ty / 8 * 32) + (tx / 8));

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

					byte lowByte = ReadVRamInternal(tileLocation);
					byte highByte = ReadVRamInternal(tileLocation + 1);

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
				short spriteStartY = (short)(oam[i] - 16);
				short spriteEndY = (short)(spriteStartY + spriteSize);

				if (spriteStartY <= Ly && Ly < spriteEndY)
				{
					sprites.Add(new Sprite(oam[i], oam[i + 1], oam[i + 2], oam[i + 3]));
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

				byte lowByte = ReadVRamInternal(tileIndex);
				byte highByte = ReadVRamInternal(tileIndex + 1);

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
