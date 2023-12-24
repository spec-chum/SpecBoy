﻿using CommunityToolkit.HighPerformance;

using static SDL2.SDL;
using static SDL2.SDL.SDL_Scancode;

namespace SpecBoy;

class Joypad
{
	private bool buttonsEnabled;
	private bool dpadEnabled;
	
	private readonly SDL_Scancode[] keymap = [
		SDL_SCANCODE_RIGHT, SDL_SCANCODE_LEFT, SDL_SCANCODE_UP, SDL_SCANCODE_DOWN,
		SDL_SCANCODE_S, SDL_SCANCODE_A, SDL_SCANCODE_D, SDL_SCANCODE_F];

	public byte JoyP
	{
		get
		{
			// DMG returns 0x0f as ID (top 2 bits unused, so return 0xcf)
			if (buttonsEnabled == dpadEnabled)
			{
				return 0xcf;
			}

			return (byte)(0xc0 | buttonsEnabled.ToBytePower(5) | dpadEnabled.ToBytePower(4) | GetInput());
		}
		set
		{
			buttonsEnabled = value.IsBitSet(5);
			dpadEnabled = value.IsBitSet(4);
		}
	}

	public byte GetInput()
	{
		byte result = 0;
		int offset = !dpadEnabled ? 0 : 4;

        unsafe
		{
			byte* keyState = (byte*)SDL_GetKeyboardState(out _).ToPointer();

			for (int i = 0; i < 4; i++)
			{
				result |= (byte)(keyState[(int)keymap.DangerousGetReferenceAt(i + offset)] << i);
			}
		}
		result ^= 0x0f;
		Interrupts.JoypadIrqReq = result != 0x0f;

		return result;
	}
}
