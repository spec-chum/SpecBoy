namespace SpecBoy
{
	static class Utility
	{
		public static bool IsBitSet(byte value, int bit)
		{
			return (value & (1 << bit)) != 0;
		}

		public static byte SetBit(byte value, int bit)
		{
			return (byte)(value | (1 << bit));
		}

		public static byte ClearBit(byte value, int bit)
		{
			return (byte)(value & (~(1 << bit)));
		}
	}
}
