using System;
using System.Diagnostics;
using System.IO;

namespace SpecBoy
{
	class Gameboy
	{
		private readonly Cpu cpu;
		private readonly Memory mem;
		private readonly Timers timers;

		public Gameboy()
		{
			timers = new Timers();
			mem = new Memory(timers);
			cpu = new Cpu(mem, timers);
		}

		public void Run()
		{
			using (var rom = File.Open("instr_timing.gb", FileMode.Open))
			{
				rom.Read(mem.Mem, 0, (int)rom.Length);
			}

			//using var log = new StreamWriter(@"log.txt");

			while (true)
			{
				cpu.Execute();

				//log.WriteLine($"A: {cpu.A:X2} F: {cpu.F:X2}" +
				//	$" B: {cpu.B:X2} C: {cpu.C:X2} D: {cpu.D:X2} E: {cpu.E:X2} H: {cpu.H:X2} L: {cpu.L:X2}" +
				//	$" SP: {cpu.SP:X4} PC: 00:{cpu.PC:X4}" +
				//	$" ({mem.Mem[cpu.PC]:X2} {mem.Mem[cpu.PC + 1]:X2} {mem.Mem[cpu.PC + 2]:X2} {mem.Mem[cpu.PC + 3]:X2})");
			}
		}
	}
}
