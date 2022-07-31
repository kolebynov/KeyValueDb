using System.Runtime.InteropServices;

namespace KeyValueDb.Paging;

[StructLayout(LayoutKind.Sequential, Size = Size, Pack = 2)]
public readonly struct BlockAddress : IEquatable<BlockAddress>
{
	public const int Size = 6;

	public int PageIndex { get; }

	public byte BlockIndex { get; }

	public static BlockAddress Invalid => new(Constants.InvalidPageIndex, Constants.InvalidBlockIndex);

	public BlockAddress(int pageIndex, byte blockIndex)
	{
		PageIndex = pageIndex;
		BlockIndex = blockIndex;
	}

	public bool Equals(BlockAddress other) => PageIndex == other.PageIndex && BlockIndex == other.BlockIndex;

	public override bool Equals(object? obj) => obj is BlockAddress other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(PageIndex, BlockIndex);

	public override string ToString()
	{
		return this == Invalid ? "Invalid" : $"P: {PageIndex}, B: {BlockIndex}";
	}

	public static bool operator ==(BlockAddress left, BlockAddress right) => left.Equals(right);

	public static bool operator !=(BlockAddress left, BlockAddress right) => !left.Equals(right);
}