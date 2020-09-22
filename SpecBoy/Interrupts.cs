//using System;
//using System.Collections.Generic;
//using System.Text;

//namespace SpecBoy
//{
//	class Interrupts
//	{
//		private Cpu cpu;
//		private Memory mem;
//		private Timers timers;

//		public Interrupts()
//		{ }

//		public void SetComponents(Gameboy gameboy)
//		{
//			cpu = gameboy.cpu;
//			mem = gameboy.mem;
//			timers = gameboy.timers;
//		}

//		public void InterruptHandler()
//		{
//			ushort vector = 0;

//			// Check if any interrupts are pending
//			if ((mem.IE & mem.IF) != 0)
//			{
//				cpu.isHalted = false;

//				// Only fire if interrupts actually enabled
//				if (cpu.ime)
//				{
//					if (timers.TimaIRQReq && Utility.IsBitSet(mem.IE, 2))
//					{
//						timers.TimaIRQReq = false;
//						Utility.ClearBit(mem.IF, 2);
//						vector = Timers.IRQVector;
//					}

//					cpu.DispatchInterrupt(vector);
//				}
//			}
//		}
//	}
//}
