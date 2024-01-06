using System.Diagnostics;

using static SDL2.SDL;

namespace SpecBoy;

sealed class Gameboy
{
	private const int Scale = 6;
	private const int FPSQueueDepth = 4;

	private readonly long FrameInterval = (long)(Stopwatch.Frequency / 59.7);
	private bool fullspeed;

	private readonly Cpu cpu;
	private readonly Memory mem;
	private readonly Timers timers;
	private readonly Ppu ppu;
	private readonly Joypad joypad;
	private readonly Cartridge cartridge;

	// SDL
	private readonly nint window;
	private readonly nint renderer;

	public Gameboy(string romName)
	{
		_ = SDL_Init(SDL_INIT_VIDEO);

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
		Queue<double> frameTimes = new (FPSQueueDepth);

		// Timer starts 4t before CPU
		timers.Tick();

		// First CPU instruction lags by 4t so tick other components to compensate
		timers.Tick();
		ppu.Tick();

		long prevCycles = 0;
		bool quit = false;

		while (!quit)
		{
			long frameStart = Stopwatch.GetTimestamp();

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

			ppu.HitVSync = false;
			prevCycles = cpu.Cycles;

			if (!fullspeed)
			{
				int sleepTime = (int)((1000 * (FrameInterval - (Stopwatch.GetTimestamp() - frameStart)) / (double)Stopwatch.Frequency) - 0.5);
				if (sleepTime > 2)
				{
					Thread.Sleep(sleepTime);
					while (Stopwatch.GetTimestamp() - frameStart < FrameInterval) { }
				}
			}

			frameTimes.Enqueue((double)((Stopwatch.GetTimestamp() - frameStart) / (double)Stopwatch.Frequency));
			if (frameTimes.Count > FPSQueueDepth)
			{
				frameTimes.Dequeue();
			}

			double averageFPS = 0.0;
			foreach (double frameTime in frameTimes)
			{
				averageFPS += frameTime;
			}
			averageFPS /= frameTimes.Count;

			SDL_SetWindowTitle(window, $"SpecBoy - FPS: {1.0 / averageFPS:F2}");
		}

		SDL_DestroyRenderer(renderer);
		SDL_DestroyWindow(window);
		SDL_Quit();
	}
}
