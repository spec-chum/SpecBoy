using System.Diagnostics;

using static SDL2.SDL;

namespace SpecBoy;

sealed class Gameboy
{
	private const int ScreenWidth = 160;
	private const int ScreenHeight = 144;
	private const int Scale = 6;
	private const int FrameTimeArraySize = 4;
	private const int CpuCyclesPerFrame = 70224;

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

		window = SDL_CreateWindow("SpecBoy", SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED, ScreenWidth * Scale, ScreenHeight * Scale, SDL_WindowFlags.SDL_WINDOW_SHOWN);
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
		Span<double> frameTimeArray = stackalloc double[FrameTimeArraySize];
		uint frameTimeIndex = 0;
		long prevCycles = 0;
		bool quit = false;

		// Timer starts 4t before CPU
		timers.Tick();

		// First CPU instruction lags by 4t so tick other components to compensate
		timers.Tick();
		ppu.Tick();

		while (!quit)
		{
			long frameStart = Stopwatch.GetTimestamp();

			ProcessEvents(ref quit);

			RunFrame(ref prevCycles);

			if (!fullspeed)
			{
				FrameDelay(frameStart);
			}

			long frameEnd = Stopwatch.GetTimestamp();
			frameTimeArray[(int)frameTimeIndex] = (double)((frameEnd - frameStart) / (double)Stopwatch.Frequency);
			frameTimeIndex = (frameTimeIndex + 1) % FrameTimeArraySize;

			double frameTime = CalculateFrameTimeAverage(frameTimeArray);

			SDL_SetWindowTitle(window, $"SpecBoy - FPS: {1.0 / frameTime:F2}");
		}

		SDL_DestroyRenderer(renderer);
		SDL_DestroyWindow(window);
		SDL_Quit();
	}

	private void ProcessEvents(ref bool quit)
	{
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
	}

	private static double CalculateFrameTimeAverage(ReadOnlySpan<double> frameTimeArray)
	{
		double frameTime = 0.0;
		for (int i = 0; i < FrameTimeArraySize; i++)
		{
			frameTime += frameTimeArray[i]; // should unroll with no bounds checks
		}
		frameTime *= 1.0 / FrameTimeArraySize;
		return frameTime;
	}

	private void RunFrame(ref long prevCycles)
	{
		long cyclesThisFrame = 0;
		while (cyclesThisFrame < CpuCyclesPerFrame && !ppu.HitVBlank)
		{
			long currentCycles = cpu.Execute();
			cyclesThisFrame += currentCycles - prevCycles;
			prevCycles = currentCycles;
		}

		ppu.HitVBlank = false;
	}

	private void FrameDelay(in long frameStart)
	{
		int sleepTime = (int)((1000 * (FrameInterval - (Stopwatch.GetTimestamp() - frameStart)) / (double)Stopwatch.Frequency) - 0.5);
		if (sleepTime > 2)
		{
			Thread.Sleep(sleepTime);
		}
		while (Stopwatch.GetTimestamp() - frameStart < FrameInterval) { }
	}
}
