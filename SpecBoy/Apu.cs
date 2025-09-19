using static SDL2.SDL;

namespace SpecBoy;

sealed class Apu
{
	private const int SampleRate = 44100;
	private const int BufferSize = 1024;
	private const double ClockRate = 4194304.0; // Game Boy CPU clock rate
	private const double SampleInterval = ClockRate / SampleRate;

	// SDL Audio
	private uint audioDevice;
	private readonly object audioLock = new();
	private readonly Queue<short> audioBuffer = new();
	private double sampleClock;

	// APU Registers
	private readonly byte[] registers = new byte[0x30]; // NR10-NR52 + wave pattern

	// Channel state
	private readonly SquareChannel square1 = new(true);  // with sweep
	private readonly SquareChannel square2 = new(false); // no sweep
	private readonly WaveChannel wave = new();
	private readonly NoiseChannel noise = new();

	// Frame sequencer (512 Hz)
	private int frameSequencer;
	private int frameSequencerClock;

	public bool Enabled { get; private set; }

	public Apu()
	{
		InitializeAudio();
	}

	private void InitializeAudio()
	{
		SDL_AudioSpec desired = new()
		{
			freq = SampleRate,
			format = AUDIO_S16SYS,
			channels = 2, // Stereo
			samples = BufferSize,
			callback = AudioCallback
		};

		audioDevice = SDL_OpenAudioDevice(null, 0, ref desired, out _, 0);
		if (audioDevice == 0)
		{
			Console.WriteLine($"SDL_OpenAudioDevice failed: {SDL_GetError()}");
			return;
		}

		SDL_PauseAudioDevice(audioDevice, 0); // Start playback
	}

	private void AudioCallback(nint userdata, nint stream, int len)
	{
		int samples = len / sizeof(short);
		unsafe
		{
			short* buffer = (short*)stream;
			lock (audioLock)
			{
				int i = 0;
				while (i < samples && audioBuffer.Count > 0)
				{
					buffer[i++] = audioBuffer.Dequeue();
				}
				
				// Fill remaining with silence
				while (i < samples)
				{
					buffer[i++] = 0;
				}
			}
		}
	}

	public void Tick()
	{
		if (!Enabled) return;

		// Update frame sequencer (512 Hz)
		frameSequencerClock++;
		if (frameSequencerClock >= ClockRate / 512)
		{
			frameSequencerClock = 0;
			TickFrameSequencer();
		}

		// Update channels at 4 MHz
		square1.Tick();
		square2.Tick();
		wave.Tick();
		noise.Tick();

		// Generate audio sample
		sampleClock++;
		if (sampleClock >= SampleInterval)
		{
			sampleClock -= SampleInterval;
			GenerateSample();
		}
	}

	private void TickFrameSequencer()
	{
		frameSequencer = (frameSequencer + 1) % 8;

		switch (frameSequencer)
		{
			case 0:
			case 4:
				// Length counters (256 Hz)
				square1.TickLength();
				square2.TickLength();
				wave.TickLength();
				noise.TickLength();
				break;
			case 2:
			case 6:
				// Length counters and sweep (128 Hz)
				square1.TickLength();
				square2.TickLength();
				wave.TickLength();
				noise.TickLength();
				square1.TickSweep();
				break;
			case 7:
				// Volume envelopes (64 Hz)
				square1.TickEnvelope();
				square2.TickEnvelope();
				noise.TickEnvelope();
				break;
		}
	}

	private void GenerateSample()
	{
		if (!Enabled)
		{
			lock (audioLock)
			{
				audioBuffer.Enqueue(0);
				audioBuffer.Enqueue(0);
			}
			return;
		}

		// Get channel outputs
		float ch1 = square1.GetOutput();
		float ch2 = square2.GetOutput();
		float ch3 = wave.GetOutput();
		float ch4 = noise.GetOutput();

		// Apply channel enables
		byte nr52 = registers[0x26];
		if (!nr52.IsBitSet(0)) ch1 = 0;
		if (!nr52.IsBitSet(1)) ch2 = 0;
		if (!nr52.IsBitSet(2)) ch3 = 0;
		if (!nr52.IsBitSet(3)) ch4 = 0;

		// Mix channels with panning (NR51)
		byte nr51 = registers[0x25];
		byte nr50 = registers[0x24];

		float leftVol = ((nr50 >> 4) & 7) / 7.0f;
		float rightVol = (nr50 & 7) / 7.0f;

		float left = 0, right = 0;

		if (nr51.IsBitSet(4)) left += ch1;
		if (nr51.IsBitSet(0)) right += ch1;
		if (nr51.IsBitSet(5)) left += ch2;
		if (nr51.IsBitSet(1)) right += ch2;
		if (nr51.IsBitSet(6)) left += ch3;
		if (nr51.IsBitSet(2)) right += ch3;
		if (nr51.IsBitSet(7)) left += ch4;
		if (nr51.IsBitSet(3)) right += ch4;

		left *= leftVol;
		right *= rightVol;

		// Convert to 16-bit samples
		short leftSample = (short)(left * 0x1FFF);
		short rightSample = (short)(right * 0x1FFF);

		lock (audioLock)
		{
			// Keep buffer from growing too large
			while (audioBuffer.Count > BufferSize * 4)
			{
				audioBuffer.Dequeue();
			}

			audioBuffer.Enqueue(leftSample);
			audioBuffer.Enqueue(rightSample);
		}
	}

