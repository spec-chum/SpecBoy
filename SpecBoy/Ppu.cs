﻿using SFML.Graphics;
using SFML.System;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// Avoid conflict with our Sprite class - I refuse to rename it :D
using SfmlSprite = SFML.Graphics.Sprite;

namespace SpecBoy
{
	class Ppu
	{
		private const int ScreenWidth = 160;
		private const int ScreenHeight = 144;

		//public readonly uint[] colours = { 0xfff4fff4, 0xffc0d0c0, 0xff80a080, 0xff001000, 0xff0000ff };
		public readonly uint[] colours = { 0xffd0f8e0, 0xff70c088, 0xff566834, 0xff201808, 0xff0000ff };

		public bool HitVSync;

		// SFML
		private readonly RenderWindow window;
		private readonly Texture texture;
		private readonly SfmlSprite framebuffer;

		private readonly byte[] pixels;
		private readonly int[] scanlineBuffer;

		private int currentCycle;
		private byte winY;
		private Mode currentMode;

		public Ppu(RenderWindow window, int scale)
		{
			VRam = new byte[0x2000];
			Oam = new byte[0xa0];
			pixels = new byte[ScreenWidth * ScreenHeight * 4];
			scanlineBuffer = new int[160];

			this.window = window;
			texture = new Texture(ScreenWidth, ScreenHeight);
			framebuffer = new SfmlSprite(texture);
			framebuffer.Scale = new Vector2f(scale, scale);

			currentMode = Mode.OAM;
		}

		private enum Mode
		{
			HBlank,
			VBlank,
			OAM,
			LCDTransfer
		}

		public byte[] VRam { get; set; }

		public byte[] Oam { get; set; }

		public byte Lcdc { get; set; }

		public byte Stat { get; set; }

		public byte Scy { get; set; }

		public byte Scx { get; set; }

		public byte Ly { get; set; }

		public byte Lyc { get; set; }

		public byte Wy { get; set; }

		public byte Wx { get; set; }

		public byte Bgp { get; set; }

		public byte Obp0 { get; set; }

		public byte Obp1 { get; set; }

		public void Tick()
		{
			currentCycle += 4;

			switch (currentMode)
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
						
						CompareLYC();
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

		private void ChangeMode(Mode mode)
		{
			switch (mode)
			{
				case Mode.HBlank:
					int framebufferIndex = Ly * 160 * 4;
					RenderScanline(framebufferIndex);

					currentMode = Mode.HBlank;

					Stat = (byte)(Stat & 0xfc);

					if (Utility.IsBitSet(Stat, 3))
					{
						Interrupts.StatIrqReq = true;
					}

					break;

				case Mode.VBlank:
					HitVSync = true;

					winY = 0;
					RenderBuffer();

					currentMode = Mode.VBlank;

					Interrupts.VBlankIrqReq = true;

					Stat = (byte)((Stat & 0xfc) | 1);

					if (Utility.IsBitSet(Stat, 4))
					{
						Interrupts.StatIrqReq = true;
					}

					break;

				case Mode.OAM:
					currentMode = Mode.OAM;

					Stat = (byte)((Stat & 0xfc) | 2);

					if (Utility.IsBitSet(Stat, 5))
					{
						Interrupts.StatIrqReq = true;
					}

					break;

				case Mode.LCDTransfer:
					currentMode = Mode.LCDTransfer;
					Stat = (byte)((Stat & 0xfc) | 3);
					break;
			}
		}

		private void CompareLYC()
		{
			if (Ly == Lyc)
			{
				Stat = Utility.SetBit(Stat, 2);

				if (Utility.IsBitSet(Stat, 6))
				{
					Interrupts.StatIrqReq = true;
				}
			}
			else
			{
				Stat = Utility.ClearBit(Stat, 2);
			}
		}

		private byte ReadByteVRam(int address)
		{
			return VRam[address & 0x1fff];
		}

		private ushort ReadVramWord(int address)
		{
			return (ushort)(ReadByteVRam(address) | (ReadByteVRam(address + 1) << 8));
		}

		private void RenderBackground()
		{
			int colour = 0;
			ushort tileData = (ushort)(Utility.IsBitSet(Lcdc, 4) ? 0x8000 : 0x8800);
			ushort bgTilemap = (ushort)(Utility.IsBitSet(Lcdc, 3) ? 0x9c00 : 0x9800);
			ushort windowTilemap = (ushort)(Utility.IsBitSet(Lcdc, 6) ? 0x9C00 : 0x9800);
			byte winX = (byte)(Wx - 7);

			bool windowDrawn = false;
			bool canRenderWindow = Wy <= Ly && Utility.IsBitSet(Lcdc, 5);

			for (int x = 0; x < 160; x++)
			{
				// Colour is 0 is BG Priority bit not set
				if (Utility.IsBitSet(Lcdc, 0))
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

					colour = (Utility.IsBitSet(highByte, 7 - tileX) ? (1 << 1) : 0) | (Utility.IsBitSet(lowByte, 7 - tileX) ? 1 : 0);
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
			if (!Utility.IsBitSet(Lcdc, 1))
			{
				return;
			}

			byte screenY = Ly;

			var sprites = new List<Sprite>();

			int spriteSize = Utility.IsBitSet(Lcdc, 2) ? 16 : 8;

			// Search OAM for sprites that appear on scanline (up to 10)
			for (int i = 0; i < 0xa0 && sprites.Count < 10; i +=4 )
			{
				short spriteStartY = (short)(Oam[i] - 16);
				short spriteEndY = (short)(spriteStartY + spriteSize);

				if (spriteStartY <= screenY && screenY < spriteEndY)
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
					tileY = (byte)(sprite.YFlip ? 7 - (screenY - sprite.Y) : (screenY - sprite.Y));
					tileIndex += (ushort)(sprite.TileNum * 16 + (tileY * 2));
				}
				else
				{
					tileY = (byte)(sprite.YFlip ? 15 - (screenY - sprite.Y) : (screenY - sprite.Y));
					tileIndex += (ushort)((sprite.TileNum & 0xfe) * 16 + (tileY * 2));
				}

				byte lowByte = ReadByteVRam(tileIndex);
				byte highByte = ReadByteVRam(tileIndex + 1);

				for (int tilePixel = 0; tilePixel < 8; tilePixel++)
				{
					short currentPixel = (short)(sprite.X + tilePixel);

					if (currentPixel < 0)
					{
						continue;
					}

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
					int colour = (Utility.IsBitSet(highByte, 7 - tileX) ? (1 << 1) : 0) | (Utility.IsBitSet(lowByte, 7 - tileX) ? 1 : 0);
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

		private void RenderScanline(int framebufferIndex)
		{
			RenderBackground();
			RenderSprites();

			var scanline = MemoryMarshal.Cast<byte, uint>(new Span<byte>(pixels, framebufferIndex, sizeof(uint) * 160));

			for (int i = 0; i < 160; i++)
			{
				scanline[i] = colours[scanlineBuffer[i]];
			}
		}

		private void RenderBuffer()
		{
			texture.Update(pixels);
			window.Draw(framebuffer);
			window.Display();
		}

		private int GetColourFromPalette(int colour, int palette) => (palette >> (colour << 1)) & 3;
	}
}
