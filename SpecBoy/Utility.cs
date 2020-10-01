using System.Runtime.CompilerServices;

namespace SpecBoy
{
	static class Utility
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsBitSet(byte value, int bit)
		{
			return (value & (1 << bit)) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsBitSet(ushort value, int bit)
		{
			return (value & (1 << bit)) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte SetBit(byte value, int bit)
		{
			return (byte)(value | (1 << bit));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetBit(ref byte value, int bit)
		{
			value |= (byte)(1 << bit);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte ClearBit(byte value, int bit)
		{
			return (byte)(value & (~(1 << bit)));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearBit(ref byte value, int bit)
		{
			value &= (byte)~(1 << bit);
		}
	}
}
