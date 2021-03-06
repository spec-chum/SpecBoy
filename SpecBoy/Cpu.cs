﻿using System;
using System.Runtime.InteropServices;

namespace SpecBoy
{
	class Cpu
	{
		private readonly Memory mem;
		private readonly Timers timers;
		private readonly Ppu ppu;

		// Flags - discrete, so we're not wasting cycles on bitwise ops
		private bool zero;
		private bool negative;
		private bool halfCarry;
		private bool carry;

		private bool isHalted;
		private bool haltBug;
		private bool eiDelay;

		// Registers
		private Reg16 af;
		private Reg16 bc;
		private Reg16 de;
		private Reg16 hl;

		// Interrupt Master Enable flag
		private bool ime;

		public Cpu(Memory mem, Ppu ppu, Timers timers)
		{
			this.mem = mem;
			this.timers = timers;
			this.ppu = ppu;

			if (!mem.BootRomEnabled)
			{
				AF = 0x01b0;
				BC = 0x0013;
				DE = 0x00d8;
				HL = 0x014d;
				PC = 0x0100;
				SP = 0xfffe;

				ppu.Lcdc = 0x91;
				ppu.Bgp = 0xfc;
				ppu.Obp0 = 0xff;
				ppu.Obp1 = 0xff;
			}
		}

		// Only really needed for PUSH AF and POP AF
		public ushort AF
		{
			get
			{
				UpdateFlags();
				return af.R16;
			}

			private set
			{
				af.R16 = value;

				zero = (value & 0x80) != 0;
				negative = (value & 0x40) != 0;
				halfCarry = (value & 0x20) != 0;
				carry = (value & 0x10) != 0;
			}
		}

		public byte A { get => af.R8High; private set => af.R8High = value; }
		public byte F
		{
			// Don't want F being altered directly, so only use getter
			get
			{
				UpdateFlags();
				return af.R8Low;
			}
		}

		public ushort BC { get => bc.R16; private set => bc.R16 = value; }
		public byte B { get => bc.R8High; private set => bc.R8High = value; }
		public byte C { get => bc.R8Low; private set => bc.R8Low = value; }

		public ushort DE { get => de.R16; private set => de.R16 = value; }
		public byte D { get => de.R8High; private set => de.R8High = value; }
		public byte E { get => de.R8Low; private set => de.R8Low = value; }

		public ushort HL { get => hl.R16; private set => hl.R16 = value; }
		public byte H { get => hl.R8High; private set => hl.R8High = value; }
		public byte L { get => hl.R8Low; private set => hl.R8Low = value; }

		public ushort PC { get; private set; }

		public ushort SP { get; private set; }

		public long Cycles { get; private set; }

