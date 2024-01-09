using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Helpers;
using System.Runtime.CompilerServices;

namespace SpecBoy;

public static class ExtensionMethods
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBitSet(this byte value, int bit)
	{
		return IsBitSet((uint)value, bit);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBitSet(this ushort value, int bit)
	{
		return IsBitSet((uint)value, bit);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBitSet(this uint value, int bit)
	{
		return BitHelper.HasFlag(value, bit);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void SetBit(ref this byte value, int bit)
	{
		value |= (byte)(1 << bit);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ResetBit(ref this byte value, int bit)
	{
		value &= (byte)~(1 << bit);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte ToBytePower(this bool value, int bitShiftAmount)
	{
		return (byte)(value.ToByte() << bitShiftAmount);
	}
}
