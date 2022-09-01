using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Paging;

namespace KeyValueDb.Records;

public sealed class RecordManager
{
	private static readonly int MinPageFreeSpaceForRecord = RecordHeader.Size + 8;

	private readonly PageManager _pageManager;
	private readonly FileMappedStructure<RecordManagerHeader> _header;

	public RecordManager(FileStream dbFileStream, PageManager pageManager)
	{
		_ = dbFileStream ?? throw new ArgumentNullException(nameof(dbFileStream));
		_pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));

		_header = new FileMappedStructure<RecordManagerHeader>(dbFileStream, 0, RecordManagerHeader.Initial);

		if (dbFileStream.Length <= RecordManagerHeader.Size + PageManager.HeaderSize)
		{
			_header.Write();
		}

		_header.Read();
	}

	public RecordAddress Add(ReadOnlySpan<byte> record)
	{
		PageAccessor page;
		using var headerRef = _header.GetMutableRef();
		if (!headerRef.Ref.FirstPageWithFreeSpace.IsInvalid)
		{
			page = _pageManager.GetAllocatedPage(headerRef.Ref.FirstPageWithFreeSpace);
		}
		else
		{
			page = AllocateRecordsPage();
			headerRef.Ref.FirstPageWithFreeSpace = page.PageIndex;
		}

		ushort? recordIndex;
		while (true)
		{
			ref var recordsPage = ref page.ReadMutable().AsRef<RecordsPage>();
			recordIndex = recordsPage.AddRecord(record);
			if (recordIndex != null)
			{
				if (page.PageIndex == headerRef.Ref.FirstPageWithFreeSpace && recordsPage.FreeSpace < MinPageFreeSpaceForRecord)
				{
					headerRef.Ref.FirstPageWithFreeSpace = GetNextPageWithFreeSpace(headerRef.Ref.FirstPageWithFreeSpace).PageIndex;
				}

				break;
			}

			page = GetNextPageWithFreeSpace(page.PageIndex);
		}

		page.Dispose();

		return new RecordAddress(page.PageIndex, recordIndex.Value);
	}

	public ReadOnlySpan<byte> Get(RecordAddress recordAddress)
	{
		ref readonly var recordsPage = ref _pageManager.GetAllocatedPage(recordAddress.PageIndex).Read().AsRef<RecordsPage>();
		return recordsPage.GetRecord(recordAddress.RecordIndex);
	}

	public void Remove(RecordAddress recordAddress)
	{
		using var page = _pageManager.GetAllocatedPage(recordAddress.PageIndex);
		ref var recordsPage = ref page.ReadMutable().AsRef<RecordsPage>();
		recordsPage.RemoveRecord(recordAddress.RecordIndex);

		if (page.PageIndex < _header.ReadOnlyRef.FirstPageWithFreeSpace)
		{
			using var headerRef = _header.GetMutableRef();
			headerRef.Ref.FirstPageWithFreeSpace = page.PageIndex;
		}
	}

	private PageAccessor GetNextPageWithFreeSpace(PageIndex startPageIndex)
	{
		while (true)
		{
			if (!_pageManager.TryGetNextAllocatedPage(startPageIndex, out var page))
			{
				page = AllocateRecordsPage();
			}

			if (page.Read().AsRef<RecordsPage>().FreeSpace >= MinPageFreeSpaceForRecord)
			{
				return page;
			}

			startPageIndex = page.PageIndex;
		}
	}

	private PageAccessor AllocateRecordsPage()
	{
		var page = _pageManager.AllocatePage();
		var recordsPageInitial = RecordsPage.Initial;
		page.Write(recordsPageInitial.AsBytes());

		return page;
	}
}