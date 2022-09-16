namespace SpecBoy;

class Timers
{
	private readonly int[] triggerBits = new int[4] { 9, 3, 5, 7 };

	private int divBit;
	private ushort divCounter;
	private bool lastResult;
	private bool reloadTima;
	private bool reloadingTima;

	// Registers
	private byte tima;
	private byte tma;
	private byte tac;

	public byte Div
	{
		get => (byte)(divCounter >> 8);

		set
		{
			// Writing to Div always sets it to 0
			divCounter = 0;

			// Can't have falling edge if last result was 0
			if (!lastResult)
			{
				return;
			}

			DetectFallingEdge();
		}
	}

	public byte Tima
	{
		get => tima;
		set
		{
			// Block write if TIMA is being reloaded
			if (reloadingTima)
			{
				return;
			}
			
			// Writing to TIMA can cancel IRQ request, do that here
			tima = value;
			reloadTima = false;
		}
	}

	public byte Tma
	{
		get => tma;
		set
		{
			tma = value;

			// TIMA also updated if it's being reloaded when TMA written to
			if (reloadingTima)
			{
				tima = tma;
			}
		}
	}

	public byte Tac 
	{
		get => tac;
		set
		{
			tac = value;

			divBit = triggerBits[tac & 3];

			// Can't have falling edge if last result was 0
			if (!lastResult)
			{
				return;
			}

			DetectFallingEdge();
		}
	}

	public void Tick()
	{
		reloadingTima = false;

		if (reloadTima)
		{
			reloadTima = false;
			reloadingTima = true;
			tima = tma;

			Interrupts.TimerIrqReq = true;
		}

		divCounter += 4;
		DetectFallingEdge();
	}

	private void DetectFallingEdge()
	{
		bool result = tac.IsBitSet(2) && divCounter.IsBitSet(divBit);

		// Detect falling edge
		if (lastResult && !result)
		{
			tima++;

			if (tima == 0)
			{
				reloadTima = true;
			}
		}

		lastResult = result;
	}
}
