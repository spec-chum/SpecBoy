namespace SpecBoy
{
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

		public static bool VBlankIrqReq;
		public static bool StatIrqReq;
		public static bool SerialIrqReq;
		public static bool TimerIrqReq;
		public static bool JoypadIrqReq;
	}
}
