using System.Runtime.InteropServices;
using KeyValueDb.FileMemory.Paging;

namespace KeyValueDb.FileMemory;

[StructLayout(LayoutKind.Sequential, Pack = 2, Size = Size)]
public readonly struct FileMemoryAddress : IEquatable<FileMemoryAddress>
{
	public const int Size = 8;

	public bool IsInvalid => Value >= Invalid.Value;

	public static FileMemoryAddress Invalid { get; } = new(PageIndex.Invalid - 1, ushort.MaxValue);

	public FileMemoryAddress(ulong value)
	{
		Value = value;
	}

	public override string ToString() => IsInvalid ? "Invalid" : $"{PageIndex}:{BlockIndex}";

	public bool Equals(FileMemoryAddress other) => PageIndex == other.PageIndex && BlockIndex == other.BlockIndex;

	public override bool Equals(object? obj) => obj is FileMemoryAddress other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(PageIndex, BlockIndex);

	public static bool operator ==(FileMemoryAddress left, FileMemoryAddress right) => left.Equals(right);

	public static bool operator !=(FileMemoryAddress left, FileMemoryAddress right) => !left.Equals(right);

	internal ulong Value { get; }

	internal PageIndex PageIndex => Value >> 16;

	internal ushort BlockIndex => (ushort)(Value & 0x000000000000FFFF);

	internal FileMemoryAddress(PageIndex pageIndex, ushort blockIndex)
	{
		Value = (pageIndex.Value << 16) + blockIndex;
	}
}

public readonly struct FileMemoryAddress<T> : IEquatable<FileMemoryAddress<T>>
	where T : unmanaged
{
	public static FileMemoryAddress<T> Invalid { get; } = new(FileMemoryAddress.Invalid);

	public bool Equals(FileMemoryAddress<T> other) => InnerAddress.Equals(other.InnerAddress);

	public override bool Equals(object? obj) => obj is FileMemoryAddress<T> other && Equals(other);

	public override int GetHashCode() => InnerAddress.GetHashCode();

	public static bool operator ==(FileMemoryAddress<T> left, FileMemoryAddress<T> right) => left.Equals(right);

	public static bool operator !=(FileMemoryAddress<T> left, FileMemoryAddress<T> right) => !left.Equals(right);

	internal readonly FileMemoryAddress InnerAddress;

	internal FileMemoryAddress(FileMemoryAddress innerAddress)
	{
		InnerAddress = innerAddress;
	}
}