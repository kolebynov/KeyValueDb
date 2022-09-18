using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Paging;

namespace KeyValueDb.Records;

public sealed class RecordManager
{
	public const int MaxRecordSize = RecordsPage.PagePayload;

	private static readonly int MinPageFreeSpaceForRecord = 8;

	private readonly PageManager _pageManager;
	private readonly FileMappedStructure<RecordManagerHeader> _header;

	public RecordManager(FileStream dbFileStream, PageManager pageManager, long offset, bool forceInitialize)
	{
		_ = dbFileStream ?? throw new ArgumentNullException(nameof(dbFileStream));
		_pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));

		_header = new FileMappedStructure<RecordManagerHeader>(dbFileStream, offset, RecordManagerHeader.Initial, forceInitialize);
	}

	public Record Create(int size)
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
			recordIndex = recordsPage.CreateRecord(size);
			if (recordIndex != null)
			{
				if (page.PageIndex == headerRef.Ref.FirstPageWithFreeSpace && recordsPage.FreeSpace < MinPageFreeSpaceForRecord)
				{
					using var nextPageWithFreeSpace = GetNextPageWithFreeSpace(headerRef.Ref.FirstPageWithFreeSpace);
					headerRef.Ref.FirstPageWithFreeSpace = nextPageWithFreeSpace.PageIndex;
				}

				break;
			}

			page.AssignNewDisposableToVariable(GetNextPageWithFreeSpace(page.PageIndex));
		}

		return new Record(page, recordIndex.Value);
	}

	public RecordAddress CreateAndSaveData(ReadOnlySpan<byte> recordData)
	{
		using var record = Create(recordData.Length);
		recordData.CopyTo(record.ReadMutable());

		return record.Address;
	}

	public Record Get(RecordAddress recordAddress) =>
		new(_pageManager.GetAllocatedPage(recordAddress.PageIndex), recordAddress.RecordIndex);

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

	public readonly struct Record : IDisposable
	{
		private readonly PageAccessor _page;
		private readonly ushort _recordIndex;

		public RecordAddress Address => new(_page.PageIndex, _recordIndex);

		public Record(PageAccessor page, ushort recordIndex)
		{
			_page = page;
			_recordIndex = recordIndex;
		}

		public ReadOnlySpan<byte> Read() => _page.Read().AsRef<RecordsPage>().GetRecord(_recordIndex);

		public Span<byte> ReadMutable() => _page.ReadMutable().AsRef<RecordsPage>().GetRecordMutable(_recordIndex);

		public void Dispose() => _page.Dispose();
	}

	public readonly struct MutableRecord : IDisposable
	{
		private readonly PageAccessor _page;
		private readonly ushort _recordIndex;

		public Span<byte> Data => _page.ReadMutable().AsRef<RecordsPage>().GetRecordMutable(_recordIndex);

		public MutableRecord(PageAccessor page, ushort recordIndex)
		{
			_page = page;
			_recordIndex = recordIndex;
		}

		public void Dispose() => _page.Dispose();
	}
}