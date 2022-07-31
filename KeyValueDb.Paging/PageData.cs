using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KeyValueDb.Paging;

[StructLayout(LayoutKind.Sequential, Size = Constants.PageSize)]
internal unsafe struct PageData
{
	private byte _firstFreeBlock;
	private fixed byte _blockStates[Constants.PageBlockCount];
	private fixed byte _blocks[Constants.PagePayloadSize];

	public bool HasFreeBlocks => FirstFreeBlock != Constants.InvalidBlockIndex;

	public byte FirstFreeBlock => _firstFreeBlock;

	public ReadOnlySpan<byte> GetBlockData(byte index, int offset, int length)
	{
		CheckBlockIndex(index);

		if (length + offset > Constants.BlockSize)
		{
			throw new ArgumentException(string.Empty, nameof(offset));
		}

		return new ReadOnlySpan<byte>(GetBlockPointer(index) + offset, length > 0 ? length : Constants.BlockSize - offset);
	}

	public bool SetBlockData(byte index, ReadOnlySpan<byte> data, int offset)
	{
		CheckBlockIndex(index);

		if (data.Length > Constants.BlockSize - offset)
		{
			throw new ArgumentException($"Data length can't be greater than block size {Constants.BlockSize - offset}", nameof(data));
		}

		var blockSpan = new Span<byte>(GetBlockPointer(index) + offset, data.Length);
		if (data.SequenceEqual(blockSpan))
		{
			return false;
		}

		data.CopyTo(blockSpan);

		_blockStates[index] = (byte)BlockState.Busy;

		if (index != FirstFreeBlock)
		{
			return true;
		}

		for (var i = FirstFreeBlock; i < Constants.PageBlockCount; i++)
		{
			if (_blockStates[i] == (byte)BlockState.Free)
			{
				_firstFreeBlock = i;
				return true;
			}
		}

		_firstFreeBlock = Constants.InvalidBlockIndex;
		return true;
	}

	public bool FreeBlock(byte index)
	{
		CheckBlockIndex(index);

		if (_blockStates[index] == (byte)BlockState.Free)
		{
			return false;
		}

		_blockStates[index] = (byte)BlockState.Free;
		if (index < FirstFreeBlock)
		{
			_firstFreeBlock = index;
		}

		return true;
	}

	public static int GetBlockOffset(byte index) => index * Constants.BlockSize;

	private byte* FirstBlockPointer => (byte*)Unsafe.AsPointer(ref _blocks[0]);

	private byte* GetBlockPointer(byte index) => FirstBlockPointer + GetBlockOffset(index);

	private static void CheckBlockIndex(byte index)
	{
		if (index >= Constants.PageBlockCount)
		{
			throw new ArgumentOutOfRangeException(nameof(index), $"Index must be [0; {Constants.PageBlockCount - 1}]");
		}
	}
}