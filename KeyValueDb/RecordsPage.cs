using System.Collections;
using System.Runtime.InteropServices;
using KeyValueDb.Common;
using KeyValueDb.Paging;

namespace KeyValueDb;

[StructLayout(LayoutKind.Sequential, Size = Constants.PageSize)]
public unsafe struct RecordsPage
{
	public const ushort InvalidRecordEndOffset = ushort.MaxValue;
	public const ushort InvalidOffsetIndex = ushort.MaxValue;

	private const int PagePayload = Constants.PageSize - RecordsPageHeader.Size;

	private RecordsPageHeader _header;
	private fixed byte _payload[PagePayload];

	public RecordIndicesEnumerator RecordIndices => new(ref this);

	public RecordsPage()
	{
		_header = new RecordsPageHeader();
	}

	public ReadOnlySpan<byte> GetRecord(ushort recordIndex)
	{
		CheckRecordIndex(recordIndex);

		var prevRecordEndOffset = GetPrevRecordEndOffset(recordIndex);

		return Payload[prevRecordEndOffset.._header.RecordEndOffsets[recordIndex]];
	}

	public ushort? AddRecord(Span<byte> record)
	{
		var recordEndOffsets = _header.RecordEndOffsets;

		var freeSpace = _header.LastFilledOffsetIndex == ushort.MaxValue
			? (ushort)PagePayload
			: (ushort)(PagePayload - recordEndOffsets[_header.LastFilledOffsetIndex]);

		if (_header.NextFreeOffsetIndex >= recordEndOffsets.Length || freeSpace < (ushort)record.Length)
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
			ShiftRecords((ushort)(freeOffsetIndex + 1), (short)record.Length);
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

		recordEndOffsets[freeOffsetIndex] = (ushort)(beginRecordOffset + record.Length);

		record.CopyTo(Payload[beginRecordOffset..]);

		return freeOffsetIndex;
	}

	public void RemoveRecord(ushort index)
	{
		CheckRecordIndex(index);

		var offsets = _header.RecordEndOffsets;
		var recordLength = (ushort)(offsets[index] - GetPrevRecordEndOffset(index));

		ShiftRecords((ushort)(index + 1), (short)-recordLength);

		offsets[index] = InvalidRecordEndOffset;
		_header.NextFreeOffsetIndex = index < _header.NextFreeOffsetIndex ? index : _header.NextFreeOffsetIndex;
	}

	private Span<byte> Payload => MemoryMarshal.CreateSpan(ref _payload[0], PagePayload);

	private void CheckRecordIndex(ushort recordIndex)
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

	private ushort GetPrevRecordEndOffset(ushort currentRecordIndex)
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

		return prevRecordIndex != InvalidOffsetIndex
			? recordEndOffsets[prevRecordIndex]
			: (ushort)0;
	}

	public struct RecordIndicesEnumerator : IEnumerator<ushort>
	{
		private readonly StructReference<RecordsPage> _recordsPageRef;
		private ushort _currentIndex;

		public ushort Current { get; private set; }

		object IEnumerator.Current => Current;

		public RecordIndicesEnumerator(ref RecordsPage recordsPage)
		{
			_recordsPageRef = new StructReference<RecordsPage>(ref recordsPage);
			Current = 0;
			_currentIndex = 0;
		}

		public bool MoveNext()
		{
			ref readonly var header = ref _recordsPageRef.Value._header;
			for (var i = _currentIndex; i <= header.LastFilledOffsetIndex; i++)
			{
				if (header.RecordEndOffsets[i] != InvalidRecordEndOffset)
				{
					Current = i;
					_currentIndex = (ushort)(i + 1);
					return true;
				}
			}

			return false;
		}

		public void Reset()
		{
			Current = 0;
		}

		public void Dispose()
		{
		}

		public RecordIndicesEnumerator GetEnumerator() => this;
	}
}