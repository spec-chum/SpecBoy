namespace SpecBoy
{
	class Sprite
	{
		public Sprite(byte y, byte x, byte tileNum, byte attributes)
		{
			Y = (short)(y - 16);
			X = (short)(x - 8);
			TileNum = tileNum;

			Priority = Utility.IsBitSet(attributes, 7);
			YFlip = Utility.IsBitSet(attributes, 6);
			XFlip = Utility.IsBitSet(attributes, 5);
			PalNum = Utility.IsBitSet(attributes, 4);
		}

		public short Y { get; set; }

		public short X { get; set; }

		public byte TileNum { get; set; }

		public bool Priority { get; set; }

		public bool XFlip { get; set; }

		public bool YFlip { get; set; }

		public bool PalNum { get; set; }
	}
}
