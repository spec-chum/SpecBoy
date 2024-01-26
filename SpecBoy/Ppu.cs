using CommunityToolkit.HighPerformance;
using System.Runtime.CompilerServices;

using static SDL2.SDL;

using Mem16 = (byte low, byte high);
using Mem32 = (byte, byte, byte, byte);

namespace SpecBoy;

sealed class Ppu
{

	private const int ScreenWidth = 160;
	private const int ScreenHeight = 144;
	private const int OamCycles = 80;
	private const int LcdTransferCycles = 172;
	private const int LineTotalCycles = 456;

	private static ReadOnlySpan<uint> Colours => new uint[] { 0xffd0f8e0, 0xff70c088, 0xff566834, 0xff201808, 0xff0000ff };

	public byte Scy;
	public byte Scx;
	public byte Wy;
	public byte Wx;
	public byte Bgp;
	public byte Obp0;
	public byte Obp1;

	public bool HitVBlank;

	private readonly uint[] pixels;
	private readonly byte[] vRam;
	private readonly byte[] oam;

	// SDL
	private readonly nint renderer;
	private readonly nint texture;

	private LcdcReg lcdc;
	private StatReg stat;

	private int currentCycle;
	private byte winY;
	private byte lyc;
	private bool onLine153;

	public Ppu(nint renderer)
	{
		vRam = new byte[0x2000];
		oam = new byte[0xa0];
		pixels = new uint[ScreenWidth * ScreenHeight];

		this.renderer = renderer;
		texture = SDL_CreateTexture(renderer, SDL_PIXELFORMAT_ABGR8888, (int)SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, ScreenWidth, ScreenHeight);

		stat.Init();
	}

