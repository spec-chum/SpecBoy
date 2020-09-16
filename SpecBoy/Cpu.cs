using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

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

		private readonly Memory mem;

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
		public byte F
		{
			get
			{
				int flags = Convert.ToInt32(Zero) << 7;
				flags += Convert.ToInt32(Negative) << 6;
				flags += Convert.ToInt32(HalfCarry) << 5;
				flags += Convert.ToInt32(Carry) << 4;

				af.R8Low = (byte)flags;

				return af.R8Low;
			}
		}

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

		public Cpu(Memory mem)
		{
			this.mem = mem;

			AF = 0x01b0;
			BC = 0x0013;
			DE = 0x00d8;
			HL = 0x014d;
			PC = 0x100;
			SP = 0xfffe;
		}

		private byte IncR8(int reg)
		{
			byte result = GetR8(reg);
			result++;

			Zero = result == 0;
			HalfCarry = (result & 0x0f) == 0;
			Negative = false;

			return result;
		}

		private byte DecR8(int reg)
		{
			byte result = GetR8(reg);
			result--;

			Zero = result == 0;
			HalfCarry = (result & 0x0f) == 0x0f;
			Negative = true;

			return result;
		}

		private ushort IncR16(int reg)
		{
			ushort result = GetR16(reg);
			return (ushort)(result + 1);
		}

		private ushort DecR16(int reg)
		{
			ushort result = GetR16(reg);
			return (ushort)(result - 1);
		}

		private ushort GetR16(int reg, bool usesSP = true)
		{
			if (usesSP)
			{
				return reg switch
				{
					0 => BC,
					1 => DE,
					2 => HL,
					_ => SP
				};
			}

			else
			{
				return reg switch
				{
					0 => BC,
					1 => DE,
					2 => HL,
					_ => AF
				};
			}
		}

		private void SetR16(int reg, ushort value, bool usesSP = true)
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
					if (usesSP)
					{
						SP = value;
					}
					else
					{
						AF = value;
					}
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
				6 => ReadByte(HL),
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
					WriteByte(HL, value);
					break;

				default:
					A = value;
					break;
			}
		}


		private byte ReadByte(int address)
		{
			Cycles++;
			return mem.ReadByte(address);
		}

		private ushort ReadWord(int address)
		{
			Cycles += 2;
			return mem.ReadWord(address);
		}

		private byte ReadNextByte()
		{
			Cycles++;
			var num = mem.ReadByte(PC);
			PC++;
			return num;
		}

		private ushort ReadNextWord()
		{
			Cycles += 2;
			var num = mem.ReadWord(PC);
			PC += 2;
			return num;
		}

		private void WriteByte(int address, byte value)
		{
			Cycles++;
			mem.WriteByte(address, value);
		}

		private void WriteWord(int address, ushort value)
		{
			Cycles += 2;
			mem.WriteWord(address, value);
		}

		private void Push(int r16)
		{
			SP -= 2;
			WriteWord(SP, GetR16(r16, false));
		}

		private void PushPC()
		{
			SP -= 2;
			WriteWord(SP, PC);
		}

		private ushort Pop()
		{
			var address = ReadWord(SP);
			SP += 2;
			return address;
		}

		private void Pop(int r16)
		{
			SetR16(r16, ReadWord(SP), false);
			SP += 2;
		}

		private void Call(int opcode)
		{
			ushort address = ReadNextWord();

			// Handle unconditional CALL or test condition
			if (opcode == 0xcd || TestCondition(opcode))
			{
				PushPC();
				PC = address;
			}
		}

		private void Jp(int opcode)
		{
			ushort address = ReadNextWord();

			// Handle unconditional JP or test condition
			if (opcode == 0xc3 || TestCondition(opcode))
			{
				PC = address;
			}
		}

		private void Jr(int opcode)
		{
			sbyte offset = (sbyte)ReadNextByte();

			// Handle unconditional JR or test condition
			if (opcode == 0x18 || TestCondition(opcode))
			{
				PC += (ushort)offset;
			}
		}

		private void Ret(int opcode)
		{
			// Handle unconditional RET or test condition
			if (opcode == 0xc9 || TestCondition(opcode))
			{
				PC = Pop();
			}
		}

		private bool TestCondition(int opcode)
		{
			 // 0 = NZ 1 = Z 2 = NC 3 = C

			return ((opcode >> 3) & 0x03) switch
			{
				0 => !Zero,
				1 => Zero,
				2 => !Carry,
				3 => Carry,
				_ => false
			};
		}

		private void Add(byte value)
		{
			byte result = (byte)(A + value);

			Zero = result == 0;
			Negative = false;
			HalfCarry = (A & 0x0f) + (value & 0x0f) > 0x0f;
			Carry = result < A;

			A = result;
		}

		private void Adc(byte value)
		{
			byte result = (byte)((A + value) + (Carry == true ? 1 : 0));

			Zero = result == 0;
			Negative = false;
			HalfCarry = ((result - value) & 0x0f) + (value & 0x0f) > 0x0f;
			Carry = result < A;

			A = result;
		}

		private void AddHl(int reg)
		{
			ushort result = (ushort)(HL + GetR16(reg));
			
			Negative = false;
			HalfCarry = (HL & 0xfff) + (reg & 0xfff) > 0xfff;
			Carry = result < HL;

			HL = result;
		}

		private byte Cp(byte value)
		{
			byte result = (byte)(A - value);

			Zero = result == 0;
			Negative = true;
			HalfCarry = (A & 0x0f) < (value & 0x0f);
			Carry = result > A;

			return result;
		}

		private void Sub(byte value)
		{
			A = Cp(value);
		}

		private void And(byte value)
		{
			A &= value;

			Zero = A == 0;
			Negative = false;
			HalfCarry = true;
			Carry = false;
		}

		private void Or(byte value)
		{
			A |= value;

			Zero = A == 0;
			Negative = false;
			HalfCarry = false;
			Carry = false;
		}

		private void Xor(byte value)
		{
			A ^= value;

			Zero = A == 0;
			Negative = false;
			HalfCarry = false;
			Carry = false;
		}

		private void Bit(int reg, int bit)
		{			
			Zero = (reg & (1 << bit)) == 0;
			Negative = false;
			HalfCarry = true;
		}

		private byte Srl(int reg)
		{
			reg >>= 1;
			Zero = reg == 0;
			Negative = false;
			HalfCarry = false;
			Carry = (reg & 1) == 1;

			return (byte)reg;
		}

		private byte Rr(int reg)
		{
			var cc = Carry;

			Carry = (reg & 1) == 1;
			reg = (byte)((reg >> 1) | (cc ? (1 << 7) : 0));

			Zero = reg == 0;
			Negative = false;
			HalfCarry = false;

			return (byte)reg;
		}

		private byte Swap(int reg)
		{
			reg = (reg >> 4) | (reg << 4);

			Zero = reg == 0;
			Negative = false;
			HalfCarry = false;
			Carry = false;

			return (byte)reg;
		}

		public void Execute()
		{
			int registerId;
			byte opcode = ReadNextByte();

			// Helper function
			//int ShiftAndIsolateBits(int bits, int mask) => (opcode >> bits) & mask;

			switch (opcode)
			{	
				// NOP
				case 0x00:
					break;

				// LD R16, u16
				case 0x01:
				case 0x11:
				case 0x21:
				case 0x31:
					registerId = (opcode >> 4) & 0x03;
					SetR16(registerId, ReadNextWord());
					break;

				// LD (BC), A
				case 0x02:
					WriteByte(BC, A);
					break;

				// LD (DE), A
				case 0x12:
					WriteByte(DE, A);
					break;

				// LD (HL+), A
				case 0x22:
					WriteByte(HL, A);
					HL++;
					break;

				// LD (HL-), A
				case 0x32:
					WriteByte(HL, A);
					HL--;
					break;

				// INC R16
				case 0x03:
				case 0x13:
				case 0x23:
				case 0x33:
					registerId = (opcode >> 4) & 0x03;
					SetR16(registerId, IncR16(registerId));
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
					break;

				// LD R8, u8
				case 0x06:
				case 0x16:
				case 0x26:
				case 0x36:
				case 0x0e:
				case 0x1e:
				case 0x2e:
				case 0x3e:
					registerId = (opcode >> 3) & 0x07;
					SetR8(registerId, ReadNextByte());
					break;

				// ADD HL, R16
				case 0x09:
				case 0x19:
				case 0x29:
				case 0x39:
					registerId = (opcode >> 4) & 0x03;
					AddHl(registerId);
					break;

				// DEC R16
				case 0x0b:
				case 0x1b:
				case 0x2b:
				case 0x3b:
					registerId = (opcode >> 4) & 0x03;
					SetR16(registerId, DecR16(registerId));
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
					break;

				// LD A, (BC)
				case 0x0a:
					A = ReadByte(BC);
					break;

				// LD A, (DE)
				case 0x1a:
					A = ReadByte(DE);
					break;

				// RRA - same as RR A except Zero always false
				case 0x1f:
					A = Rr(A);
					Zero = false;
					break;
					
					// LD A, (HL+)
				case 0x2a:
					A = ReadByte(HL);
					HL++;
					break;

				// LD A, (HL-)
				case 0x3a:
					A = ReadByte(HL);
					HL--;
					break;

				// JR
				case 0x18:
				case 0x28:
				case 0x38:
				case 0x20:
				case 0x30:
					Jr(opcode);
					break;

				// CPL
				case 0x2f:
					A = (byte)~A;
					Negative = true;
					HalfCarry = true;
					break;

				// SCF
				case 0x37:
					Negative = false;
					HalfCarry = false;
					Carry = true;
					break;

				// CCF
				case 0x3f:
					Negative = false;
					HalfCarry = false;
					Carry = !Carry;
					break;

				// LD R8, R8
				case var n when n >= 0x40 && n <= 0x7f && n != 0x76:
					registerId = (opcode >> 3) & 0x07;
					SetR8(registerId, GetR8(opcode & 0x07));
					break;

				// ADD A, r8
				case var n when n >= 0x80 && n <= 0x87:
					Add(GetR8(opcode & 0x07));
					break;

				// SUB A, r8
				case var n when n >= 0x90 && n <= 0x97:
					Sub(GetR8(opcode & 0x07));
					break;

				// XOR A, R8
				case var n when n >= 0xa8 && n <= 0xaf:
					Xor(GetR8(opcode & 0x07));
					break;

				// OR A, R8
				case var n when n >= 0xb0 && n <= 0xb7:
					Or(GetR8(opcode & 0x07));
					break;

				// CP r8
				case var n when n >= 0xb8 && n <= 0xbf:
					Cp(GetR8(opcode & 0x07));
					break;

				// POP R16
				case 0xc1:
				case 0xd1:
				case 0xe1:
				case 0xf1:
					registerId = (opcode >> 4) & 0x03;
					Pop(registerId);
					break;

				// JP
				case 0xc2:
				case 0xc3:
				case 0xd2:
				case 0xca:
				case 0xda:
					Jp(opcode);
					break;

				// CALL
				case 0xc4:
				case 0xd4:
				case 0xcc:
				case 0xdc:
				case 0xcd:
					Call(opcode);
					break;

				// PUSH R16
				case 0xc5:
				case 0xd5:
				case 0xe5:
				case 0xf5:
					registerId = (opcode >> 4) & 0x03;
					Push(registerId);
					break;

				// ADD A, R8
				case var n when n >= 0xb0 && n <= 0xb7:
					Add(GetR8(opcode & 0x7));
					break;

				// ADD A, u8
				case 0xc6:
					Add(ReadNextByte());
					break;


				// RET
				case 0xc0:
				case 0xd0:
				case 0xc8:
				case 0xc9:
				case 0xd8:
					Ret(opcode);
					break;

				// CB Prefix
				case 0xcb:
					opcode = ReadNextByte();

					switch (opcode)
					{
						// RR r8
						case var n when n >= 0x18 && n <= 0x1f:
							registerId = opcode & 0x07;
							SetR8(registerId, Rr(GetR8(registerId)));
							break;

						// Swap r8
						case var n when n >= 0x30 && n <= 0x37:
							registerId = opcode & 0x07;
							SetR8(registerId, Swap(GetR8(registerId)));
							break;

						// SRL R8
						case var n when n >= 0x38 && n <= 0x3f:
							registerId = opcode & 0x07;
							SetR8(registerId, Srl(GetR8(registerId)));
							break;

						// BIT
						case var n when n >= 0x40 && n <= 0x7f:
							Bit(GetR8(opcode & 0x7), (opcode >> 3) & 0x07);
							break;

						default:
							throw new InvalidOperationException("Unimplemented CB instruction");
					}
					break;

				// ADC A, u8
				case 0xce:
					Adc(ReadNextByte());
					break;

				// SUB A, u8
				case 0xd6:
					Sub(ReadNextByte());
					break;

				// LD (FF00 + u8), A
				case 0xe0:
					WriteByte(0xff00 + ReadNextByte(), A);
					break;

				// AND A, u8
				case 0xe6:
					And(ReadNextByte());
					break;

				// JP HL
				case 0xe9:
					PC = HL;
					break;

				// LD (U16), A
				case 0xea:
					WriteByte(ReadNextWord(), A);
					break;

				// XOR A, u8
				case 0xee:
					Xor(ReadNextByte());
					break;

				// LD A, (FF00 + u8)
				case 0xf0:
					A = ReadByte(0xff00 + ReadNextByte());
					break;

				// Todo: EI DI - just break for now
				case 0xf3:
				case 0xfb:
					break;

				// OR A, u16
				case 0xf6:
					Or(ReadNextByte());
					break;

				// LD A, (u16)
				case 0xfa:
					A = ReadByte(ReadNextWord());
					break;

				// CP A, u8
				case 0xfe:
					Cp(ReadNextByte());
					break;

				default:
					throw new InvalidOperationException("Unimplemented instruction");
			}
		}
	}
}
