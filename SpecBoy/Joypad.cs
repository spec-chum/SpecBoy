using CommunityToolkit.HighPerformance;

using static SDL2.SDL;
using static SDL2.SDL.SDL_Scancode;

namespace SpecBoy;

class Joypad
{
	private bool dpadEnabled;
	private bool buttonsEnabled;
	private bool isDetectingSgb;

	private byte buttons;

	private readonly SDL_Scancode[] keymap = [
		SDL_SCANCODE_RIGHT, SDL_SCANCODE_LEFT, SDL_SCANCODE_UP, SDL_SCANCODE_DOWN,
		SDL_SCANCODE_S, SDL_SCANCODE_A, SDL_SCANCODE_D, SDL_SCANCODE_F];

	public Joypad()
	{
		buttons = 0xff;
	}

	public byte JoyP
	{
		get
		{
			// DMG returns 0x0f as ID (top 2 bits unused, so return 0xcf)
			if (isDetectingSgb)
			{
				return 0xcf;
			}

			byte value = (byte)(0xc0 | buttonsEnabled.ToBytePower(5) | dpadEnabled.ToBytePower(4));

			if (!buttonsEnabled)
			{
				value |= (byte)(buttons >> 4);
			}
			else if (!dpadEnabled)
			{
				value |= (byte)(buttons & 0x0f);
			}
			else
			{
				value |= 0x0f;
			}

			return value;
		}
		set
		{
			buttonsEnabled = value.IsBitSet(5);
			dpadEnabled = value.IsBitSet(4);

			// Check for SGB detection
			isDetectingSgb = !buttonsEnabled && !dpadEnabled;
		}
	}

	public void GetInput()
	{
		buttons = 0;

		unsafe
		{
			byte* keyState = (byte*)SDL_GetKeyboardState(out _).ToPointer();

			for (int i = 0; i < keymap.Length; i++)
			{
				buttons |= (byte)(keyState[(int)keymap.DangerousGetReferenceAt(i)] << i);
			}
		}
		buttons ^= 0xff;

		Interrupts.JoypadIrqReq = buttons != 0xff;
	}
}
