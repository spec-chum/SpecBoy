namespace SpecBoy
{
	readonly struct Sprite
	{
		public Sprite(byte y, byte x, byte tileNum, byte attributes)
		{
			Y = (short)(y - 16);
			X = (short)(x - 8);
			TileNum = tileNum;

			Priority = attributes.IsBitSet(7);
			YFlip = attributes.IsBitSet(6);
			XFlip = attributes.IsBitSet(5);
			PalNum = attributes.IsBitSet(4);
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
