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

	public Joypad()
	{
		down = 1;
		up = 1;
		left = 1;
		right = 1;
		buttonA = 1;
		buttonB = 1;
		select = 1;
		start = 1;
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

	unsafe public void GetInput()
	{
		down = 1;
		up = 1;
		left = 1;
		right = 1;
		buttonA = 1;
		buttonB = 1;
		select = 1;
		start = 1;

		byte* keyState = (byte*)SDL_GetKeyboardState(out _).ToPointer();

		bool fireInterrupt = false;

        if (keyState[(int)SDL_SCANCODE_DOWN] != 0)
        {
            fireInterrupt = true;
            down = 0;
        }

        if (keyState[(int)SDL_SCANCODE_UP] != 0)
        {
            fireInterrupt = true;
            up = 0;
        }

        if (keyState[(int)SDL_SCANCODE_LEFT] != 0)
        {
            fireInterrupt = true;
            left = 0;
        }

        if (keyState[(int)SDL_SCANCODE_RIGHT] != 0)
        {
            fireInterrupt = true;
            right = 0;
        }

        if (keyState[(int)SDL_SCANCODE_A] != 0)
        {
            fireInterrupt = true;
            buttonB = 0;
        }

        if (keyState[(int)SDL_SCANCODE_S] != 0)
        {
            fireInterrupt = true;
            buttonA = 0;
        }

        if (keyState[(int)SDL_SCANCODE_F] != 0)
		{
			fireInterrupt = true;
			start = 0;
		}

		if (keyState[(int)SDL_SCANCODE_D] != 0)
		{
			fireInterrupt = true;
			select = 0;
		}

		Interrupts.JoypadIrqReq = fireInterrupt;
	}
}
