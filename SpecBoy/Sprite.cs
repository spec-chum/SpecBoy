namespace SpecBoy;

readonly struct Sprite
{
	public Sprite(in Mem32 value)
	{
		Y = (short)(value.Bytes.First - 16);
		X = (short)(value.Bytes.Second - 8);
		TileNum = value.Bytes.Third;

		byte attributes = value.Bytes.Forth;

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
