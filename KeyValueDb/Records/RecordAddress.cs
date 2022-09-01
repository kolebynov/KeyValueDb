using System.Runtime.InteropServices;
using KeyValueDb.Paging;

namespace KeyValueDb.Records;

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly struct RecordAddress : IEquatable<RecordAddress>
{
	public const ushort InvalidRecordIndex = ushort.MaxValue;

	public PageIndex PageIndex { get; }

	public ushort RecordIndex { get; }

	public static RecordAddress Invalid { get; } = new(PageIndex.Invalid, InvalidRecordIndex);

	public RecordAddress(PageIndex pageIndex, ushort recordIndex)
	{
		PageIndex = pageIndex;
		RecordIndex = recordIndex;
	}

	public override string ToString() => $"{PageIndex}:{RecordIndex}";

	public bool Equals(RecordAddress other) => PageIndex == other.PageIndex && RecordIndex == other.RecordIndex;

	public override bool Equals(object? obj) => obj is RecordAddress other && Equals(other);

	public override int GetHashCode() => HashCode.Combine(PageIndex, RecordIndex);

	public static bool operator ==(RecordAddress left, RecordAddress right) => left.Equals(right);

	public static bool operator !=(RecordAddress left, RecordAddress right) => !left.Equals(right);
}