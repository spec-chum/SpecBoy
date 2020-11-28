using System;
using SFML.Graphics;
using SFML.Window;

namespace SpecBoy
{
	class Gameboy
	{
		private const int scale = 4;

		private readonly Cpu cpu;
		private readonly Memory mem;
		private readonly Timers timers;
		private readonly Ppu ppu;
		private readonly Joypad joypad;
		private readonly Cartridge cartridge;

		// SFML
		private readonly RenderWindow window;

		public Gameboy(string romName)
		{
			window = new RenderWindow(new VideoMode(160 * scale, 144 * scale), "SpecBoy", Styles.Default);
			window.SetFramerateLimit(0);
			//window.SetVerticalSyncEnabled(false);

			timers = new Timers();
			joypad = new Joypad(window);
			ppu = new Ppu(window, scale);
			cartridge = new Cartridge(romName);
			mem = new Memory(timers, ppu, joypad, cartridge);
			cpu = new Cpu(mem, ppu, timers);
		}

		public void Run()
		{
			long prevCycles = 0;
			bool logging = false;

			window.Closed += (s, e) => window.Close();

			// Timer starts 4t before CPU
			timers.Tick();

			// First CPU instruction lags by 4t so tick other components to compensate
			timers.Tick();
			ppu.Tick();

			while (window.IsOpen)
			{
				long cyclesThisFrame = 0;
				long currentCycles = 0;

				window.DispatchEvents();

				long prevPC = 0;

				while (cyclesThisFrame < 70224 && !ppu.HitVSync)
				{
					if (logging)
					{
						if (prevPC != cpu.PC)
						{
							Console.WriteLine($"A: {cpu.A:X2} F: {cpu.F:X2}" +
								$" B: {cpu.B:X2} C: {cpu.C:X2} D: {cpu.D:X2} E: {cpu.E:X2} H: {cpu.H:X2} L: {cpu.L:X2}" +
								$" SP: {cpu.SP:X4} PC: 00:{cpu.PC:X4}" +
								$" ({mem.ReadByte(cpu.PC, true):X2} {mem.ReadByte(cpu.PC + 1, true):X2}" +
								$" {mem.ReadByte(cpu.PC + 2, true):X2} {mem.ReadByte(cpu.PC + 3, true):X2})");
						}

						prevPC = cpu.PC;
					}

					currentCycles = cpu.Execute();
					cyclesThisFrame += currentCycles - prevCycles;
					prevCycles = currentCycles;
				}

				ppu.HitVSync = false;
				prevCycles = cpu.Cycles;
			}
		}
	}
}