	public byte Lcdc
	{
		get => lcdc.GetByte();
		set
		{
			bool oldEnabled = lcdc.LcdEnabled;
			lcdc.SetByte(value);
			stat.LcdEnabled = lcdc.LcdEnabled;

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
				stat.CompareLyc(0);
				ChangeMode(Mode.None);
			}
		}
	}

	public byte Stat
	{
		get => stat.GetByte();
		set => stat.SetByte(value);
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
		if (stat.CurrentMode is Mode.OAM or Mode.LCDTransfer)
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
		stat.UpdatePending();

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

	public void DestroyPpuTexture()
	{
		SDL_DestroyTexture(texture);
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
			stat.CurrentMode = Mode.None;

			currentCycle = 0;
			Ly++;
			stat.LyCompareFlag = false;

			if (Ly == 144)
			{
				ChangeMode(Mode.VBlank);
			}
			else
			{
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
				Interrupts.IF.SetBit(Interrupts.VBlankBit);
				stat.RequestInterrupt(Mode.OAM);
			}

			stat.CompareLyc(Ly);
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
		stat.PendingInterrupt = Mode.None;
		stat.PendingMode = mode;

		switch (mode)
		{
			case Mode.None:
				break;

			case Mode.HBlank:
				RenderScanline();
				stat.PendingInterrupt = Mode.HBlank;
				break;

			case Mode.VBlank:
				HitVBlank = true;
				RenderFrame();
				winY = 0;
				stat.PendingInterrupt = Mode.VBlank;
				break;

			case Mode.OAM:
				stat.PendingInterrupt = Mode.OAM;
				break;

			case Mode.LCDTransfer:
				stat.PendingInterrupt = Mode.LCDTransfer;
				break;
		}
	}

	private static int RoundToMCycles(int value)
	{
		return (value + 3) & -4;
	}

	private byte ReadVRamByteInternal(int address)
	{
		return vRam.DangerousGetReferenceAt(address & 0x1fff);
	}

	private Mem16 ReadVRamWordInternal(int address)
	{
		return Unsafe.ReadUnaligned<Mem16>(ref vRam.DangerousGetReferenceAt(address & 0x1fff));
	}

	private void RenderBackground(Span<uint> pixelSpan)
	{
		// Early exit if background not enabled
		if (!lcdc.BgEnabled)
		{
			pixelSpan.Fill(GetColourFromPalette(0, Bgp));
			return;
		}

		ushort tileDataLocation = (ushort)(lcdc.TileDataSelect ? 0x8000 : 0x9000);
		ushort bgTilemap = (ushort)(lcdc.BgTileMapSelect ? 0x9c00 : 0x9800);
		ushort windowTilemap = (ushort)(lcdc.WindowTileMapSelect ? 0x9c00 : 0x9800);
		short winX = (short)(Wx - 7);

		bool windowDrawn = false;
		bool canRenderWindow = Wy <= Ly && lcdc.WindowEnabled;

		// Assume we're drawing background tiles, will be overridden below if we're drawing window
		byte tileX = Scx;
		byte tileY = (byte)(Ly + Scy);
		byte offsetY = (byte)(tileY & 7);
		ushort tilemap = bgTilemap;

		for (int x = 0; x < pixelSpan.Length; x++)
		{
			// Render window if enabled and visible
			if (!windowDrawn && (canRenderWindow && winX <= x))
			{
				windowDrawn = true;
				tileX = (byte)(x - winX);
				tileY = winY;
				offsetY = (byte)(tileY & 7);
				tilemap = windowTilemap;
			}

			// Get tile and it's pixels
			byte tileIndex = ReadVRamByteInternal(tilemap + (tileY / 8 * 32) + (tileX / 8));
			ushort offset = (ushort)((offsetY * 2) + (ushort)(lcdc.TileDataSelect ? tileIndex * 16 : (sbyte)tileIndex * 16));
			Mem16 tileData = ReadVRamWordInternal(tileDataLocation + offset);

			// Get pixel offset within tile (reversed)
			byte offsetX = (byte)((tileX & 7) ^ 7);

			// Get and display pixel (reversed)
			int colour = tileData.high.IsBitSet(offsetX).ToBytePower(1) | tileData.low.IsBitSet(offsetX).ToByte();
			pixelSpan[x] = GetColourFromPalette(colour, Bgp);

			tileX++;
		}

		// Only update window Y pos if window was drawn
		winY += windowDrawn.ToByte();
	}

	private void RenderSprites(Span<uint> pixelSpan)
	{
		// Just return if sprites not enabled
		if (!lcdc.SpritesEnabled)
		{
			return;
		}

		Span<Sprite> spriteSpan = stackalloc Sprite[10];
		int numSprites = 0;
		int spriteSize = lcdc.SpriteSize ? 16 : 8;

		// Search OAM for sprites that appear on scanline (up to 10)
		for (int i = 0; i < oam.Length && numSprites < 10; i += 4)
		{
			short spriteStartY = (short)(oam[i] - 16);
			short spriteEndY = (short)(spriteStartY + spriteSize);

			if (spriteStartY <= Ly && Ly < spriteEndY)
			{
				spriteSpan[numSprites] = new Sprite(Unsafe.ReadUnaligned<Mem32>(ref oam[i]));
				numSprites++;
			}
		}

		// Just return if there are no sprites to draw
		if (numSprites == 0)
		{
			return;
		}

		// Sort only the found sprites in ascending X co-ord
		spriteSpan[..numSprites].Sort((s1, s2) => s1.X.CompareTo(s2.X));

		// Used to check if pixel already drawn
		Span<bool> pixelDrawn = stackalloc bool[ScreenWidth];

		for (int i = 0; i < numSprites; i++)
		{
			ref Sprite sprite = ref spriteSpan.DangerousGetReferenceAt(i);

			// Is any of sprite actually visible?
			if (sprite.X >= ScreenWidth)
			{
				continue;
			}

			byte tileY;
			ushort tileIndex = 0x8000;
			if (spriteSize == 8)
			{
				tileY = (byte)(sprite.YFlip ? 7 - (Ly - sprite.Y) : (Ly - sprite.Y));
				tileIndex += (ushort)((sprite.TileNum * 16) + (tileY * 2));
			}
			else
			{
				tileY = (byte)(sprite.YFlip ? 15 - (Ly - sprite.Y) : (Ly - sprite.Y));
				tileIndex += (ushort)(((sprite.TileNum & 0xfe) * 16) + (tileY * 2));
			}

			Mem16 tileData = ReadVRamWordInternal(tileIndex);

			// Set sprite palette
			int pal = sprite.PalNum ? Obp1 : Obp0;

			for (int tilePixel = 0; tilePixel < 8; tilePixel++)
			{
				short currentPixel = (short)(sprite.X + tilePixel);

				// Pixel off screen?
				if (currentPixel < 0)
				{
					continue;
				}
				else if (currentPixel >= ScreenWidth)
				{
					// Do next sprite if remaining pixels off screen
					break;
				}

				// Optimise out some bounds checks
				ref bool pixelDrawnRef = ref pixelDrawn.DangerousGetReferenceAt(currentPixel);

				// Already drawn?
				if (pixelDrawnRef)
				{
					continue;
				}

				// Get pixel offset within tile
				byte offsetX = (byte)(sprite.XFlip ? tilePixel : tilePixel ^ 7);

				// Get colour
				int colour = tileData.high.IsBitSet(offsetX).ToBytePower(1) | tileData.low.IsBitSet(offsetX).ToByte();

				// Move on if pixel is transparent anyway
				if (colour == 0)
				{
					continue;
				}

				ref uint pixelInSpanRef = ref pixelSpan.DangerousGetReferenceAt(currentPixel);

				// Check priority or draw over transparent pixel
				if (!sprite.Priority || pixelInSpanRef == GetColourFromPalette(0, Bgp))
				{
					pixelInSpanRef = GetColourFromPalette(colour, pal);
					pixelDrawnRef = true;
				}
			}
		}
	}

	private void RenderScanline()
	{
		Span<uint> pixelSpan = new(pixels, Ly * ScreenWidth, ScreenWidth);
		RenderBackground(pixelSpan);
		RenderSprites(pixelSpan);
	}

	private void RenderFrame()
	{
		unsafe
		{
			_ = SDL_UpdateTexture(texture, nint.Zero, (nint)Unsafe.AsPointer(ref pixels.DangerousGetReference()), ScreenWidth * sizeof(uint));
		}

		_ = SDL_RenderClear(renderer);
		_ = SDL_RenderCopy(renderer, texture, nint.Zero, nint.Zero);
		SDL_RenderPresent(renderer);
	}

	private static uint GetColourFromPalette(int colour, int palette)
	{
		return Colours.DangerousGetReferenceAt((palette >> (colour << 1)) & 3);
	}

	public enum Mode
	{
		HBlank,
		VBlank,
		OAM,
		LCDTransfer,
		None = 256
	}
}
