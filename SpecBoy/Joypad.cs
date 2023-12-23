using System.Collections.Frozen;
using System.Collections.Generic;
using System;

using static SDL2.SDL;
using static SDL2.SDL.SDL_Scancode;

namespace SpecBoy;

class Joypad
{
	private bool dpadEnabled;
	private bool buttonsEnabled;
	private bool isDetectingSgb;

	private int up;
	private int down;
	private int left;
	private int right;
	private int buttonA;
	private int buttonB;
	private int start;
	private int select;

	private readonly FrozenDictionary<SDL_Scancode, Action> keymap;

	public Joypad()
	{
		keymap = new Dictionary<SDL_Scancode, Action>
		{
			{ SDL_SCANCODE_UP, () => { up = 0; } },
			{ SDL_SCANCODE_DOWN, () => { down = 0; } },
			{ SDL_SCANCODE_LEFT, () => { left = 0; } },
			{ SDL_SCANCODE_RIGHT, () => { right = 0; } },
			{ SDL_SCANCODE_S, () => { buttonA = 0; } },
			{ SDL_SCANCODE_A, () => { buttonB = 0; } },
			{ SDL_SCANCODE_F, () => { start = 0; } },
			{ SDL_SCANCODE_D, () => { select = 0; } },
		}.ToFrozenDictionary();

		ResetButtons();
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

			byte value = (byte)(0xc0 | (!buttonsEnabled).ToBytePower(5) | (!dpadEnabled).ToBytePower(4));

			if (buttonsEnabled)
			{
				value |= (byte)((start << 3) | (select << 2) | (buttonB << 1) | buttonA);
			}
			else if (dpadEnabled)
			{
				value |= (byte)((down << 3) | (up << 2) | (left << 1) | right);
			}

			return value;
		}
		set
		{
			buttonsEnabled = !value.IsBitSet(5);
			dpadEnabled = !value.IsBitSet(4);

			// Check for SGB detection
			isDetectingSgb = !buttonsEnabled && !dpadEnabled;
		}
	}

	public void GetInput()
	{
		ResetButtons();

		bool fireInterrupt = false;

		unsafe
		{
			byte* keyState = (byte*)SDL_GetKeyboardState(out _).ToPointer();

			foreach (var keys in keymap)
			{
				if (keyState[(int)keys.Key] != 0)
				{
					keys.Value.Invoke();
					fireInterrupt = true;
				}
			}
		}

		Interrupts.JoypadIrqReq = fireInterrupt;
	}

	private void ResetButtons()
	{
		up = 1;
		down = 1;
		left = 1;
		right = 1;
		buttonA = 1;
		buttonB = 1;
		start = 1;
		select = 1;
	}
}
