using System;
using System.Collections.Generic;
using System.Text;

namespace SpecBoy
{
	class Memory
	{
		public Memory()
		{
			Ram = new byte[0xffff];
		}

		public byte[] Ram { get; set; }
	}
}
