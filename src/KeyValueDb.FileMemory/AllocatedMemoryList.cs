using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace KeyValueDb.FileMemory;

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal struct AllocatedMemoryList
{
	private const int InvalidBlockEndOffset = -1;
	private const ushort InvalidOffsetIndex = ushort.MaxValue;
	private const int FieldsSize = 10;

	private readonly int _size;
	private readonly ushort _maxBlockCount;
	private ushort _nextFreeOffsetIndex;
	private ushort _lastFilledOffsetIndex;

	public readonly bool IsEmpty => _lastFilledOffsetIndex == InvalidOffsetIndex;

	public readonly int PayloadSize => _size - PayloadOffset;

	public readonly int FreeSpace => _lastFilledOffsetIndex == InvalidOffsetIndex
		? PayloadSize
		: (PayloadSize - BlockEndOffsets[_lastFilledOffsetIndex]);

	public AllocatedMemoryList(int size, ushort maxBlockCount)
	{
		_size = size;
		_maxBlockCount = maxBlockCount;
		_nextFreeOffsetIndex = 0;
		_lastFilledOffsetIndex = InvalidOffsetIndex;
	}

	public readonly ReadOnlySpan<byte> GetAllocatedMemory(ushort blockIndex)
	{
		CheckBlockIndex(blockIndex);

		var prevRecordEndOffset = GetPrevBlockEndOffset(blockIndex);

		return Payload[prevRecordEndOffset..BlockEndOffsets[blockIndex]];
	}

	public Span<byte> GetAllocatedMemoryMutable(ushort blockIndex)
	{
		CheckBlockIndex(blockIndex);

		var prevRecordEndOffset = GetPrevBlockEndOffset(blockIndex);

		return PayloadMutable[prevRecordEndOffset..BlockEndOffsetsMutable[blockIndex]];
	}

	public ushort? AllocateBlock(int size, ReadOnlySpan<byte> initialValue = default)
	{
		var blockEndOffsets = BlockEndOffsetsMutable;

		if (_nextFreeOffsetIndex >= blockEndOffsets.Length || FreeSpace < size)
		{
			return null;
		}

		var beginRecordOffset = _nextFreeOffsetIndex == 0 ? 0 : blockEndOffsets[_nextFreeOffsetIndex - 1];

		var freeOffsetIndex = _nextFreeOffsetIndex;
		if (freeOffsetIndex > _lastFilledOffsetIndex || _lastFilledOffsetIndex == ushort.MaxValue)
		{
			_lastFilledOffsetIndex = freeOffsetIndex;
			_nextFreeOffsetIndex++;
		}
		else
		{
			ShiftBlocks((ushort)(freeOffsetIndex + 1), size);
			var nextFreeOffsetIndex = InvalidOffsetIndex;
			for (var i = (ushort)(freeOffsetIndex + 1); i < _lastFilledOffsetIndex; i++)
			{
				if (blockEndOffsets[i] == InvalidBlockEndOffset)
				{
					nextFreeOffsetIndex = i;
					break;
				}
			}

			_nextFreeOffsetIndex = nextFreeOffsetIndex != InvalidOffsetIndex
				? nextFreeOffsetIndex
				: (ushort)(_lastFilledOffsetIndex + 1);
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

		var offsets = BlockEndOffsetsMutable;
		var recordLength = offsets[blockIndex] - GetPrevBlockEndOffset(blockIndex);

		if (blockIndex != _lastFilledOffsetIndex)
		{
			ShiftBlocks((ushort)(blockIndex + 1), -recordLength);
		}

		offsets[blockIndex] = InvalidBlockEndOffset;
		_nextFreeOffsetIndex = blockIndex < _nextFreeOffsetIndex ? blockIndex : _nextFreeOffsetIndex;
		_lastFilledOffsetIndex = blockIndex == _lastFilledOffsetIndex ? GetPrevBlockEndOffsetIndex(blockIndex) : _lastFilledOffsetIndex;
	}

	private readonly int PayloadOffset => FieldsSize + BlockOffsetsSize;

	private readonly int BlockOffsetsSize => _maxBlockCount * 4;

	private readonly ReadOnlySpan<byte> ThisSpan =>
		MemoryMarshal.CreateSpan(ref Unsafe.As<AllocatedMemoryList, byte>(ref Unsafe.AsRef(in this)), _size);

	private Span<byte> ThisSpanMutable => MemoryMarshal.CreateSpan(ref Unsafe.As<AllocatedMemoryList, byte>(ref this), _size);

	private Span<int> BlockEndOffsetsMutable => ThisSpanMutable.Slice(FieldsSize, BlockOffsetsSize).Cast<byte, int>();

	private readonly ReadOnlySpan<int> BlockEndOffsets => ThisSpan.Slice(FieldsSize, BlockOffsetsSize).Cast<byte, int>();

	private Span<byte> PayloadMutable => ThisSpanMutable[PayloadOffset..];

	private readonly ReadOnlySpan<byte> Payload => ThisSpan[PayloadOffset..];

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

		var prevRecordEndOffset = GetPrevBlockEndOffset(startBlock);
		var recordsData = PayloadMutable[prevRecordEndOffset..recordEndOffsets[_lastFilledOffsetIndex]];
		recordsData.CopyTo(PayloadMutable[(prevRecordEndOffset + offset)..]);

		for (var i = startBlock; i <= _lastFilledOffsetIndex; i++)
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