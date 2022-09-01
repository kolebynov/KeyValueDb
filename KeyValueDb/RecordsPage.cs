using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Paging;

namespace KeyValueDb;

[StructLayout(LayoutKind.Sequential, Size = Constants.PageSize)]
public unsafe struct RecordsPage
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

	public readonly RecordData GetRecord(ushort recordIndex)
	{
		CheckRecordIndex(recordIndex);

		var prevRecordEndOffset = GetPrevRecordEndOffset(recordIndex);
		var spanReader = new SpanReader<byte>(ReadOnlyPayload[prevRecordEndOffset..]);
		ref readonly var header = ref spanReader.Read(RecordHeader.Size).AsRef<RecordHeader>();
		var key = spanReader.Read(header.KeySize);
		var data = spanReader.Read(header.DataSize);

		return new RecordData(in header, key, data);
	}

	public ushort? AddRecord(RecordData record)
	{
		if (record.Header.KeySize != record.Key.Length || record.Header.DataSize != record.Data.Length)
		{
			throw new ArgumentException(
				"Different key/data size in the header and the real key/data size",
				nameof(record));
		}

		var recordEndOffsets = _header.RecordEndOffsets;
		var recordSize = record.Size;

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

		var spanWriter = new SpanWriter<byte>(Payload[beginRecordOffset..]);
		var recordHeader = record.Header;
		spanWriter.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref recordHeader, 1)));
		spanWriter.Write(record.Key);
		spanWriter.Write(record.Data);

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

	public void UpdateNextRecordAddress(ushort recordIndex, RecordAddress nextRecordAddress)
	{
		var record = GetRecord(recordIndex);
		var newHeader = new RecordHeader(nextRecordAddress, record.Header.KeySize, record.Header.DataSize);

		newHeader.AsBytes().CopyTo(Payload.Slice(_header.RecordEndOffsets[recordIndex] - record.Size, RecordHeader.Size));
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

	public readonly ref struct RecordData
	{
		public readonly RecordHeader Header;

		public readonly ReadOnlySpan<byte> Key;

		public readonly ReadOnlySpan<byte> Data;

		public ushort Size => (ushort)(RecordHeader.Size + Key.Length + Data.Length);

		public RecordData(in RecordHeader header, ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
		{
			Header = header;
			Key = key;
			Data = data;
		}
	}
}