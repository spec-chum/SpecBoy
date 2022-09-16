using CommunityToolkit.HighPerformance;
using System.Runtime.CompilerServices;

namespace SpecBoy;

public static class ExtensionMethods
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBitSet(this byte value, int bit)
	{
		return (value & (1 << bit)) != 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBitSet(this ushort value, int bit)
	{
		return (value & (1 << bit)) != 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte SetBit(this byte value, int bit)
	{
		return (byte)(value | (1 << bit));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte ClearBit(this byte value, int bit)
	{
		return (byte)(value & (~(1 << bit)));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ToBytePower(this bool value, int bitShiftAmount)
	{
		return value.ToByte() << bitShiftAmount;
	}
}
