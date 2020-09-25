using System;
using System.IO;
using SFML.Graphics;
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
		public readonly Input joypad;

		private readonly string romName;

		// SFML
		private readonly RenderWindow window;

		public Gameboy(string rom)
		{
			window = new RenderWindow(new VideoMode(160 * scale, 144 * scale), "SpecBoy", Styles.Default);
			//window.SetFramerateLimit(60);
			//window.SetVerticalSyncEnabled(true);

			timers = new Timers();
			joypad = new Input(window);
			ppu = new Ppu(window, scale);
			mem = new Memory(timers, ppu, joypad);
			cpu = new Cpu(mem, ppu, timers);

			romName = rom;
		}

		public void Run()
		{
			bool logging = false;

			window.Closed += (s, e) => window.Close();

			using (var rom = File.Open(romName, FileMode.Open))
			{
				rom.Read(mem.Rom, 0, (int)rom.Length);
			}

			while (window.IsOpen)
			{
				window.DispatchEvents();

				// For debugging - stop log when certain PC reached
				//if (cpu.PC == 0x486e)
				//{
				//	logging = false;
				//}

				if (logging)
				{
					Console.WriteLine($"A: {cpu.A:X2} F: {cpu.F:X2}" +
						$" B: {cpu.B:X2} C: {cpu.C:X2} D: {cpu.D:X2} E: {cpu.E:X2} H: {cpu.H:X2} L: {cpu.L:X2}" +
						$" SP: {cpu.SP:X4} PC: 00:{cpu.PC:X4}" +
						$" ({mem.ReadByte(cpu.PC):X2} {mem.ReadByte(cpu.PC + 1):X2} {mem.ReadByte(cpu.PC + 2):X2} {mem.ReadByte(cpu.PC + 3):X2})");
				}


				cpu.Execute();
			}
		}
	}
}
