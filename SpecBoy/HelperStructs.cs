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

#pragma warning restore CA1051 // Do not declare visible instance fields
