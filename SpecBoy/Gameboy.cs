using System;
using System.IO;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace SpecBoy
{
	class Gameboy
	{
		private const int scale = 4;

		public readonly Cpu cpu;
		public readonly Memory mem;
		public readonly Timers timers;
		public readonly Ppu ppu;

		private RenderWindow window;

		public Gameboy()
		{
			window = new RenderWindow(new VideoMode(160 * scale, 144 * scale), "SpecBoy", Styles.Default);

			timers = new Timers();
			ppu = new Ppu(window);
			mem = new Memory(timers, ppu);
			cpu = new Cpu(mem, timers, ppu);
		}

		public void Run()
		{
			window.Closed += (s, e) => window.Close();

			using (var rom = File.Open("instr_timing.gb", FileMode.Open))
			{
				rom.Read(mem.Rom, 0, (int)rom.Length);
			}

			while (window.IsOpen)
			{
				window.DispatchEvents();

				cpu.Execute();

				//Console.WriteLine($"A: {cpu.A:X2} F: {cpu.F:X2}" +
				//	$" B: {cpu.B:X2} C: {cpu.C:X2} D: {cpu.D:X2} E: {cpu.E:X2} H: {cpu.H:X2} L: {cpu.L:X2}" +
				//	$" SP: {cpu.SP:X4} PC: 00:{cpu.PC:X4}") +
				//$" ({mem.Mem[cpu.PC]:X2} {mem.Mem[cpu.PC + 1]:X2} {mem.Mem[cpu.PC + 2]:X2} {mem.Mem[cpu.PC + 3]:X2})");
			}
		}
	}
}
