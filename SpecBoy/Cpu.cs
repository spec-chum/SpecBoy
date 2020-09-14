using System;
using System.Runtime.InteropServices;

namespace SpecBoy
{
	class Cpu
	{
		[StructLayout(LayoutKind.Explicit)]
		private struct Reg16
		{
			[FieldOffset(0)] public ushort R16;
			[FieldOffset(0)] public byte R8Low;
			[FieldOffset(1)] public byte R8High;

			public static implicit operator ushort(Reg16 reg)
			{
				return reg.R16;
			}
		}

		// Flags - discrete, so we're not wasting cycles on bitwise ops
		private bool Zero;
		private bool Negative;
		private bool HalfCarry;
		private bool Carry;

		// Registers
		private Reg16 af;
		private Reg16 bc;
		private Reg16 de;
		private Reg16 hl;

		// Only really needed for PUSH AF and POP AF
		public ushort AF
		{
			get
			{
				int flags = Convert.ToInt32(Zero) << 7;
				flags += Convert.ToInt32(Negative) << 6;
				flags += Convert.ToInt32(HalfCarry) << 5;
				flags += Convert.ToInt32(Carry) << 4;

				af.R8Low = (byte)flags;

				return af.R16;
			}

			set
			{
				af.R16 = value;

				Zero = (value & 0x80) != 0;
				Negative = (value & 0x40) != 0;
				HalfCarry = (value & 0x20) != 0;
				Carry = (value & 0x10) != 0;
			}
		}

		public byte A { get => af.R8High; set => af.R8High = value; }

		public ushort BC { get => bc; set => bc.R16 = value; }
		public byte B { get => bc.R8High; set => bc.R8High = value; }
		public byte C { get => bc.R8Low; set => bc.R8Low = value; }

		public ushort DE { get => de; set => de.R16 = value; }
		public byte D { get => de.R8High; set => de.R8High = value; }
		public byte E { get => de.R8Low; set => de.R8Low = value; }

		public ushort HL { get => hl; set => hl.R16 = value; }
		public byte H { get => hl.R8High; set => hl.R8High = value; }
		public byte L { get => hl.R8Low; set => hl.R8Low = value; }

		public ushort PC { get; set; }
		public ushort SP { get; set; }

		public int Cycles { get; set; }

		public Cpu()
		{
			AF = 0;
			BC = 0;
			DE = 0;
			HL = 0;
			PC = 0;
			SP = 0xffff;
		}

		private byte IncR8(int reg)
		{
			byte result = GetR8(reg);
			result++;

			Zero = result == 0;
			HalfCarry = (result & 0xf) == 0;
			Negative = false;

			return result;
		}

		private byte DecR8(int reg)
		{
			byte result = GetR8(reg);
			result--;

			Zero = result == 0;
			HalfCarry = (result & 0xf) == 0xf;
			Negative = true;

			return result;
		}

		private ushort IncR16(int reg)
		{
			ushort result = GetR16(reg);
			return result++;
		}

		private ushort DecR16(int reg)
		{
			ushort result = GetR16(reg);
			return result--;
		}

		private ushort GetR16(int reg)
		{
			return reg switch
			{
				0 => BC,
				1 => DE,
				2 => HL,
				_ => SP
			};
		}

		private void SetR16(int reg, ushort value)
		{
			switch (reg)
			{
				case 0:
					BC = value;
					break;

				case 1:
					DE = value;
					break;

				case 2:
					HL = value;
					break;

				default:
					SP = value;
					break;
			}
		}

		private byte GetR8(int reg)
		{
			return reg switch
			{
				0 => B,
				1 => C,
				2 => D,
				3 => E,
				4 => H,
				5 => L,
				//6 => ram[hl]
				_ => A
			};
		}

		private void SetR8(int reg, byte value)
		{
			switch (reg)
			{
				case 0:
					B = value;
					break;

				case 1:
					C = value;
					break;

				case 2:
					D = value;
					break;

				case 3:
					E = value;
					break;

				case 4:
					H = value;
					break;

				case 5:
					L = value;
					break;

				case 6:
					//(HL) = value;
					break;

				default:
					A = value;
					break;
			}
		}


		// ZNHC
		public void Decode()
		{
			byte opcode = 1;

			int registerId;

			// Helper fucntion
			//int ShiftAndIsolateBits(int bits, int mask) => (opcode >> bits) & mask;

			switch (opcode)
			{	
				// NOP
				case 0x00:
					Cycles += 1;
					break;

				// INC R16
				case 0x03:
				case 0x13:
				case 0x23:
				case 0x33:
					registerId = (opcode >> 4) & 0x03;
					SetR16(registerId, IncR16(registerId));
					Cycles += 2;
					break;
				
				// INC R8
				case 0x04:
				case 0x14:
				case 0x24:
				case 0x34:
				case 0x0c:
				case 0x1c:
				case 0x2c:
				case 0x3c:
					registerId = (opcode >> 3) & 0x07;
					SetR8(registerId, IncR8(registerId));
					Cycles += 1;
					break;

				// DEC R8
				case 0x05:
				case 0x15:
				case 0x25:
				case 0x35:
				case 0x0d:
				case 0x1d:
				case 0x2d:
				case 0x3d:
					registerId = (opcode >> 3) & 0x07;
					SetR8(registerId, DecR8(registerId));
					Cycles += 1;
					break;

				// DEC R16
				case 0x0b:
				case 0x1b:
				case 0x2b:
				case 0x3b:
					registerId = (opcode >> 4) & 0x03;
					SetR16(registerId, DecR16(registerId));
					Cycles += 2;
					break;

				// DAA
				case 0x27:
					byte adjustment = 0;

					if (Carry || (A > 0x99 && !Negative))
					{
						adjustment = 0x60;
						Carry = true;
					}

					if (HalfCarry || ((A & 0x0f) > 0x09 && !Negative))
					{
						adjustment += 0x06;
					}

					A += Negative ? (byte)-adjustment : adjustment;

					Zero = A == 0;
					HalfCarry = false;
					Cycles += 1;
					break;

				// CPL
				case 0x2f:
					A = (byte)~A;
					Negative = true;
					HalfCarry = true;
					Cycles += 1;
					break;

				// SCF
				case 0x37:
					Negative = false;
					HalfCarry = false;
					Carry = true;
					Cycles += 1;
					break;

				// CCF
				case 0x3f:
					Negative = false;
					HalfCarry = false;
					Carry = !Carry;
					Cycles += 1;
					break;

				// LD R8, R8
				case var n when n >= 0x40 && n <= 0x7f && n != 0x76:
					registerId = (opcode >> 3) & 0x07;
					SetR8(registerId, GetR8(opcode & 0x07));
					Cycles += 1;
					break;

				// HALT
				case 0x76:
					throw new InvalidOperationException("Unimplemented instruction");

				// JP HL
				case 0xe9:
					PC = HL;
					Cycles += 1;
					break;

				default:
					throw new InvalidOperationException("Unimplemented instruction");
			}
		}
	}
}