		public long Execute()
		{
			ProcessInterrupts();

			if (isHalted)
			{
				// Emulate NOP, so just tick
				CycleTick();
				return Cycles;
			}

			int regId;
			byte opcode = ReadNextByte();

			if (haltBug)
			{
				haltBug = false;
				PC--;
			}

			if (eiDelay)
			{
				ime = true;
				eiDelay = false;
			}

			// Opcode execute logic
			switch (opcode)
			{	
				// NOP
				case 0x00:
					break;

				// LD r16, u16
				case 0x01:
				case 0x11:
				case 0x21:
				case 0x31:
					regId = (opcode >> 4) & 0x03;
					SetR16(regId, ReadNextWord());
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
					regId = (opcode >> 4) & 0x03;
					SetR16(regId, IncR16(regId));
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
					regId = (opcode >> 3) & 0x07;
					SetR8(regId, IncR8(regId));
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
					regId = (opcode >> 3) & 0x07;
					SetR8(regId, DecR8(regId));
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
					regId = (opcode >> 3) & 0x07;
					SetR8(regId, ReadNextByte());
					break;

				// RLCA - Same as RLC A but Zero always false
				case 0x07:
					A = Rlc(A);
					zero = false;
					break;

				// LD (u16), SP
				case 0x08:
					WriteWord(ReadNextWord(), SP);
					break;

				// ADD HL, r16
				case 0x09:
				case 0x19:
				case 0x29:
				case 0x39:
					regId = (opcode >> 4) & 0x03;
					AddHl(regId);
					break;

				// DEC r16
				case 0x0b:
				case 0x1b:
				case 0x2b:
				case 0x3b:
					regId = (opcode >> 4) & 0x03;
					SetR16(regId, DecR16(regId));
					break;

				// STOP
				case 0x10:
					throw new Exception("STOP HIT!");

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
					zero = false;
					break;

				// RLA  - same as RL A except Zero always false
				case 0x17:
					A = Rl(A);
					zero = false;
					break;

				// LD A, (DE)
				case 0x1a:
					A = ReadByte(DE);
					break;

				// RRA - same as RR A except Zero always false
				case 0x1f:
					A = Rr(A);
					zero = false;
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
					negative = true;
					halfCarry = true;
					break;

				// SCF
				case 0x37:
					negative = false;
					halfCarry = false;
					carry = true;
					break;

				// CCF
				case 0x3f:
					negative = false;
					halfCarry = false;
					carry = !carry;
					break;

				// LD R8, R8
				case var n when n >= 0x40 && n <= 0x7f && n != 0x76:
					regId = (opcode >> 3) & 0x07;
					SetR8(regId, GetR8(opcode & 0x07));
					break;

				// HALT
				case 0x76:
					if (ime || (Interrupts.IE & Interrupts.IF & 0x1f) == 0)
					{
						isHalted = true;
					}
					else
					{
						haltBug = true;
					}
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

				// XOR A, r8
				case var n when n >= 0xa8 && n <= 0xaf:
					Xor(GetR8(opcode & 0x07));
					break;

				// OR A, r8
				case var n when n >= 0xb0 && n <= 0xb7:
					Or(GetR8(opcode & 0x07));
					break;

				// CP r8
				case var n when n >= 0xb8 && n <= 0xbf:
					Cp(GetR8(opcode & 0x07));
					break;

				// POP r16
				case 0xc1:
				case 0xd1:
				case 0xe1:
				case 0xf1:
					regId = (opcode >> 4) & 0x03;
					Pop(regId);
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

				// PUSH r16
				case 0xc5:
				case 0xd5:
				case 0xe5:
				case 0xf5:
					regId = (opcode >> 4) & 0x03;
					Push(GetR16(regId, false));
					break;

				// ADD A, r8
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
					PC = (ushort)(opcode & 0x38);
					break;

				// CB Prefix
				case 0xcb:
					DecodeCB();
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

				// LD (u16), A
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

				// DI
				case 0xf3:
					Di();
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
					CycleTick();
					break;

				// LD A, (u16)
				case 0xfa:
					A = ReadByte(ReadNextWord());
					break;

				// EI
				case 0xfb:
					Ei();
					break;

				// CP A, u8
				case 0xfe:
					Cp(ReadNextByte());
					break;

				default:
					throw new InvalidOperationException($"Unimplemented instruction {opcode:X2} at PC: {PC:X4}");
			}

			return Cycles;
		}

		private void DecodeCB()
		{
			var opcode = ReadNextByte();
			var regId = opcode & 0x07;

			switch (opcode)
			{
				// RLC r8
				case var n when n <= 0x07:
					SetR8(regId, Rlc(GetR8(regId)));
					break;

				// RRC r8
				case var n when n <= 0x0f:
					SetR8(regId, Rrc(GetR8(regId)));
					break;

				// RL r8
				case var n when n <= 0x17:
					SetR8(regId, Rl(GetR8(regId)));
					break;

				// RR r8
				case var n when n <= 0x1f:
					SetR8(regId, Rr(GetR8(regId)));
					break;

				// SLA r8
				case var n when n <= 0x27:
					SetR8(regId, Sla(GetR8(regId)));
					break;

				// SRA r8
				case var n when n <= 0x2f:
					SetR8(regId, Sra(GetR8(regId)));
					break;

				// SWAP r8
				case var n when n <= 0x37:
					SetR8(regId, Swap(GetR8(regId)));
					break;

				// SRL r8
				case var n when n <= 0x3f:
					SetR8(regId, Srl(GetR8(regId)));
					break;

				// BIT
				case var n when n <= 0x7f:
					Bit(GetR8(regId), (opcode >> 3) & 0x07);
					break;

				// RES
				case var n when n <= 0xbf:
					SetR8(regId, Res(GetR8(regId), (opcode >> 3) & 0x07));
					break;

				// SET
				case var n when n <= 0xff:
					SetR8(regId, Set(GetR8(regId), (opcode >> 3) & 0x07));
					break;

				default:
					// Something has gone seriously wrong if we reach here
					throw new InvalidOperationException("Unimplemented CB instruction");
			}
		}

