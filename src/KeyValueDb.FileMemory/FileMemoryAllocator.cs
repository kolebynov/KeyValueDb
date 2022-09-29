using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;
using KeyValueDb.FileMemory.Paging;

namespace KeyValueDb.FileMemory;

public sealed class FileMemoryAllocator
{
	public const int MaxRecordSize = RecordsPage.PagePayload;

	private static readonly int MinPageFreeSpaceForRecord = 8;

	private readonly PageManager _pageManager;
	private readonly FileMappedStructure<FileMemoryAllocatorHeader> _header;

	internal FileMemoryAllocator(FileStream fileStream, PageManager pageManager, long offset, bool forceInitialize)
	{
		_ = fileStream ?? throw new ArgumentNullException(nameof(fileStream));
		_pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));

		_header = new FileMappedStructure<FileMemoryAllocatorHeader>(fileStream, offset, FileMemoryAllocatorHeader.Initial, forceInitialize);
		if (_header.ReadOnlyRef.MemoryBlocksFirstPage.IsInvalid)
		{
			using var header = _header.GetMutableRef();
			using var page = pageManager.AllocatePageBlock(1);
			header.Ref.MemoryBlocksFirstPage = page.PageIndex;
		}
	}

	public Record Create(int size)
	{
		PageBlockAccessor pageBlock;
		using var headerRef = _header.GetMutableRef();
		if (!headerRef.Ref.MemoryBlocksFirstPage.IsInvalid)
		{
			pageBlock = _pageManager.GetAllocatedPageBlock(headerRef.Ref.MemoryBlocksFirstPage);
		}
		else
		{
			pageBlock = AllocateRecordsPage();
			headerRef.Ref.MemoryBlocksFirstPage = pageBlock.PageIndex;
		}

		ushort? recordIndex;
		while (true)
		{
			ref var recordsPage = ref pageBlock.ReadMutable().AsRef<RecordsPage>();
			recordIndex = recordsPage.CreateRecord(size);
			if (recordIndex != null)
			{
				if (pageBlock.PageIndex == headerRef.Ref.MemoryBlocksFirstPage && recordsPage.FreeSpace < MinPageFreeSpaceForRecord)
				{
					using var nextPageWithFreeSpace = GetNextPageWithFreeSpace(headerRef.Ref.MemoryBlocksFirstPage);
					headerRef.Ref.MemoryBlocksFirstPage = nextPageWithFreeSpace.PageIndex;
				}

				break;
			}

			pageBlock.AssignNewDisposableToVariable(GetNextPageWithFreeSpace(pageBlock.PageIndex));
		}

		return new Record(pageBlock, recordIndex.Value);
	}

	public FileMemoryAddress CreateAndSaveData(ReadOnlySpan<byte> recordData)
	{
		using var record = Create(recordData.Length);
		recordData.CopyTo(record.ReadMutable());

		return record.Address;
	}

	public Record Get(FileMemoryAddress fileMemoryAddress) =>
		new(_pageManager.GetAllocatedPageBlock(fileMemoryAddress.PageIndex), fileMemoryAddress.BlockIndex);

	public void Remove(FileMemoryAddress fileMemoryAddress)
	{
		using var page = _pageManager.GetAllocatedPageBlock(fileMemoryAddress.PageIndex);
		ref var recordsPage = ref page.ReadMutable().AsRef<RecordsPage>();
		recordsPage.RemoveRecord(fileMemoryAddress.BlockIndex);

		if (page.PageIndex < _header.ReadOnlyRef.MemoryBlocksFirstPage)
		{
			using var headerRef = _header.GetMutableRef();
			headerRef.Ref.MemoryBlocksFirstPage = page.PageIndex;
		}
	}

	private PageBlockAccessor GetNextPageWithFreeSpace(PageAddress startPageAddress)
	{
		while (true)
		{
			if (!_pageManager.TryGetNextAllocatedPageBlock(startPageAddress, out var page))
			{
				page = AllocateRecordsPage();
			}

			if (page.Read().AsRef<RecordsPage>().FreeSpace >= MinPageFreeSpaceForRecord)
			{
				return page;
			}

			startPageAddress = page.PageIndex;
		}
	}

	private PageBlockAccessor AllocateRecordsPage()
	{
		var page = _pageManager.AllocatePageBlock(TODO);
		var recordsPageInitial = RecordsPage.Initial;
		page.Write(recordsPageInitial.AsBytes());

		return page;
	}
}