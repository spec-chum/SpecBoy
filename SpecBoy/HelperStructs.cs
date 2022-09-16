using System.Runtime.InteropServices;

namespace SpecBoy;

[StructLayout(LayoutKind.Explicit)]
public struct Reg16
{
	[FieldOffset(0)] public ushort R16;
	[FieldOffset(0)] public byte R8Low;
	[FieldOffset(1)] public byte R8High;
}

[StructLayout(LayoutKind.Explicit)]
public readonly struct Mem16
{
	[FieldOffset(0)] public readonly ushort fullWord;
	[FieldOffset(0)] public readonly byte lowByte;
	[FieldOffset(1)] public readonly byte highByte;
}

[StructLayout(LayoutKind.Explicit)]
public readonly struct PackedBytes32
{
	[FieldOffset(0)] public readonly uint allBytes;
	[FieldOffset(0)] public readonly byte firstByte;
	[FieldOffset(1)] public readonly byte secondByte;
	[FieldOffset(2)] public readonly byte thirdByte;
	[FieldOffset(3)] public readonly byte forthByte;
}