		// INCs cycles and ticks all components
		private void CycleTick()
		{
			Cycles += 4;

			// Check DMA
			mem.DoDma();

			// tick components
			timers.Tick();
			ppu.Tick();
		}

		private void UpdateFlags()
		{
			int flags = zero.ToIntPower(7);
			flags |= negative.ToIntPower(6); 
			flags |= halfCarry.ToIntPower(5);
			flags |= carry.ToIntPower(4);

			af.R8Low = (byte)flags;
		}

		// Can get either SP or AF depending on bool
		private ushort GetR16(int r16, bool usesSP = true) => r16 switch
		{
			0 => BC,
			1 => DE,
			2 => HL,
			3 => usesSP ? SP : AF,
			_ => throw new ArgumentException($"Attempt to get invalid R16 identifier. R16 was {r16}", "r16")
		};

		// Can set either SP or AF depending on bool
		private void SetR16(int r16, ushort value, bool usesSP = true)
		{
			switch (r16)
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

				case 3:
					if (usesSP)
					{
						SP = value;
					}
					else
					{
						AF = value;
					}

					break;

				default:
					throw new ArgumentException($"Attempt to set invalid R16 identifier. R16 was {r16}", "r16");
			}
		}

		private byte GetR8(int r8) => r8 switch
		{
			0 => B,
			1 => C,
			2 => D,
			3 => E,
			4 => H,
			5 => L,
			6 => ReadByte(HL),
			7 => A,
			_ => throw new ArgumentException($"Attempt to get invalid R8 identifier. R8 was {r8}", "r8")
		};

