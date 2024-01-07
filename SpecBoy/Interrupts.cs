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

	public const ushort JoypadIrqVector = 0x60;
	public const int JoypadIeBit = 4;

	private static byte interruptFlag;
	private static byte interruptEnable;

	// We can't use auto properties here as we use exention methods on the bytes, so disable warnings
#pragma warning disable RCS1085, IDE0032 // Use auto-implemented property
	public static byte IE
	{
		get => interruptEnable;
		set => interruptEnable = value;
	}

	public static byte IF
	{
		get => interruptFlag;
		set => interruptFlag = value;
	}
#pragma warning restore RCS1085, IDE0032 // Use auto-implemented property

	public static bool VBlankIrqReq
	{
		get => interruptFlag.IsBitSet(VBlankIeBit);
		set => interruptFlag.SetBitToValue(VBlankIeBit, value);
	}

	public static bool StatIrqReq
	{
		get => interruptFlag.IsBitSet(StatIeBit);
		set => interruptFlag.SetBitToValue(StatIeBit, value);
	}

	public static bool TimerIrqReq
	{
		get => interruptFlag.IsBitSet(TimerIeBit);
		set => interruptFlag.SetBitToValue(TimerIeBit, value);
	}

	public static bool SerialIrqReq
	{
		get => interruptFlag.IsBitSet(SerialIeBit);
		set => interruptFlag.SetBitToValue(	SerialIeBit, value);
	}

	public static bool JoypadIrqReq
	{
		get => interruptFlag.IsBitSet(JoypadIeBit);
		set => interruptFlag.SetBitToValue(JoypadIeBit, value);
	}
}
