using System.Diagnostics;
using System.Threading;
using static SDL2.SDL;

namespace SpecBoy;

class Gameboy
{
	private const double FrameInterval = 1000.0 / 59.7;
	private const int Scale = 6;

	private bool fullspeed = false;

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

		window = SDL_CreateWindow("SpecBoy", SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED, 160 * Scale, 144 * Scale, SDL_WindowFlags.SDL_WINDOW_SHOWN);
		renderer = SDL_CreateRenderer(window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

		timers = new Timers();
		joypad = new Joypad();
		ppu = new Ppu(renderer);
		cartridge = new Cartridge(romName);
		mem = new Memory(timers, ppu, joypad, cartridge);
		cpu = new Cpu(mem, ppu, timers);
	}

	public void Run()
	{
		Stopwatch stopwatch = Stopwatch.StartNew();

		// Timer starts 4t before CPU
		timers.Tick();

		// First CPU instruction lags by 4t so tick other components to compensate
		timers.Tick();
		ppu.Tick();

		long prevCycles = 0;
		bool quit = false;

		while (!quit)
		{
			var frameStart = stopwatch.Elapsed.TotalMilliseconds;

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
                        else if (e.key.keysym.sym == SDL_Keycode.SDLK_SPACE)
                        {
                            fullspeed = !fullspeed;
                        }
                        break;
				}
			}

			long cyclesThisFrame = 0;
			while (cyclesThisFrame < 70224 && !ppu.HitVSync)
			{
				long currentCycles = cpu.Execute();
				cyclesThisFrame += currentCycles - prevCycles;
				prevCycles = currentCycles;
			}

			var elapsedTime = stopwatch.Elapsed.TotalMilliseconds - frameStart;
			
			if (!fullspeed && elapsedTime < FrameInterval)
			{
				var remainingTime = FrameInterval - elapsedTime;
				Thread.Sleep((int)remainingTime);
			}

			ppu.HitVSync = false;
			prevCycles = cpu.Cycles;
		}

		SDL_DestroyRenderer(renderer);
		SDL_DestroyWindow(window);
		SDL_Quit();
	}
}
