using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace SpecBoy
{
	class Gameboy
	{
		private readonly Cpu cpu;
		private readonly Memory mem;

		public Gameboy()
		{
			mem = new Memory();
			cpu = new Cpu(mem);
		}

		public void Run()
		{
			using (var rom = File.Open("11-op a,(hl).gb", FileMode.Open))
			{
				rom.Read(mem.Mem, 0, (int)rom.Length);
			}

			using (var log = new StreamWriter(@"log.txt"))
			{
				while (true)
				{
					//Console.SetCursorPosition(0, 0);
					cpu.Execute();
					log.WriteLine($"A: {cpu.A:X2} F: {cpu.F:X2}" +
						$" B: {cpu.B:X2} C: {cpu.C:X2} D: {cpu.D:X2} E: {cpu.E:X2} H: {cpu.H:X2} L: {cpu.L:X2}" +
						$" SP: {cpu.SP:X4} PC: 00:{cpu.PC:X4}" +
						$" ({mem.Mem[cpu.PC]:X2} {mem.Mem[cpu.PC + 1]:X2} {mem.Mem[cpu.PC + 2]:X2} {mem.Mem[cpu.PC + 3]:X2})");
				}
			}
		}
	}
}
