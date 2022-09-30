using System.Diagnostics;
using System.Runtime.CompilerServices;
using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;
using KeyValueDb.FileMemory.Paging;

namespace KeyValueDb.FileMemory;

public sealed class FileMemoryAllocator
{
	public const int MaxAllocatedSizeForDefaultMemoryList = 3842;

	private const int MinPageFreeSpaceForRecord = 8;

	private static readonly AllocatedMemoryList DefaultMemoryList = new(Constants.PageSize, 61);

	private readonly PageManager _pageManager;
	private readonly FileMappedStructure<FileMemoryAllocatorHeader> _header;

	internal FileMemoryAllocator(FileStream fileStream, PageManager pageManager, long offset, bool forceInitialize)
	{
		_ = fileStream ?? throw new ArgumentNullException(nameof(fileStream));
		_pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));

		_header = new FileMappedStructure<FileMemoryAllocatorHeader>(fileStream, offset, FileMemoryAllocatorHeader.Initial, forceInitialize);
		if (_header.ReadOnlyRef.AllocatedBlocksFirstPage.IsInvalid)
		{
			using var header = _header.GetMutableRef();
			using var page = AllocateRecordsPage();
			header.Ref.AllocatedBlocksFirstPage = page.PageIndex;
		}

		Debug.Assert(
			DefaultMemoryList.PayloadSize == MaxAllocatedSizeForDefaultMemoryList,
			$"Invalid {nameof(MaxAllocatedSizeForDefaultMemoryList)}");
	}

	public AllocatedMemory<T> AllocateStruct<T>(T initial = default)
		where T : unmanaged
	{
		return new AllocatedMemory<T>(Allocate(Unsafe.SizeOf<T>(), initial.AsBytes()));
	}

	public AllocatedMemory Allocate(int size, ReadOnlySpan<byte> initialValue = default)
	{
		using var headerRef = _header.GetMutableRef();
		var pageBlock = _pageManager.GetAllocatedPageBlock(headerRef.Ref.AllocatedBlocksFirstPage);

		ushort? recordIndex;
		while (true)
		{
			ref var allocatedMemoryList = ref pageBlock.ReadMutable().AsRef<AllocatedMemoryList>();
			recordIndex = allocatedMemoryList.AllocateBlock(size, initialValue);
			if (recordIndex != null)
			{
				if (pageBlock.PageIndex == headerRef.Ref.AllocatedBlocksFirstPage && allocatedMemoryList.FreeSpace < MinPageFreeSpaceForRecord)
				{
					using var nextPageWithFreeSpace = GetNextPageWithFreeSpace(headerRef.Ref.AllocatedBlocksFirstPage);
					headerRef.Ref.AllocatedBlocksFirstPage = nextPageWithFreeSpace.PageIndex;
				}

				break;
			}

			pageBlock.AssignNewDisposableToVariable(GetNextPageWithFreeSpace(pageBlock.PageIndex));
		}

		return new AllocatedMemory(pageBlock, recordIndex.Value);
	}

	public AllocatedMemory Get(FileMemoryAddress fileMemoryAddress) =>
		new(_pageManager.GetAllocatedPageBlock(fileMemoryAddress.PageIndex), fileMemoryAddress.BlockIndex);

	public AllocatedMemory<T> Get<T>(FileMemoryAddress<T> fileMemoryAddress)
		where T : unmanaged
	{
		return new AllocatedMemory<T>(Get(fileMemoryAddress.InnerAddress));
	}

	public void Remove(FileMemoryAddress fileMemoryAddress)
	{
		using var page = _pageManager.GetAllocatedPageBlock(fileMemoryAddress.PageIndex);
		ref var allocatedMemoryList = ref page.ReadMutable().AsRef<AllocatedMemoryList>();
		allocatedMemoryList.RemoveBlock(fileMemoryAddress.BlockIndex);

		if (page.PageIndex < _header.ReadOnlyRef.AllocatedBlocksFirstPage)
		{
			using var headerRef = _header.GetMutableRef();
			headerRef.Ref.AllocatedBlocksFirstPage = page.PageIndex;
		}

		if (allocatedMemoryList.IsEmpty)
		{
			_pageManager.FreePageBlock(page.PageIndex);
		}
	}

	private PageBlockAccessor GetNextPageWithFreeSpace(PageIndex startPageIndex)
	{
		while (true)
		{
			if (!_pageManager.TryGetNextAllocatedPageBlock(startPageIndex, out var pageBlock))
			{
				pageBlock = AllocateRecordsPage();
			}

			if (pageBlock.Read().AsRef<AllocatedMemoryList>().FreeSpace >= MinPageFreeSpaceForRecord)
			{
				return pageBlock;
			}

			startPageIndex = pageBlock.PageIndex;
		}
	}

	private PageBlockAccessor AllocateRecordsPage()
	{
		var pageBlock = _pageManager.AllocatePageBlock(1);
		pageBlock.Write(SpanExtensions.AsReadOnlyBytes(in DefaultMemoryList));

		return pageBlock;
	}
}