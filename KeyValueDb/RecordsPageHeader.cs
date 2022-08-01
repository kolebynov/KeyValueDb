using System.Runtime.InteropServices;
using KeyValueDb.Paging;

namespace KeyValueDb;

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public unsafe struct RecordsPageHeader
{
	private const int MaxRecordCount = 62;

	private static readonly int PagePayload = Constants.PageSize - Marshal.SizeOf<RecordsPageHeader>();

	private ushort _nextFreeOffsetIndex;
	private ushort _lastFilledOffsetIndex;
#pragma warning disable CS0649
	private fixed ushort _recordEndOffsets[MaxRecordCount];
#pragma warning restore CS0649

	public ushort FreeSpace =>
		_lastFilledOffsetIndex == ushort.MaxValue ? (ushort)PagePayload : (ushort)(PagePayload - RecordEndOffsets[_lastFilledOffsetIndex]);

	public RecordsPageHeader()
	{
		_nextFreeOffsetIndex = 0;
		_lastFilledOffsetIndex = ushort.MaxValue;
		RecordEndOffsets.Fill(RecordAddress.InvalidPageOffset);
	}

	public bool AddRecordOffset(ushort recordLength)
	{
		if (_nextFreeOffsetIndex >= RecordEndOffsets.Length || FreeSpace < recordLength)
		{
			return false;
		}

		var offset = _nextFreeOffsetIndex == 0 ? recordLength : (ushort)(RecordEndOffsets[_nextFreeOffsetIndex - 1] + recordLength);
		if (_nextFreeOffsetIndex > _lastFilledOffsetIndex || _lastFilledOffsetIndex == ushort.MaxValue)
		{
			_lastFilledOffsetIndex = _nextFreeOffsetIndex;
		}

		RecordEndOffsets[_nextFreeOffsetIndex++] = offset;

		return true;
	}

	public void RemoveRecordOffset(ushort index)
	{
		if (index >= _nextFreeOffsetIndex)
		{
			throw new ArgumentException($"Invalid record index {index}", nameof(index));
		}

		var offsets = RecordEndOffsets;
		var recordLength = index == 0 ? offsets[0] : (ushort)(offsets[index] - offsets[index - 1]);

		_nextFreeOffsetIndex--;

		for (var i = index; i < _nextFreeOffsetIndex; i++)
		{
			offsets[i] = (ushort)(offsets[i + 1] - recordLength);
		}
	}

	private Span<ushort> RecordEndOffsets => MemoryMarshal.CreateSpan(ref _recordEndOffsets[0], MaxRecordCount);
}