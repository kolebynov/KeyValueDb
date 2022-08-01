using System.Runtime.InteropServices;
using KeyValueDb.Paging;

namespace KeyValueDb;

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly struct RecordAddress : IEquatable<RecordAddress>
{
	public const ushort InvalidPageOffset = ushort.MaxValue;

	public uint PageIndex { get; }

	public ushort PageOffset { get; }

	public static RecordAddress Invalid => new(Constants.InvalidPageIndex, InvalidPageOffset);

	public RecordAddress(uint pageIndex, ushort pageOffset)
	{
		PageIndex = pageIndex;
		PageOffset = pageOffset;
	}

	public bool Equals(RecordAddress other) => PageIndex == other.PageIndex && PageOffset == other.PageOffset;

	public override bool Equals(object? obj) => obj is RecordAddress other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(PageIndex, PageOffset);

	public static bool operator ==(RecordAddress left, RecordAddress right) => left.Equals(right);

	public static bool operator !=(RecordAddress left, RecordAddress right) => !left.Equals(right);
}