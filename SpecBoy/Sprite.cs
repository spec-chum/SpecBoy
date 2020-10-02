namespace SpecBoy
{
	readonly struct Sprite
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

		public short Y { get; }

		public short X { get; }

		public byte TileNum { get; }

		public bool Priority { get; }

		public bool XFlip { get; }

		public bool YFlip { get; }

		public bool PalNum { get; }
	}
}