	public byte ReadRegister(int address)
	{
		int offset = address - 0xff10;
		if (offset < 0 || offset >= registers.Length)
			return 0xff;

		return address switch
		{
			0xff10 => (byte)(registers[0x00] | 0x80), // NR10
			0xff11 => 0xff, // NR11 - write only
			0xff12 => registers[0x02], // NR12
			0xff13 => 0xff, // NR13 - write only
			0xff14 => (byte)(registers[0x04] | 0xbf), // NR14

			0xff16 => 0xff, // NR21 - write only
			0xff17 => registers[0x07], // NR22
			0xff18 => 0xff, // NR23 - write only
			0xff19 => (byte)(registers[0x09] | 0xbf), // NR24

			0xff1a => (byte)(registers[0x0a] | 0x7f), // NR30
			0xff1b => 0xff, // NR31 - write only
			0xff1c => (byte)(registers[0x0c] | 0x9f), // NR32
			0xff1d => 0xff, // NR33 - write only
			0xff1e => (byte)(registers[0x0e] | 0xbf), // NR34

			0xff20 => (byte)(registers[0x10] | 0xc0), // NR41
			0xff21 => registers[0x11], // NR42
			0xff22 => registers[0x12], // NR43
			0xff23 => (byte)(registers[0x13] | 0x3f), // NR44

			0xff24 => registers[0x14], // NR50
			0xff25 => registers[0x15], // NR51
			0xff26 => (byte)(registers[0x16] | 0x70), // NR52

			>= 0xff30 and <= 0xff3f => registers[offset], // Wave pattern

			_ => 0xff
		};
	}

	public void WriteRegister(int address, byte value)
	{
		int offset = address - 0xff10;
		if (offset < 0 || offset >= registers.Length)
			return;

		// If APU is disabled, only NR52 can be written
		if (!Enabled && address != 0xff26)
			return;

		registers[offset] = value;

		switch (address)
		{
			// Square 1
			case 0xff10: // NR10 - Sweep
				square1.WriteSweep(value);
				break;
			case 0xff11: // NR11 - Length/duty
				square1.WriteLength(value);
				break;
			case 0xff12: // NR12 - Volume envelope
				square1.WriteEnvelope(value);
				break;
			case 0xff13: // NR13 - Frequency low
				square1.WriteFrequencyLow(value);
				break;
			case 0xff14: // NR14 - Frequency high/control
				square1.WriteFrequencyHigh(value);
				break;

			// Square 2
			case 0xff16: // NR21 - Length/duty
				square2.WriteLength(value);
				break;
			case 0xff17: // NR22 - Volume envelope
				square2.WriteEnvelope(value);
				break;
			case 0xff18: // NR23 - Frequency low
				square2.WriteFrequencyLow(value);
				break;
			case 0xff19: // NR24 - Frequency high/control
				square2.WriteFrequencyHigh(value);
				break;

			// Wave
			case 0xff1a: // NR30 - On/off
				wave.WriteEnable(value);
				break;
			case 0xff1b: // NR31 - Length
				wave.WriteLength(value);
				break;
			case 0xff1c: // NR32 - Volume
				wave.WriteVolume(value);
				break;
			case 0xff1d: // NR33 - Frequency low
				wave.WriteFrequencyLow(value);
				break;
			case 0xff1e: // NR34 - Frequency high/control
				wave.WriteFrequencyHigh(value);
				break;

			// Noise
			case 0xff20: // NR41 - Length
				noise.WriteLength(value);
				break;
			case 0xff21: // NR42 - Volume envelope
				noise.WriteEnvelope(value);
				break;
			case 0xff22: // NR43 - Frequency/randomness
				noise.WriteFrequency(value);
				break;
			case 0xff23: // NR44 - Control
				noise.WriteControl(value);
				break;

			// Master controls
			case 0xff24: // NR50 - Master volume
				break;
			case 0xff25: // NR51 - Sound panning
				break;
			case 0xff26: // NR52 - Sound on/off
				if (value.IsBitSet(7))
				{
					if (!Enabled)
					{
						Enabled = true;
						// Reset all channels when enabled
						square1.Reset();
						square2.Reset();
						wave.Reset();
						noise.Reset();
						frameSequencer = 0;
						frameSequencerClock = 0;
					}
				}
				else
				{
					Enabled = false;
					// Clear all registers except wave pattern
					for (int i = 0; i < 0x20; i++)
					{
						registers[i] = 0;
					}
				}
				break;

			// Wave pattern RAM
			case >= 0xff30 and <= 0xff3f:
				wave.WriteWaveData(address - 0xff30, value);
				break;
		}
	}

	public void Dispose()
	{
		if (audioDevice != 0)
		{
			SDL_CloseAudioDevice(audioDevice);
		}
	}
}