using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KeyValueDb.FileMemory;

[StructLayout(LayoutKind.Sequential, Size = Constants.PageSize)]
internal unsafe struct RecordsPage
{
	public const ushort InvalidRecordEndOffset = ushort.MaxValue;
	public const ushort InvalidOffsetIndex = ushort.MaxValue;
	public const int PagePayload = Constants.PageSize - RecordsPageHeader.Size;

	private RecordsPageHeader _header;
	private fixed byte _payload[PagePayload];

	public readonly ushort FreeSpace => _header.LastFilledOffsetIndex == ushort.MaxValue
		? (ushort)PagePayload
		: (ushort)(PagePayload - _header.RecordEndOffsets[_header.LastFilledOffsetIndex]);

	public static RecordsPage Initial { get; } = new() { _header = RecordsPageHeader.Initial };

	public readonly ReadOnlySpan<byte> GetRecord(ushort recordIndex)
	{
		CheckRecordIndex(recordIndex);

		var prevRecordEndOffset = GetPrevRecordEndOffset(recordIndex);

		return ReadOnlyPayload[prevRecordEndOffset.._header.RecordEndOffsets[recordIndex]];
	}

	public Span<byte> GetRecordMutable(ushort recordIndex)
	{
		CheckRecordIndex(recordIndex);

		var prevRecordEndOffset = GetPrevRecordEndOffset(recordIndex);

		return Payload[prevRecordEndOffset.._header.RecordEndOffsets[recordIndex]];
	}

	public ushort? CreateRecord(int recordSize)
	{
		var recordEndOffsets = _header.RecordEndOffsets;

		if (_header.NextFreeOffsetIndex >= recordEndOffsets.Length || FreeSpace < recordSize)
		{
			return null;
		}

		var beginRecordOffset = _header.NextFreeOffsetIndex == 0 ? (ushort)0 : recordEndOffsets[_header.NextFreeOffsetIndex - 1];

		var freeOffsetIndex = _header.NextFreeOffsetIndex;
		if (freeOffsetIndex > _header.LastFilledOffsetIndex || _header.LastFilledOffsetIndex == ushort.MaxValue)
		{
			_header.LastFilledOffsetIndex = freeOffsetIndex;
			_header.NextFreeOffsetIndex++;
		}
		else
		{
			ShiftRecords((ushort)(freeOffsetIndex + 1), (short)recordSize);
			var nextFreeOffsetIndex = InvalidOffsetIndex;
			for (var i = (ushort)(freeOffsetIndex + 1); i < _header.LastFilledOffsetIndex; i++)
			{
				if (recordEndOffsets[i] == InvalidRecordEndOffset)
				{
					nextFreeOffsetIndex = i;
					break;
				}
			}

			_header.NextFreeOffsetIndex = nextFreeOffsetIndex != InvalidOffsetIndex
				? nextFreeOffsetIndex
				: (ushort)(_header.LastFilledOffsetIndex + 1);
		}

		recordEndOffsets[freeOffsetIndex] = (ushort)(beginRecordOffset + recordSize);

		return freeOffsetIndex;
	}

	public void RemoveRecord(ushort index)
	{
		CheckRecordIndex(index);

		var offsets = _header.RecordEndOffsets;
		var recordLength = (ushort)(offsets[index] - GetPrevRecordEndOffset(index));

		if (index != _header.LastFilledOffsetIndex)
		{
			ShiftRecords((ushort)(index + 1), (short)-recordLength);
		}

		offsets[index] = InvalidRecordEndOffset;
		_header.NextFreeOffsetIndex = index < _header.NextFreeOffsetIndex ? index : _header.NextFreeOffsetIndex;
		_header.LastFilledOffsetIndex = index == _header.LastFilledOffsetIndex ? GetPrevRecordEndOffsetIndex(index) : _header.LastFilledOffsetIndex;
	}

	private Span<byte> Payload => MemoryMarshal.CreateSpan(ref _payload[0], PagePayload);

	private readonly ReadOnlySpan<byte> ReadOnlyPayload =>
		MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _payload[0]), PagePayload);

	private readonly void CheckRecordIndex(ushort recordIndex)
	{
		if (recordIndex >= _header.RecordEndOffsets.Length || _header.RecordEndOffsets[recordIndex] == InvalidRecordEndOffset)
		{
			throw new ArgumentException($"Invalid record index {recordIndex}", nameof(recordIndex));
		}
	}

	private void ShiftRecords(ushort startIndex, short offset)
	{
		var recordEndOffsets = _header.RecordEndOffsets;

		var prevRecordEndOffset = GetPrevRecordEndOffset(startIndex);
		var recordsData = Payload[prevRecordEndOffset..recordEndOffsets[_header.LastFilledOffsetIndex]];
		recordsData.CopyTo(Payload[(prevRecordEndOffset + offset)..]);

		for (var i = startIndex; i <= _header.LastFilledOffsetIndex; i++)
		{
			if (recordEndOffsets[i] != InvalidRecordEndOffset)
			{
				recordEndOffsets[i] = (ushort)(recordEndOffsets[i] + offset);
			}
		}
	}

	private readonly ushort GetPrevRecordEndOffset(ushort currentRecordIndex)
	{
		var recordEndOffsets = _header.RecordEndOffsets;
		var prevRecordIndex = GetPrevRecordEndOffsetIndex(currentRecordIndex);

		return prevRecordIndex != InvalidOffsetIndex
			? recordEndOffsets[prevRecordIndex]
			: (ushort)0;
	}

	private readonly ushort GetPrevRecordEndOffsetIndex(ushort currentRecordIndex)
	{
		var recordEndOffsets = _header.RecordEndOffsets;
		var prevRecordIndex = InvalidOffsetIndex;

		for (var i = currentRecordIndex - 1; i >= 0; i--)
		{
			if (recordEndOffsets[i] != InvalidRecordEndOffset)
			{
				prevRecordIndex = (ushort)i;
				break;
			}
		}

		return prevRecordIndex;
	}
}