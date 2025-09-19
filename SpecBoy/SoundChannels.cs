namespace SpecBoy;

// Square wave channel (for channels 1 and 2)
internal sealed class SquareChannel
{
	private static readonly byte[][] DutyPatterns = new byte[][]
	{
		new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, // 12.5%
		new byte[] { 1, 0, 0, 0, 0, 0, 0, 1 }, // 25%
		new byte[] { 1, 0, 0, 0, 0, 1, 1, 1 }, // 50%
		new byte[] { 0, 1, 1, 1, 1, 1, 1, 0 }  // 75%
	};

	private readonly bool hasSweep;

	// Frequency
	private int frequency;
	private int timer;
	private int dutyPosition;

	// Length counter
	private int length;
	private bool lengthEnabled;

	// Volume envelope
	private int volume;
	private int envelopeVolume;
	private int envelopeTimer;
	private bool envelopeIncrease;

	// Duty cycle
	private int duty;

	// Sweep (channel 1 only)
	private int sweepTimer;
	private int sweepPeriod;
	private int sweepShift;
	private bool sweepIncrease;
	private bool sweepEnabled;

	// State
	private bool enabled;
	private bool dacEnabled;

	public SquareChannel(bool hasSweep)
	{
		this.hasSweep = hasSweep;
	}

	public void Reset()
	{
		frequency = 0;
		timer = 0;
		dutyPosition = 0;
		length = 0;
		lengthEnabled = false;
		volume = 0;
		envelopeVolume = 0;
		envelopeTimer = 0;
		envelopeIncrease = false;
		duty = 0;
		sweepTimer = 0;
		sweepPeriod = 0;
		sweepShift = 0;
		sweepIncrease = false;
		sweepEnabled = false;
		enabled = false;
		dacEnabled = false;
	}

	public void Tick()
	{
		if (timer > 0)
		{
			timer--;
		}
		else
		{
			timer = (2048 - frequency) * 4;
			dutyPosition = (dutyPosition + 1) % 8;
		}
	}

	public void TickLength()
	{
		if (lengthEnabled && length > 0)
		{
			length--;
			if (length == 0)
			{
				enabled = false;
			}
		}
	}

	public void TickEnvelope()
	{
		if (envelopeTimer > 0)
		{
			envelopeTimer--;
		}
		else if (volume > 0)
		{
			envelopeTimer = volume;
			if (envelopeIncrease && envelopeVolume < 15)
			{
				envelopeVolume++;
			}
			else if (!envelopeIncrease && envelopeVolume > 0)
			{
				envelopeVolume--;
			}
		}
	}

	public void TickSweep()
	{
		if (!hasSweep || !sweepEnabled) return;

		if (sweepTimer > 0)
		{
			sweepTimer--;
		}
		else if (sweepPeriod > 0)
		{
			sweepTimer = sweepPeriod;
			
			int newFreq = CalculateSweepFrequency();
			
			if (newFreq <= 2047 && sweepShift > 0)
			{
				frequency = newFreq;
			}
			
			// Check overflow again
			if (CalculateSweepFrequency() > 2047)
			{
				enabled = false;
			}
		}
	}

	private int CalculateSweepFrequency()
	{
		int delta = frequency >> sweepShift;
		return sweepIncrease ? frequency + delta : frequency - delta;
	}

	public float GetOutput()
	{
		if (!enabled || !dacEnabled)
			return 0;

		byte sample = DutyPatterns[duty][dutyPosition];
		return sample * envelopeVolume / 15.0f;
	}

	public void WriteSweep(byte value)
	{
		if (!hasSweep) return;

		sweepPeriod = (value >> 4) & 7;
		sweepIncrease = !value.IsBitSet(3);
		sweepShift = value & 7;

		if (!sweepIncrease && sweepShift > 0)
		{
			// Negate sweep can disable channel
			if (CalculateSweepFrequency() > 2047)
			{
				enabled = false;
			}
		}
	}

	public void WriteLength(byte value)
	{
		duty = (value >> 6) & 3;
		length = 64 - (value & 0x3f);
	}

	public void WriteEnvelope(byte value)
	{
		volume = (value >> 4) & 0xf;
		envelopeIncrease = value.IsBitSet(3);
		envelopeVolume = volume;

		// DAC is disabled when volume and direction are both 0
		dacEnabled = (value & 0xf8) != 0;
		if (!dacEnabled)
		{
			enabled = false;
		}
	}

	public void WriteFrequencyLow(byte value)
	{
		frequency = (frequency & 0x700) | value;
	}

	public void WriteFrequencyHigh(byte value)
	{
		frequency = (frequency & 0xff) | ((value & 7) << 8);
		lengthEnabled = value.IsBitSet(6);

		if (value.IsBitSet(7)) // Trigger
		{
			enabled = dacEnabled;
			
			if (length == 0)
			{
				length = 64;
			}

			timer = (2048 - frequency) * 4;
			envelopeTimer = volume;
			envelopeVolume = volume;

			if (hasSweep)
			{
				sweepTimer = sweepPeriod > 0 ? sweepPeriod : 8;
				sweepEnabled = sweepPeriod > 0 || sweepShift > 0;
				
				if (sweepShift > 0 && CalculateSweepFrequency() > 2047)
				{
					enabled = false;
				}
			}
		}
	}
}

// Wave channel (channel 3)
internal sealed class WaveChannel
{
	private readonly byte[] waveData = new byte[16];
	
