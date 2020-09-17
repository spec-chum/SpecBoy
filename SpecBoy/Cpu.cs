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
				UpdateFlags();
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
			// Don't want F being altered directly, so only use getter
			get
			{
				UpdateFlags();
				return af.R8Low;
			}
		}

		private void UpdateFlags()
		{
			int flags = Zero ? 1 << 7 : 0;
			flags |= Negative ? 1 << 6 : 0;
			flags |= HalfCarry ? 1 << 5 : 0;
			flags |= Carry ? 1 << 4 : 0;

			af.R8Low = (byte)flags;
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
			Cycles++;
			return (ushort)(GetR16(reg) + 1);
		}

		private ushort DecR16(int reg)
		{
			Cycles++;
			return (ushort)(GetR16(reg) - 1);
		}

		// Can get either SP or AF depening on bool
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
		// Can set either SP or AF depening on bool
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

		private void Push(ushort address)
		{
			Cycles++;
			SP -= 2;
			WriteWord(SP, address);
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
				Cycles++;
				Push(PC);
				PC = address;
			}
		}

		private void Jp(int opcode)
		{
			ushort address = ReadNextWord();

			// Handle unconditional JP or test condition
			if (opcode == 0xc3 || TestCondition(opcode))
			{
				Cycles++;
				PC = address;
			}
		}

		private void Jr(int opcode)
		{
			sbyte offset = (sbyte)ReadNextByte();

			// Handle unconditional JR or test condition
			if (opcode == 0x18 || TestCondition(opcode))
			{
				Cycles++;
				PC += (ushort)offset;
			}
		}

		private void Ret(int opcode)
		{
			Cycles++;

			var condition = TestCondition(opcode);

			// Handle unconditional RET or test condition
			if (opcode == 0xc9 || condition)
			{
				// Add extra cycle if conditional
				Cycles += condition ? 1 : 0;
				PC = Pop();
			}
		}

		private void Reti()
		{
			Ei();
			PC = Pop();
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
				_ => false	// Never reached
			};
		}

		private void Ei()
		{

		}

		private void Daa()
		{
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
			int result = (A + value + (Carry ? 1 : 0));

			Zero = (byte)result == 0;
			Negative = false;
			HalfCarry = (A & 0x0f) + (value & 0x0f) + (Carry ? 1 : 0) > 0x0f;
			Carry = result > 255;

			A = (byte)result;
		}

		private void AddHl(int reg)
		{
			Cycles++;

			ushort value = GetR16(reg);
			ushort result = (ushort)(HL + value);

			Negative = false;
			HalfCarry = (HL & 0xfff) + (value & 0xfff) > 0xfff;
			Carry = result < HL;

			HL = result;
		}

		private void LdHlSp(byte i8)
		{
			Cycles++;

			Zero = false;
			Negative = false;
			HalfCarry = ((SP + i8) & 0x0f) < (SP & 0x0f);
			Carry = ((SP + i8) & 0xff) < (SP & 0xff);

			HL = (ushort)(SP + (sbyte)i8);
		}

		private void AddSp(byte i8)
		{
			Cycles += 2;

			Zero = false;
			Negative = false;
			HalfCarry = ((SP + i8) & 0x0f) < (SP & 0x0f);
			Carry = ((SP + i8) & 0xff) < (SP & 0xff);

			SP += (ushort)(sbyte)i8;
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

		private void Sbc(byte value)
		{
			int result = (A - value - (Carry ? 1 : 0));

			Zero = (byte)result == 0;
			Negative = true;
			HalfCarry = ((A & 0x0f) - (Carry ? 1 : 0) < (value & 0x0f));
			Carry = result < 0;

			A = (byte)result;
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

		private void Bit(byte value, int bit)
		{			
			Zero = (value & (1 << bit)) == 0;
			Negative = false;
			HalfCarry = true;
		}

		private byte Srl(byte value)
		{
			Carry = (value & 0x01) != 0;
			value >>= 1;
			Zero = value == 0;
			Negative = false;
			HalfCarry = false;

			return value;
		}

		private byte Rl(byte value)
		{
			var cc = Carry;

			Carry = (value & 0x80) != 0;
			value = (byte)((value << 1) | (cc ? 1 : 0));

			Zero = value == 0;
			Negative = false;
			HalfCarry = false;

			return value;
		}

		private byte Rr(byte value)
		{
			var cc = Carry;

			Carry = (value & 1) == 1;
			value = (byte)((value >> 1) | (cc ? (1 << 7) : 0));

			Zero = value == 0;
			Negative = false;
			HalfCarry = false;

			return value;
		}

		private byte Rlc(byte value)
		{
			Carry = (value & 0x80) != 0;
			value = (byte)((value << 1) | (value >> 7));

			Zero = value == 0;
			Negative = false;
			HalfCarry = false;

			return value;
		}

		private byte Rrc(byte value)
		{
			Carry = (value & 0x01) != 0;
			value = (byte)((value >> 1) | (value << 7));

			Zero = value == 0;
			Negative = false;
			HalfCarry = false;

			return value;
		}

		private byte Sla(byte value)
		{
			Carry = (value & 0x80) != 0;
			value <<= 1;

			Zero = value == 0;
			Negative = false;
			HalfCarry = false;

			return value;
		}

		private byte Sra(byte value)
		{
			Carry = (value & 0x01) != 0;
			value = (byte)((value >> 1) | (value & 0x80));

			Zero = value == 0;
			Negative = false;
			HalfCarry = false;

			return value;
		}

		private byte Swap(byte value)
		{
			value = (byte)((value >> 4) | (value << 4));

			Zero = value == 0;
			Negative = false;
			HalfCarry = false;
			Carry = false;

			return value;
		}

		private byte Res(byte value, int bit)
		{
			return (byte)(value & (~(1 << bit)));
		}

		private byte Set(byte value, int bit)
		{
			return (byte)(value | (1 << bit));
		}

		public void Execute()
		{
			int registerId;
			byte opcode = ReadNextByte();

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

				// RLCA - Same as RLC A but Zero always false
				case 0x07:
					A = Rlc(A);
					Zero = false;
					break;

				// LD (u16), SP
				case 0x08:
					WriteWord(ReadNextWord(), SP);
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

				// STOP
				case 0x10:
					break;

				// DAA
				case 0x27:
					Daa();
					break;

				// LD A, (BC)
				case 0x0a:
					A = ReadByte(BC);
					break;

				// RRCA - same as RRC A except Zero always false
				case 0xf:
					A = Rrc(A);
					Zero = false;
					break;

				// RLA  - same as RL A except Zero always false
				case 0x17:
					A = Rl(A);
					Zero = false;
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

				// ADC A, r8
				case var n when n >= 0x88 && n <= 0x8f:
					Adc(GetR8(opcode & 0x07));
					break;

				// SUB A, r8
				case var n when n >= 0x90 && n <= 0x97:
					Sub(GetR8(opcode & 0x07));
					break;

				// SBC A, r8
				case var n when n >= 0x98 && n <= 0x9f:
					Sbc(GetR8(opcode & 0x07));
					break;

				// AND A, r8
				case var n when n >= 0xa0 && n <= 0xa7:
					And(GetR8(opcode & 0x07));
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
					Push(GetR16(registerId, false));
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

				// RST XX
				case 0xc7:
				case 0xd7:
				case 0xe7:
				case 0xf7:
				case 0xcf:
				case 0xdf:
				case 0xef:
				case 0xff:
					Push(PC);
					PC = ((ushort)(opcode & 0x38));
					break;

				// CB Prefix
				case 0xcb:
					opcode = ReadNextByte();

					switch (opcode)
					{
						// RLC r8
						case var n when n >= 0x00 && n <= 0x07:
							registerId = opcode & 0x07;
							SetR8(registerId, Rlc(GetR8(registerId)));
							break;

						// RRC r8
						case var n when n >= 0x08 && n <= 0x0f:
							registerId = opcode & 0x07;
							SetR8(registerId, Rrc(GetR8(registerId)));
							break;

						// RL r8
						case var n when n >= 0x10 && n <= 0x17:
							registerId = opcode & 0x07;
							SetR8(registerId, Rl(GetR8(registerId)));
							break;

						// RR r8
						case var n when n >= 0x18 && n <= 0x1f:
							registerId = opcode & 0x07;
							SetR8(registerId, Rr(GetR8(registerId)));
							break;

						// SLA r8
						case var n when n >= 0x20 && n <= 0x27:
							registerId = opcode & 0x07;
							SetR8(registerId, Sla(GetR8(registerId)));
							break;

						// SRA r8
						case var n when n >= 0x28 && n <= 0x2f:
							registerId = opcode & 0x07;
							SetR8(registerId, Sra(GetR8(registerId)));
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
				
						// RES
						case var n when n >= 0x80 && n <= 0xbf:
							registerId = opcode & 0x07;
							SetR8(registerId, Res(GetR8(registerId), (opcode >> 3) & 0x07));
							break;

						// SET
						case var n when n >= 0xc0 && n <= 0xff:
							registerId = opcode & 0x07;
							SetR8(registerId, Set(GetR8(registerId), (opcode >> 3) & 0x07));
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

				// RETI
				case 0xd9:
					Reti();
					break;

				// SBC A, u8
				case 0xde:
					Sbc(ReadNextByte());
					break;

				// LD (FF00 + u8), A
				case 0xe0:
					WriteByte(0xff00 + ReadNextByte(), A);
					break;

				// LD (FF00 + C), A
				case 0xe2:
					WriteByte(0xff00 + C, A);
					break;

				// AND A, u8
				case 0xe6:
					And(ReadNextByte());
					break;

				// ADD SP, i8
				case 0xe8:
					AddSp(ReadNextByte());
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

				// LD A,(FF00 + C)
				case 0xf2:
					A = ReadByte(0xff00 + C);
					break;

				// Todo: EI DI - just break for now
				case 0xf3:
				case 0xfb:
					break;

				// OR A, u16
				case 0xf6:
					Or(ReadNextByte());
					break;

				// LD HL, SP+i8
				case 0xf8:
					LdHlSp(ReadNextByte());
					break;

				// LD SP, HL
				case 0xf9:
					SP = HL;
					Cycles++;
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
