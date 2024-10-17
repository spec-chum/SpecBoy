using CommunityToolkit.HighPerformance;

using static SDL2.SDL;
using static SDL2.SDL.SDL_Scancode;

namespace SpecBoy;

sealed class Joypad
{
	private static ReadOnlySpan<SDL_Scancode> Keymap => new SDL_Scancode[]
	{
		SDL_SCANCODE_RIGHT, SDL_SCANCODE_LEFT, SDL_SCANCODE_UP, SDL_SCANCODE_DOWN,
		SDL_SCANCODE_S, SDL_SCANCODE_A, SDL_SCANCODE_D, SDL_SCANCODE_F
	};

	private bool buttonsEnabled;
	private bool dpadEnabled;

	public byte JoyP
	{
		get
		{
			byte result = (byte)(0xc0 | buttonsEnabled.ToBytePower(5) | dpadEnabled.ToBytePower(4));

			// DMG returns 0x0f as ID (top 2 bits unused, so return 0xcf)
			if (buttonsEnabled == dpadEnabled)
			{
				result |= !buttonsEnabled ? (byte)(GetInput(0) & GetInput(4)) : (byte)0x0f;
			}
			else
			{
				result |= GetInput(!buttonsEnabled ? 4 : 0);
			}

			return result;
		}
		set
		{
			buttonsEnabled = value.IsBitSet(5);
			dpadEnabled = value.IsBitSet(4);
		}
	}

	public byte GetInput(int offset)
	{
		byte result = 0;

		ReadOnlySpan<byte> keyState;
		unsafe
		{
			keyState = new ReadOnlySpan<byte>((byte*)SDL_GetKeyboardState(out int n), n);
		}

		for (int i = 0; i < 4; i++)
		{
			result |= (byte)(keyState[(int)Keymap.DangerousGetReferenceAt(i + offset)] << i);
		}

		result ^= 0x0f;

		if (result != 0x0f)
		{
			Interrupts.IF.SetBit(Interrupts.JoypadBit);
		}

		return result;
	}
}
