using CommunityToolkit.HighPerformance.Helpers;

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

	private static uint interruptFlag;
	private static uint interruptEnable;

	public static byte IE
	{
		get => (byte)interruptEnable;
		set => interruptEnable = value;
	}

	public static byte IF
	{
		get => (byte)interruptFlag;
		set => interruptFlag = value;
	}

	public static bool VBlankIrqReq
	{
		get => interruptFlag.IsBitSet(VBlankIeBit);
		set => BitHelper.SetFlag(ref interruptFlag, VBlankIeBit, value);
	}

	public static bool StatIrqReq
	{
		get => interruptFlag.IsBitSet(StatIeBit);
		set => BitHelper.SetFlag(ref interruptFlag, StatIeBit, value);
	}

	public static bool TimerIrqReq
	{
		get => interruptFlag.IsBitSet(TimerIeBit);
		set => BitHelper.SetFlag(ref interruptFlag, TimerIeBit, value);
	}

	public static bool SerialIrqReq
	{
		get => interruptFlag.IsBitSet(SerialIeBit);
		set => BitHelper.SetFlag(ref interruptFlag, SerialIeBit, value);
	}

	public static bool JoypadIrqReq
	{
		get => interruptFlag.IsBitSet(JoypadIeBit);
		set => BitHelper.SetFlag(ref interruptFlag, JoypadIeBit, value);
	}
}
