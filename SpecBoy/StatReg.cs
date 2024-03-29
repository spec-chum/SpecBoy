﻿using static SpecBoy.Ppu;

namespace SpecBoy;

struct StatReg
{
	public bool LyCompareInt;
	public bool OamInt;
	public bool VBlankInt;
	public bool HBlankInt;
	public bool LyCompareFlag;

	public bool LcdEnabled;
	public Mode CurrentMode;
	public Mode PendingMode;
	public Mode PendingInterrupt;

	private bool intRequest;

	public byte Lyc { get; set; }

	public void Init()
	{
		CurrentMode = Mode.None;
		PendingMode = Mode.None;
		PendingInterrupt = Mode.None;
	}

	public void UpdatePending()
	{
		CurrentMode = PendingMode;

		if (PendingInterrupt != Mode.None)
		{
			RequestInterrupt(PendingInterrupt);
			PendingInterrupt = Mode.None;
		}
	}

	public byte GetByte()
	{
		RequestInterrupt(CurrentMode);

		return (byte)(0x80
			| LyCompareInt.ToBytePower(6)
			| OamInt.ToBytePower(5)
			| VBlankInt.ToBytePower(4)
			| HBlankInt.ToBytePower(3)
			| LyCompareFlag.ToBytePower(2)
			| (int)CurrentMode);
	}

	public void SetByte(byte value)
	{
		LyCompareInt = true;
		OamInt = true;
		VBlankInt = true;
		HBlankInt = true;

		RequestInterrupt(CurrentMode);

		LyCompareInt = value.IsBitSet(6);
		OamInt = value.IsBitSet(5);
		VBlankInt = value.IsBitSet(4);
		HBlankInt = value.IsBitSet(3);
	}

	public void CompareLyc(byte ly, bool reqInt = true)
	{
		if (!LcdEnabled)
		{
			return;
		}

		LyCompareFlag = ly == Lyc;

		if (reqInt)
		{
			RequestInterrupt(CurrentMode);
		}
	}

	public void RequestInterrupt(Mode mode)
	{
		if (!LcdEnabled)
		{
			return;
		}

		bool oldIntRequest = intRequest;

		if (mode != Mode.None)
		{
			intRequest = mode switch
			{
				Mode.HBlank => HBlankInt,
				Mode.VBlank => VBlankInt,
				Mode.OAM => OamInt,
				_ => false
			};
		}

		if (LyCompareInt && LyCompareFlag)
		{
			intRequest = true;
		}

		// Only fire on rising edge (STAT IRQ blocking)
		if (!oldIntRequest && intRequest)
		{
			Interrupts.IF.SetBit(Interrupts.StatBit);
		}
	}
}
