using System.Runtime.InteropServices;

namespace SpecBoy;

#pragma warning disable CA1051 // Do not declare visible instance fields
[StructLayout(LayoutKind.Explicit)]
public struct Reg16
{
	[FieldOffset(0)] public ushort R16;
	[FieldOffset(0)] public Anon R8;

	public struct Anon
	{
		public byte Low;
		public byte High;
	}
}

[StructLayout(LayoutKind.Explicit)]
public readonly struct Mem16
{
	[FieldOffset(0)] public readonly ushort FullWord;
	[FieldOffset(0)] public readonly Anon Bytes;

	public readonly struct Anon
	{
		public readonly byte Low;
		public readonly byte High;
	}
}

[StructLayout(LayoutKind.Explicit)]
public readonly struct Mem32
{
	[FieldOffset(0)] public readonly uint FullDWord;
	[FieldOffset(0)] public readonly Anon Bytes;

	public readonly struct Anon
	{
		public readonly byte First;
		public readonly byte Second;
		public readonly byte Third;
		public readonly byte Forth;
	}
}
#pragma warning restore CA1051 // Do not declare visible instance fields
