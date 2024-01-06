﻿namespace SpecBoy;

sealed class Program
{
	static void Main(string[] args)
	{
		if (args.Length < 1)
		{
			Console.WriteLine("Usage: SpecBoy romname.gb");
			return;
		}

		Gameboy gb = new (args[0]);
		gb.Run();
	}
}
