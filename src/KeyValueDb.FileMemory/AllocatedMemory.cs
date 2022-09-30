using KeyValueDb.Common.Extensions;
using KeyValueDb.FileMemory.Paging;

namespace KeyValueDb.FileMemory;

public readonly struct AllocatedMemory : IDisposable
{
	private readonly PageBlockAccessor _pageBlockAccessor;
	private readonly ushort _blockIndex;

	public Span<byte> DataMutable => _pageBlockAccessor.ReadMutable().AsRef<AllocatedMemoryList>().GetAllocatedBlockMutable(_blockIndex);

	public ReadOnlySpan<byte> Data => _pageBlockAccessor.Read().AsRef<AllocatedMemoryList>().GetAllocatedBlock(_blockIndex);

	public FileMemoryAddress Address => new(_pageBlockAccessor.PageIndex, _blockIndex);

	public void Dispose()
	{
		_pageBlockAccessor.Dispose();
	}

	internal AllocatedMemory(PageBlockAccessor pageBlockAccessor, ushort blockIndex)
	{
		_pageBlockAccessor = pageBlockAccessor;
		_blockIndex = blockIndex;
	}
}

public readonly struct AllocatedMemory<T> : IDisposable
	where T : unmanaged
{
	private readonly AllocatedMemory _allocatedMemory;

	public ref T ValueRefMutable => ref _allocatedMemory.DataMutable.AsRef<T>();

	public ref readonly T ValueRef => ref _allocatedMemory.Data.AsRef<T>();

	public FileMemoryAddress<T> Address => new(_allocatedMemory.Address);

	public void Dispose()
	{
		_allocatedMemory.Dispose();
	}

	internal AllocatedMemory(AllocatedMemory allocatedMemory)
	{
		_allocatedMemory = allocatedMemory;
	}
}