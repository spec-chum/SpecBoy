namespace SpecBoy;

static class Interrupts
{
	public const int VBlankBit = 0;
	public const int StatBit = 1;
	public const int TimerBit = 2;
	public const int SerialBit = 3;
	public const int JoypadBit = 4;

	public const ushort VBlankIrqVector = 0x40;
	public const ushort StatIrqVector = 0x48;
	public const ushort TimerIrqVector = 0x50;
	public const ushort SerialIrqVector = 0x58;
	public const ushort JoypadIrqVector = 0x60;

	public static byte IE;
	public static byte IF;
}
