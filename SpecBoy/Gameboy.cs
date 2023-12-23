using System;

using static SDL2.SDL;
using static SDL2.SDL.SDL_RendererFlags;

namespace SpecBoy;

class Gameboy
{
	private const int Scale = 4;

	private readonly Cpu cpu;
	private readonly Memory mem;
	private readonly Timers timers;
	private readonly Ppu ppu;
	private readonly Joypad joypad;
	private readonly Cartridge cartridge;

	// SFML
	private readonly nint window;
	private readonly nint renderer;

	public Gameboy(string romName)
	{
		SDL_Init(SDL_INIT_VIDEO);

		window = SDL_CreateWindow("SpecBoy", SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, 160 * Scale, 144 * Scale, SDL_WindowFlags.SDL_WINDOW_SHOWN);
		renderer = SDL_CreateRenderer(window, -1, SDL_RENDERER_ACCELERATED);

		timers = new Timers();
		joypad = new Joypad();
		ppu = new Ppu(renderer);
		cartridge = new Cartridge(romName);
		mem = new Memory(timers, ppu, joypad, cartridge);
		cpu = new Cpu(mem, ppu, timers);
	}

	public void Run()
	{
		long prevCycles = 0;
		bool logging = false;

		// Timer starts 4t before CPU
		timers.Tick();

		// First CPU instruction lags by 4t so tick other components to compensate
		timers.Tick();
		ppu.Tick();

		bool quit = false;

		while (!quit)
		{
			long cyclesThisFrame = 0;

			while (SDL_PollEvent(out SDL_Event e) != 0)
			{
				switch (e.type)
				{
					case SDL_EventType.SDL_QUIT:
						quit = true;
						break;

					case SDL_EventType.SDL_KEYDOWN:
						if (e.key.keysym.sym == SDL_Keycode.SDLK_ESCAPE)
						{
							quit = true;
						}
						break;
				}
			}

			joypad.GetInput();

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

				long currentCycles = cpu.Execute();
				cyclesThisFrame += currentCycles - prevCycles;
				prevCycles = currentCycles;
			}

			ppu.HitVSync = false;
			prevCycles = cpu.Cycles;
		}

		SDL_DestroyRenderer(renderer);
		SDL_DestroyWindow(window);
		SDL_Quit();
	}
}
