using SFML.Window;

namespace SpecBoy
{
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

		public Joypad(Window window)
		{
			window.KeyPressed += OnKeyPressed;
			window.KeyReleased += OnKeyReleased;

			window.JoystickMoved += OnJoyMoved;
			window.JoystickButtonPressed += OnJoyButtonPressed;
			window.JoystickButtonReleased += OnJoyButtonReleased;

			up = 1;
			down = 1;
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

				byte value = (byte)(0xc0 | (!buttonsEnabled).ToIntPower(5) | (!dpadEnabled).ToIntPower(4));

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

		private void OnKeyPressed(object sender, KeyEventArgs e)
		{
			bool fireInterrupt = true;

			switch (e.Code)
			{
				case Keyboard.Key.Down:
					down = 0;
					break;
				case Keyboard.Key.Up:
					up = 0;
					break;
				case Keyboard.Key.Left:
					left = 0;
					break;
				case Keyboard.Key.Right:
					right = 0;
					break;

				case Keyboard.Key.F:
					start = 0;
					break;
				case Keyboard.Key.D:
					select = 0;
					break;
				case Keyboard.Key.A:
					buttonB = 0;
					break;
				case Keyboard.Key.S:
					buttonA = 0;
					break;

				case Keyboard.Key.Escape:
					((Window)sender).Close();
					break;

				default:
					fireInterrupt = false;
					break;
			}
					
			Interrupts.JoypadIrqReq = fireInterrupt;
		}

		private void OnKeyReleased(object sender, KeyEventArgs e)
		{
			switch (e.Code)
			{
				case Keyboard.Key.Up:
					up = 1;
					break;
				case Keyboard.Key.Down:
					down = 1;
					break;
				case Keyboard.Key.Left:
					left = 1;
					break;
				case Keyboard.Key.Right:
					right = 1;
					break;

				case Keyboard.Key.D:
					select = 1;
					break;
				case Keyboard.Key.F:
					start = 1;
					break;
				case Keyboard.Key.A:
					buttonB = 1;
					break;
				case Keyboard.Key.S:
					buttonA = 1;
					break;

				default:
					break;
			}
		}

		private void OnJoyMoved(object sender, JoystickMoveEventArgs e)
		{
			if (e.Axis == Joystick.Axis.X)
			{
				left = e.Position == -100 ? 0 : 1;
				right = e.Position == 100 ? 0 : 1;
			}
			else if (e.Axis == Joystick.Axis.Y)
			{
				up = e.Position == -100 ? 0 : 1;
				down = e.Position == 100 ? 0 : 1;
			}

			if (left + right + up + down != 4)
			{
				Interrupts.JoypadIrqReq = true;
			}
		}

		private void OnJoyButtonPressed(object sender, JoystickButtonEventArgs e)
		{
			bool fireInterrupt = true;

			switch (e.Button)
			{
				case 0:
					buttonA = 0;
					break;
				case 1:
					buttonB = 0;
					break;
				case 6:
					select = 0;
					break;
				case 7:
					start = 0;
					break;
				default:
					fireInterrupt = false;
					break;
			}

			Interrupts.JoypadIrqReq = fireInterrupt;
		}

		private void OnJoyButtonReleased(object sender, JoystickButtonEventArgs e)
		{
			switch (e.Button)
			{
				case 0:
					buttonA = 1;
					break;
				case 1:
					buttonB = 1;
					break;
				case 6:
					select = 1;
					break;
				case 7:
					start = 1;
					break;
				default:
					break;
			}
		}
	}
}