		private void SetR8(int r8, byte value)
		{
			switch (r8)
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

				case 7:
					A = value;
					break;

				default:
					throw new ArgumentException($"Attempt to set invalid R8 identifier. R8 was {r8}", "r8");
			}
		}

		private byte ReadByte(int address)
		{
			byte value = mem.ReadByte(address);
			CycleTick();
			return value;
		}

		private byte ReadNextByte() => ReadByte(PC++);

		private ushort ReadWord(int address) => (ushort)(ReadByte(address) | (ReadByte(address + 1) << 8));

		private ushort ReadNextWord() => (ushort)(ReadNextByte() | ReadNextByte() << 8);

		private void WriteByte(int address, byte value)
		{
			mem.WriteByte(address, value);
			CycleTick();
		}

		private void WriteWord(int address, ushort value)
		{
			WriteByte(address, (byte)value);
			WriteByte(address + 1, (byte)(value >> 8));
		}

		private byte IncR8(int r8)
		{
			byte result = GetR8(r8);
			result++;

			zero = result == 0;
			halfCarry = (result & 0x0f) == 0;
			negative = false;

			return result;
		}

		private byte DecR8(int r8)
		{
			byte result = GetR8(r8);
			result--;

			zero = result == 0;
			halfCarry = (result & 0x0f) == 0x0f;
			negative = true;

			return result;
		}

		private ushort IncR16(int r16)
		{
			ushort result = GetR16(r16);
			result++;

			CycleTick();

			return result;
		}

		private ushort DecR16(int r16)
		{
			ushort result = GetR16(r16);
			result--;

			CycleTick();

			return result;
		}

		private void Push(ushort address)
		{
			CycleTick();

			SP--;
			WriteByte(SP, (byte)(address >> 8));
			SP--;
			WriteByte(SP, (byte)address);
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
				PC = address;
				CycleTick();
			}
		}

		private void Jr(int opcode)
		{
			sbyte offset = (sbyte)ReadNextByte();

			// Handle unconditional JR or test condition
			if (opcode == 0x18 || TestCondition(opcode))
			{
				PC += (ushort)offset;
				CycleTick();
			}
		}

		private void Ret(int opcode)
		{
			if (opcode == 0xc9)
			{
				PC = Pop();
			}			
			else if (TestCondition(opcode))
			{
				CycleTick();
				PC = Pop();
			}

			CycleTick();
		}

		private void Reti()
		{
			ime = true;
			PC = Pop();
			CycleTick();
		}

		private bool TestCondition(int opcode) =>
			// 0 = NZ; 1 = Z; 2 = NC; 3 = C
			((opcode >> 3) & 0x03) switch
			{
				0 => !zero,
				1 => zero,
				2 => !carry,
				3 => carry,
				_ => false  // Never reached
			};

		private void Ei()
		{
			eiDelay = true;
		}

		private void Di()
		{
			ime = false;
		}

		private void Daa()
		{
			byte adjustment = 0;

			if (carry || (A > 0x99 && !negative))
			{
				adjustment = 0x60;
				carry = true;
			}

			if (halfCarry || ((A & 0x0f) > 0x09 && !negative))
			{
				adjustment += 0x06;
			}

			A += negative ? (byte)-adjustment : adjustment;

			zero = A == 0;
			halfCarry = false;
		}

		private void Add(byte value)
		{
			byte result = (byte)(A + value);

			zero = result == 0;
			negative = false;
			halfCarry = (A & 0x0f) + (value & 0x0f) > 0x0f;
			carry = result < A;

			A = result;
		}

		private void Adc(byte value)
		{
			int cc = carry.ToInt();

			int result = (A + value + cc);

			zero = (byte)result == 0;
			negative = false;
			halfCarry = (A & 0x0f) + (value & 0x0f) + cc > 0x0f;
			carry = result > 255;

			A = (byte)result;
		}

		private void AddHl(int r16)
		{
			ushort value = GetR16(r16);
			ushort result = (ushort)(HL + value);

			negative = false;
			halfCarry = (HL & 0xfff) + (value & 0xfff) > 0xfff;
			carry = result < HL;

			HL = result;

			CycleTick();
		}

		private void LdHlSp(byte i8)
		{
			// Cache SP to avoid many calls to getter
			ushort cachedSP = SP;
			ushort result = (ushort)(cachedSP + (sbyte)i8);

			zero = false;
			negative = false;
			halfCarry = ((result) & 0x0f) < (cachedSP & 0x0f);
			carry = ((result) & 0xff) < (cachedSP & 0xff);

			HL = result;

			CycleTick();
		}

		private void AddSp(byte i8)
		{
			// Cache SP to avoid many calls to getter
			ushort cachedSP = SP;
			ushort result = (ushort)(cachedSP + (sbyte)i8);

			zero = false;
			negative = false;
			halfCarry = ((result) & 0x0f) < (cachedSP & 0x0f);
			carry = ((result) & 0xff) < (cachedSP & 0xff);

			SP = result;

			CycleTick();
			CycleTick();
		}

		private byte Cp(byte value)
		{
			byte result = (byte)(A - value);

			zero = result == 0;
			negative = true;
			halfCarry = (A & 0x0f) < (value & 0x0f);
			carry = result > A;

			return result;
		}

		private void Sub(byte value) => A = Cp(value);

		private void Sbc(byte value)
		{
			int cc = carry.ToInt();

			int result = (A - value - cc);

			zero = (byte)result == 0;
			negative = true;
			halfCarry = ((A & 0x0f) - cc) < (value & 0x0f);
			carry = result < 0;

			A = (byte)result;
		}

		private void And(byte value)
		{
			A &= value;

			zero = A == 0;
			negative = false;
			halfCarry = true;
			carry = false;
		}

		private void Or(byte value)
		{
			A |= value;

			zero = A == 0;
			negative = false;
			halfCarry = false;
			carry = false;
		}

		private void Xor(byte value)
		{
			A ^= value;

			zero = A == 0;
			negative = false;
			halfCarry = false;
			carry = false;
		}

		private void Bit(byte value, int bit)
		{			
			zero = (value & (1 << bit)) == 0;
			negative = false;
			halfCarry = true;
		}

		private byte Srl(byte value)
		{
			carry = (value & 0x01) != 0;
			value >>= 1;
			zero = value == 0;
			negative = false;
			halfCarry = false;

			return value;
		}

		private byte Rl(byte value)
		{
			int cc = carry.ToInt();

			carry = (value & 0x80) != 0;
			value = (byte)((value << 1) | cc);

			zero = value == 0;
			negative = false;
			halfCarry = false;

			return value;
		}

		private byte Rr(byte value)
		{
			int cc = carry.ToInt();

			carry = (value & 1) != 0;
			value = (byte)((value >> 1) | (cc << 7));

			zero = value == 0;
			negative = false;
			halfCarry = false;

			return value;
		}

		private byte Rlc(byte value)
		{
			carry = (value & 0x80) != 0;
			value = (byte)((value << 1) | (value >> 7));

			zero = value == 0;
			negative = false;
			halfCarry = false;

			return value;
		}

		private byte Rrc(byte value)
		{
			carry = (value & 0x01) != 0;
			value = (byte)((value >> 1) | (value << 7));

			zero = value == 0;
			negative = false;
			halfCarry = false;

			return value;
		}

		private byte Sla(byte value)
		{
			carry = (value & 0x80) != 0;
			value <<= 1;

			zero = value == 0;
			negative = false;
			halfCarry = false;

			return value;
		}

		private byte Sra(byte value)
		{
			carry = (value & 0x01) != 0;
			value = (byte)((value >> 1) | (value & 0x80));

			zero = value == 0;
			negative = false;
			halfCarry = false;

			return value;
		}

		private byte Swap(byte value)
		{
			value = (byte)((value >> 4) | (value << 4));

			zero = value == 0;
			negative = false;
			halfCarry = false;
			carry = false;

			return value;
		}

		private byte Res(byte value, int bit) => (byte)(value & (~(1 << bit)));

		private byte Set(byte value, int bit) => (byte)(value | (1 << bit));

		private void ProcessInterrupts()
		{
			// Default vector if no matches is 0
			ushort IrqVector = 0;

			// Check if any interrupts are pending
			if ((Interrupts.IE & Interrupts.IF & 0x1f) != 0)
			{
				if (!isHalted)
				{
					CycleTick();
				}

				// Exit HALT regardless of current IME
				isHalted = false;

				// Only service if interrupts are actually enabled
				if (ime)
				{
					// 6M (24T) cycles in total (inc. fetch) to service interrupt
					CycleTick();
					ime = false;

					// Write MSB of PC to (SP); this can effect decisions below if written to IE
					SP--;
					WriteByte(SP, (byte)(PC >> 8));
					CycleTick();

					// Check which interrupt to service, in priority order
					if (Interrupts.VBlankIrqReq && Interrupts.IE.IsBitSet(Interrupts.VBlankIeBit))
					{
						Interrupts.VBlankIrqReq = false;
						IrqVector = Interrupts.VBlankIrqVector;
					}
					else if (Interrupts.StatIrqReq && Interrupts.IE.IsBitSet(Interrupts.StatIeBit))
					{
						Interrupts.StatIrqReq = false;
						IrqVector = Interrupts.StatIrqVector;
					}
					else if (Interrupts.TimerIrqReq && Interrupts.IE.IsBitSet(Interrupts.TimerIeBit))
					{
						Interrupts.TimerIrqReq = false;
						IrqVector = Interrupts.TimerIrqVector;
					}
					else if (Interrupts.SerialIrqReq && Interrupts.IE.IsBitSet(Interrupts.SerialIeBit))
					{
						Interrupts.SerialIrqReq = false;
						IrqVector = Interrupts.SerialIrqVector;
					}
					else if (Interrupts.JoypadIrqReq && Interrupts.IE.IsBitSet(Interrupts.JoypadIeBit))
					{
						Interrupts.JoypadIrqReq = false;
						IrqVector = Interrupts.JoypadIrqVector;
					}

					// Write LSB of PC to (SP)
					SP--;
					WriteByte(SP, (byte)PC);

					// Jump to IRQ vector
					PC = IrqVector;
					CycleTick();
				}
			}
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct Reg16
		{
			[FieldOffset(0)] public ushort R16;
			[FieldOffset(0)] public byte R8Low;
			[FieldOffset(1)] public byte R8High;
		}
	}
}
