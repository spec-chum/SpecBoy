namespace SpecBoy;

readonly struct Sprite
{
	public Sprite((byte y, byte x, byte tileNum, byte attribs) sprite)
	{
		Y = (short)(sprite.y - 16);
		X = (short)(sprite.x - 8);
		TileNum = sprite.tileNum;

		byte attributes = sprite.attribs;

		Priority = attributes.IsBitSet(7);
		YFlip = attributes.IsBitSet(6);
		XFlip = attributes.IsBitSet(5);
		PalNum = attributes.IsBitSet(4);
	}

	public short Y { get; }

	public short X { get; }

	public byte TileNum { get; }

	public bool Priority { get; }

	public bool YFlip { get; }

	public bool XFlip { get; }

	public bool PalNum { get; }
}
