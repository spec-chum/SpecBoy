namespace SpecBoy;

static class Interrupts
{
	public const ushort VBlankIrqVector = 0x40;
	public const int VBlankIeBit = 0;

	public const ushort StatIrqVector = 0x48;
	public const int StatIeBit = 1;

	public const ushort TimerIrqVector = 0x50;
	public const int TimerIeBit = 2;

	public const ushort SerialIrqVector = 0x58;
	public const int SerialIeBit = 3;

	public const int JoypadIrqVector = 0x60;
	public const int JoypadIeBit = 4;

	private static bool vBlankIrqReq;
	private static bool statIrqReq;
	private static bool serialIrqReq;
	private static bool timerIrqReq;
	private static bool joypadIrqReq;

	private static byte interruptFlag;

	public static byte IE { get; set; }

	public static byte IF
	{
		get
		{
			return interruptFlag;
		}
		set
		{
			vBlankIrqReq = value.IsBitSet(0);
			statIrqReq = value.IsBitSet(1);
			timerIrqReq = value.IsBitSet(2);
			serialIrqReq = value.IsBitSet(3);
			joypadIrqReq = value.IsBitSet(4);

			interruptFlag = value;
		}
	}

	public static bool VBlankIrqReq
	{
		get
		{
			return vBlankIrqReq;
		}
		set
		{
			if (value)
			{
				interruptFlag |= 1;
			}
			else
			{
				interruptFlag &= 0xff ^ 1;
			}
			vBlankIrqReq = value;
		}
	}

	public static bool StatIrqReq
	{
		get
		{
			return statIrqReq;
		}
		set
		{
			if (value)
			{
				interruptFlag |= 1 << 1;
			}
			else
			{
				interruptFlag &= 0xff ^ (1 << 1);
			}
			statIrqReq = value;
		}
	}

	public static bool TimerIrqReq
	{
		get
		{
			return timerIrqReq;
		}
		set
		{
			if (value)
			{
				interruptFlag |= 1 << 2;
			}
			else
			{
				interruptFlag &= 0xff ^ (1 << 2);
			}
			timerIrqReq = value;
		}
	}

	public static bool SerialIrqReq
	{
		get
		{
			return serialIrqReq;
		}
		set
		{
			if (value)
			{
				interruptFlag |= 1 << 3;
			}
			else
			{
				interruptFlag &= 0xff ^ (1 << 3);
			}
			serialIrqReq = value;
		}
	}

	public static bool JoypadIrqReq
	{
		get
		{
			return joypadIrqReq;
		}
		set
		{
			if (value)
			{
				interruptFlag |= 1 << 4;
			}
			else
			{
				interruptFlag &= 0xff ^ (1 << 4);
			}
			joypadIrqReq = value;
		}
	}
}
