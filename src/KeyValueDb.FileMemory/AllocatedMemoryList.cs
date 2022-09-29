using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;

namespace KeyValueDb.FileMemory;

[StructLayout(LayoutKind.Sequential, Size = Constants.PageSize)]
internal ref struct AllocatedMemoryList
{
	public const int InvalidBlockEndOffset = -1;
	public const ushort InvalidOffsetIndex = ushort.MaxValue;

	private readonly Span<byte> _data;
	private readonly int _payloadSize;

	public readonly int FreeSpace => HeaderReadOnlyRef.LastFilledOffsetIndex == InvalidOffsetIndex
		? _payloadSize
		: (_payloadSize - BlockEndOffsetsReadOnly[HeaderReadOnlyRef.LastFilledOffsetIndex]);

	public AllocatedMemoryList(Span<byte> data)
	{
		_data = data;
		_payloadSize = 0;
		_payloadSize = HeaderRef.CalculatePayloadSize(data.Length);
	}

	public readonly ReadOnlySpan<byte> GetAllocatedBlock(ushort blockIndex)
	{
		CheckBlockIndex(blockIndex);

		var prevRecordEndOffset = GetPrevBlockEndOffset(blockIndex);

		return ReadOnlyPayload[prevRecordEndOffset..BlockEndOffsetsReadOnly[blockIndex]];
	}

	public Span<byte> GetAllocatedBlockMutable(ushort blockIndex)
	{
		CheckBlockIndex(blockIndex);

		var prevRecordEndOffset = GetPrevBlockEndOffset(blockIndex);

		return Payload[prevRecordEndOffset..BlockEndOffsets[blockIndex]];
	}

	public ushort? AllocateBlock(int size, ReadOnlySpan<byte> initialValue = default)
	{
		var blockEndOffsets = BlockEndOffsets;
		ref var header = ref HeaderRef;

		if (header.NextFreeOffsetIndex >= blockEndOffsets.Length || FreeSpace < size)
		{
			return null;
		}

		var beginRecordOffset = header.NextFreeOffsetIndex == 0 ? 0 : blockEndOffsets[header.NextFreeOffsetIndex - 1];

		var freeOffsetIndex = header.NextFreeOffsetIndex;
		if (freeOffsetIndex > header.LastFilledOffsetIndex || header.LastFilledOffsetIndex == ushort.MaxValue)
		{
			header.LastFilledOffsetIndex = freeOffsetIndex;
			header.NextFreeOffsetIndex++;
		}
		else
		{
			ShiftBlocks((ushort)(freeOffsetIndex + 1), size);
			var nextFreeOffsetIndex = InvalidOffsetIndex;
			for (var i = (ushort)(freeOffsetIndex + 1); i < header.LastFilledOffsetIndex; i++)
			{
				if (blockEndOffsets[i] == InvalidBlockEndOffset)
				{
					nextFreeOffsetIndex = i;
					break;
				}
			}

			header.NextFreeOffsetIndex = nextFreeOffsetIndex != InvalidOffsetIndex
				? nextFreeOffsetIndex
				: (ushort)(header.LastFilledOffsetIndex + 1);
		}

		blockEndOffsets[freeOffsetIndex] = beginRecordOffset + size;

		if (!initialValue.IsEmpty)
		{
			initialValue.CopyTo(Payload.Slice(beginRecordOffset, size));
		}

		return freeOffsetIndex;
	}

	public void RemoveBlock(ushort blockIndex)
	{
		CheckBlockIndex(blockIndex);

		ref var header = ref HeaderRef;
		var offsets = BlockEndOffsets;
		var recordLength = offsets[blockIndex] - GetPrevBlockEndOffset(blockIndex);

		if (blockIndex != header.LastFilledOffsetIndex)
		{
			ShiftBlocks((ushort)(blockIndex + 1), -recordLength);
		}

		offsets[blockIndex] = InvalidBlockEndOffset;
		header.NextFreeOffsetIndex = blockIndex < header.NextFreeOffsetIndex ? blockIndex : header.NextFreeOffsetIndex;
		header.LastFilledOffsetIndex = blockIndex == header.LastFilledOffsetIndex ? GetPrevBlockEndOffsetIndex(blockIndex) : header.LastFilledOffsetIndex;
	}

	private ref AllocatedMemoryListHeader HeaderRef => ref _data[..AllocatedMemoryListHeader.Size].AsRef<AllocatedMemoryListHeader>();

	private readonly ref readonly AllocatedMemoryListHeader HeaderReadOnlyRef =>
		ref _data[..AllocatedMemoryListHeader.Size].AsRef<AllocatedMemoryListHeader>();

	private Span<int> BlockEndOffsets => _data.Slice(AllocatedMemoryListHeader.Size, HeaderRef.MaxBlockCount * 4).Cast<byte, int>();

	private readonly ReadOnlySpan<int> BlockEndOffsetsReadOnly =>
		_data.Slice(AllocatedMemoryListHeader.Size, HeaderReadOnlyRef.MaxBlockCount * 4).Cast<byte, int>();

	private Span<byte> Payload => _data[(AllocatedMemoryListHeader.Size + (HeaderRef.MaxBlockCount * 4))..];

	private readonly ReadOnlySpan<byte> ReadOnlyPayload => _data[(AllocatedMemoryListHeader.Size + (HeaderReadOnlyRef.MaxBlockCount * 4))..];

	private readonly void CheckBlockIndex(ushort blockIndex)
	{
		var blockEndOffsets = BlockEndOffsetsReadOnly;
		if (blockIndex >= blockEndOffsets.Length || blockEndOffsets[blockIndex] == InvalidBlockEndOffset)
		{
			throw new ArgumentException($"Invalid block index {blockIndex}", nameof(blockIndex));
		}
	}

	private void ShiftBlocks(ushort startBlock, int offset)
	{
		var recordEndOffsets = BlockEndOffsets;

		ref var header = ref HeaderRef;
		var prevRecordEndOffset = GetPrevBlockEndOffset(startBlock);
		var recordsData = Payload[prevRecordEndOffset..recordEndOffsets[header.LastFilledOffsetIndex]];
		recordsData.CopyTo(Payload[(prevRecordEndOffset + offset)..]);

		for (var i = startBlock; i <= header.LastFilledOffsetIndex; i++)
		{
			if (recordEndOffsets[i] != InvalidBlockEndOffset)
			{
				recordEndOffsets[i] += offset;
			}
		}
	}

	private readonly int GetPrevBlockEndOffset(ushort currentRecordIndex)
	{
		var blockEndOffsets = BlockEndOffsetsReadOnly;
		var prevRecordIndex = GetPrevBlockEndOffsetIndex(currentRecordIndex);

		return prevRecordIndex != InvalidOffsetIndex
			? blockEndOffsets[prevRecordIndex]
			: 0;
	}

	private readonly ushort GetPrevBlockEndOffsetIndex(ushort currentRecordIndex)
	{
		var blockEndOffsets = BlockEndOffsetsReadOnly;
		var prevBlockIndex = InvalidOffsetIndex;

		for (var i = currentRecordIndex - 1; i >= 0; i--)
		{
			if (blockEndOffsets[i] != InvalidBlockEndOffset)
			{
				prevBlockIndex = (ushort)i;
				break;
			}
		}

		return prevBlockIndex;
	}
}