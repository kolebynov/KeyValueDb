using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using KeyValueDb.Common.Extensions;

namespace KeyValueDb.FileMemory;

[StructLayout(LayoutKind.Sequential, Size = Constants.PageSize)]
internal ref struct AllocatedMemoryList
{
	public const int InvalidBlockEndOffset = -1;
	public const ushort InvalidOffsetIndex = ushort.MaxValue;

	private readonly Span<byte> _data;
	private readonly int _payloadSize;
	private readonly bool _isReadOnlyMode;

	public readonly bool IsEmpty => HeaderRef.LastFilledOffsetIndex == InvalidOffsetIndex;

	public readonly int FreeSpace => HeaderRef.LastFilledOffsetIndex == InvalidOffsetIndex
		? _payloadSize
		: (_payloadSize - BlockEndOffsets[HeaderRef.LastFilledOffsetIndex]);

	public AllocatedMemoryList(Span<byte> data)
	{
		_data = data;
		_payloadSize = 0;
		_isReadOnlyMode = false;
		_payloadSize = HeaderRef.CalculatePayloadSize(data.Length);
	}

	public AllocatedMemoryList(ReadOnlySpan<byte> data)
	{
		_data = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(data), data.Length);
		_payloadSize = 0;
		_isReadOnlyMode = true;
		_payloadSize = HeaderRef.CalculatePayloadSize(data.Length);
	}

	public readonly ReadOnlySpan<byte> GetAllocatedBlock(ushort blockIndex)
	{
		CheckBlockIndex(blockIndex);

		var prevRecordEndOffset = GetPrevBlockEndOffset(blockIndex);

		return Payload[prevRecordEndOffset..BlockEndOffsets[blockIndex]];
	}

	public Span<byte> GetAllocatedBlockMutable(ushort blockIndex)
	{
		CheckBlockIndex(blockIndex);

		var prevRecordEndOffset = GetPrevBlockEndOffset(blockIndex);

		return PayloadMutable[prevRecordEndOffset..BlockEndOffsetsMutable[blockIndex]];
	}

	public ushort? AllocateBlock(int size, ReadOnlySpan<byte> initialValue = default)
	{
		var blockEndOffsets = BlockEndOffsetsMutable;
		ref var header = ref HeaderMutableRef;

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
			initialValue.CopyTo(PayloadMutable.Slice(beginRecordOffset, size));
		}

		return freeOffsetIndex;
	}

	public void RemoveBlock(ushort blockIndex)
	{
		CheckBlockIndex(blockIndex);

		ref var header = ref HeaderMutableRef;
		var offsets = BlockEndOffsetsMutable;
		var recordLength = offsets[blockIndex] - GetPrevBlockEndOffset(blockIndex);

		if (blockIndex != header.LastFilledOffsetIndex)
		{
			ShiftBlocks((ushort)(blockIndex + 1), -recordLength);
		}

		offsets[blockIndex] = InvalidBlockEndOffset;
		header.NextFreeOffsetIndex = blockIndex < header.NextFreeOffsetIndex ? blockIndex : header.NextFreeOffsetIndex;
		header.LastFilledOffsetIndex = blockIndex == header.LastFilledOffsetIndex ? GetPrevBlockEndOffsetIndex(blockIndex) : header.LastFilledOffsetIndex;
	}

	private readonly ReadOnlySpan<byte> Data => _data;

	private Span<byte> DataMutable =>
		!_isReadOnlyMode ? _data : throw new InvalidOperationException("Mutable operations are not allowed in the read-only mode");

	private ref AllocatedMemoryListHeader HeaderMutableRef => ref DataMutable[..AllocatedMemoryListHeader.Size].AsRef<AllocatedMemoryListHeader>();

	private readonly ref readonly AllocatedMemoryListHeader HeaderRef =>
		ref Data[..AllocatedMemoryListHeader.Size].AsRef<AllocatedMemoryListHeader>();

	private Span<int> BlockEndOffsetsMutable =>
		DataMutable.Slice(AllocatedMemoryListHeader.Size, HeaderMutableRef.MaxBlockCount * 4).Cast<byte, int>();

	private readonly ReadOnlySpan<int> BlockEndOffsets =>
		Data.Slice(AllocatedMemoryListHeader.Size, HeaderRef.MaxBlockCount * 4).Cast<byte, int>();

	private Span<byte> PayloadMutable => DataMutable[(AllocatedMemoryListHeader.Size + (HeaderMutableRef.MaxBlockCount * 4))..];

	private readonly ReadOnlySpan<byte> Payload => Data[(AllocatedMemoryListHeader.Size + (HeaderRef.MaxBlockCount * 4))..];

	private readonly void CheckBlockIndex(ushort blockIndex)
	{
		var blockEndOffsets = BlockEndOffsets;
		if (blockIndex >= blockEndOffsets.Length || blockEndOffsets[blockIndex] == InvalidBlockEndOffset)
		{
			throw new ArgumentException($"Invalid block index {blockIndex}", nameof(blockIndex));
		}
	}

	private void ShiftBlocks(ushort startBlock, int offset)
	{
		var recordEndOffsets = BlockEndOffsetsMutable;

		ref var header = ref HeaderMutableRef;
		var prevRecordEndOffset = GetPrevBlockEndOffset(startBlock);
		var recordsData = PayloadMutable[prevRecordEndOffset..recordEndOffsets[header.LastFilledOffsetIndex]];
		recordsData.CopyTo(PayloadMutable[(prevRecordEndOffset + offset)..]);

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
		var blockEndOffsets = BlockEndOffsets;
		var prevRecordIndex = GetPrevBlockEndOffsetIndex(currentRecordIndex);

		return prevRecordIndex != InvalidOffsetIndex
			? blockEndOffsets[prevRecordIndex]
			: 0;
	}

	private readonly ushort GetPrevBlockEndOffsetIndex(ushort currentRecordIndex)
	{
		var blockEndOffsets = BlockEndOffsets;
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