	private int frequency;
	private int timer;
	private int position;
	private int length;
	private bool lengthEnabled;
	private int volume;
	private bool enabled;
	private bool dacEnabled;

	public void Reset()
	{
		frequency = 0;
		timer = 0;
		position = 0;
		length = 0;
		lengthEnabled = false;
		volume = 0;
		enabled = false;
		dacEnabled = false;
		Array.Clear(waveData);
	}

	public void Tick()
	{
		if (timer > 0)
		{
			timer--;
		}
		else
		{
			timer = (2048 - frequency) * 2;
			position = (position + 1) % 32;
		}
	}

	public void TickLength()
	{
		if (lengthEnabled && length > 0)
		{
			length--;
			if (length == 0)
			{
				enabled = false;
			}
		}
	}

	public float GetOutput()
	{
		if (!enabled || !dacEnabled)
			return 0;

		byte sample = waveData[position / 2];
		if (position % 2 == 0)
		{
			sample = (byte)((sample >> 4) & 0xf);
		}
		else
		{
			sample = (byte)(sample & 0xf);
		}

		int shift = volume == 0 ? 4 : volume - 1;
		sample = (byte)(sample >> shift);

		return sample / 15.0f;
	}

	public void WriteEnable(byte value)
	{
		dacEnabled = value.IsBitSet(7);
		if (!dacEnabled)
		{
			enabled = false;
		}
	}

	public void WriteLength(byte value)
	{
		length = 256 - value;
	}

	public void WriteVolume(byte value)
	{
		volume = (value >> 5) & 3;
	}

	public void WriteFrequencyLow(byte value)
	{
		frequency = (frequency & 0x700) | value;
	}

	public void WriteFrequencyHigh(byte value)
	{
		frequency = (frequency & 0xff) | ((value & 7) << 8);
		lengthEnabled = value.IsBitSet(6);

		if (value.IsBitSet(7)) // Trigger
		{
			enabled = dacEnabled;
			
			if (length == 0)
			{
				length = 256;
			}

			timer = (2048 - frequency) * 2;
			position = 0;
		}
	}

	public void WriteWaveData(int offset, byte value)
	{
		waveData[offset] = value;
	}
}

// Noise channel (channel 4)
internal sealed class NoiseChannel
{
	private int timer;
	private int lfsr = 0x7fff;
	private int length;
	private bool lengthEnabled;
	private int volume;
	private int envelopeVolume;
	private int envelopeTimer;
	private bool envelopeIncrease;
	private bool enabled;
	private bool dacEnabled;
	private bool shortMode;
	private int clockShift;
	private int divisor;

	public void Reset()
	{
		timer = 0;
		lfsr = 0x7fff;
		length = 0;
		lengthEnabled = false;
		volume = 0;
		envelopeVolume = 0;
		envelopeTimer = 0;
		envelopeIncrease = false;
		enabled = false;
		dacEnabled = false;
		shortMode = false;
		clockShift = 0;
		divisor = 0;
	}

	public void Tick()
	{
		if (timer > 0)
		{
			timer--;
		}
		else
		{
			timer = GetTimerPeriod();
			
			int bit = (lfsr & 1) ^ ((lfsr >> 1) & 1);
			lfsr = (lfsr >> 1) | (bit << 14);
			
			if (shortMode)
			{
				lfsr = (lfsr & ~0x40) | (bit << 6);
			}
		}
	}

	private int GetTimerPeriod()
	{
		int[] divisors = { 8, 16, 32, 48, 64, 80, 96, 112 };
		return divisors[divisor] << clockShift;
	}

	public void TickLength()
	{
		if (lengthEnabled && length > 0)
		{
			length--;
			if (length == 0)
			{
				enabled = false;
			}
		}
	}

	public void TickEnvelope()
	{
		if (envelopeTimer > 0)
		{
			envelopeTimer--;
		}
		else if (volume > 0)
		{
			envelopeTimer = volume;
			if (envelopeIncrease && envelopeVolume < 15)
			{
				envelopeVolume++;
			}
			else if (!envelopeIncrease && envelopeVolume > 0)
			{
				envelopeVolume--;
			}
		}
	}

	public float GetOutput()
	{
		if (!enabled || !dacEnabled)
			return 0;

		int output = (~lfsr & 1) * envelopeVolume;
		return output / 15.0f;
	}

	public void WriteLength(byte value)
	{
		length = 64 - (value & 0x3f);
	}

	public void WriteEnvelope(byte value)
	{
		volume = (value >> 4) & 0xf;
		envelopeIncrease = value.IsBitSet(3);
		envelopeVolume = volume;

		// DAC is disabled when volume and direction are both 0
		dacEnabled = (value & 0xf8) != 0;
		if (!dacEnabled)
		{
			enabled = false;
		}
	}

	public void WriteFrequency(byte value)
	{
		clockShift = (value >> 4) & 0xf;
		shortMode = value.IsBitSet(3);
		divisor = value & 7;
	}

	public void WriteControl(byte value)
	{
		lengthEnabled = value.IsBitSet(6);

		if (value.IsBitSet(7)) // Trigger
		{
			enabled = dacEnabled;
			
			if (length == 0)
			{
				length = 64;
			}

			timer = GetTimerPeriod();
			envelopeTimer = volume;
			envelopeVolume = volume;
			lfsr = 0x7fff;
		}
	}
}