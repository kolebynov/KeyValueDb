using System.Runtime.InteropServices;
using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Paging;

namespace KeyValueDb;

public sealed class Database : IDisposable
{
	private static readonly int MinPageFreeSpaceForRecord = RecordHeader.Size + 8;

	private readonly FileStream _dbFileStream;
	private readonly PageManager _pageManager;
	private readonly FileMappedStructure<DbSystemInfo> _systemInfo;

	public Database(string path)
	{
		_dbFileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 0,
			FileOptions.Asynchronous | FileOptions.RandomAccess);
		_systemInfo = new FileMappedStructure<DbSystemInfo>(_dbFileStream, 0, DbSystemInfo.Initial);

		if (_dbFileStream.Length == 0)
		{
			_systemInfo.Write();
		}

		_systemInfo.Read();

		_pageManager = new PageManager(_dbFileStream, DbSystemInfo.Size);
	}

	public byte[]? Get(string key)
	{
		var findResult = Find(GetKeyBytes(key));
		if (findResult == null)
		{
			return null;
		}

		var (recordHeader, recordAddress) = findResult.Value;
		var valueArray = new byte[recordHeader.DataSize];
		ref readonly var recordsPage = ref _pageManager.GetAllocatedPage(recordAddress.PageIndex).Read().AsRef<RecordsPage>();
		var record = recordsPage.GetRecord(recordAddress.RecordIndex);
		record.Data.CopyTo(valueArray);

		return valueArray;
	}

	public bool TryGet(string key, Span<byte> buffer)
	{
		var findResult = Find(GetKeyBytes(key));
		if (findResult == null)
		{
			return false;
		}

		var (recordHeader, recordAddress) = findResult.Value;
		if (buffer.Length < recordHeader.DataSize)
		{
			throw new ArgumentException(string.Empty, nameof(buffer));
		}

		ref readonly var recordsPage = ref _pageManager.GetAllocatedPage(recordAddress.PageIndex).Read().AsRef<RecordsPage>();
		var record = recordsPage.GetRecord(recordAddress.RecordIndex);
		record.Data.CopyTo(buffer);

		return true;
	}

	public void Set(string key, byte[] value)
	{
		var keyBytes = GetKeyBytes(key);

		if (keyBytes.Length > RecordsPage.PagePayload)
		{
			// TODO: Add handling of values that are bigger than page payload
			throw new NotImplementedException("Add handling of values that are bigger than page payload");
		}

		var findResult = Find(keyBytes);
		if (findResult != null)
		{
			var (oldHeader, recordAddress) = findResult.Value;
			return;
		}

		PageAccessor page;
		using var systemInfoRef = _systemInfo.GetMutableRef();
		if (systemInfoRef.Ref.FirstPageWithFreeSpace != PageIndex.Invalid)
		{
			page = _pageManager.GetAllocatedPage(systemInfoRef.Ref.FirstPageWithFreeSpace);
		}
		else
		{
			page = AllocateRecordsPage();
			systemInfoRef.Ref.FirstPageWithFreeSpace = page.PageIndex;
		}

		var header = new RecordHeader(RecordAddress.Invalid, keyBytes.Length, value.Length);
		var recordData = new RecordsPage.RecordData(in header, keyBytes, value);
		ushort? recordIndex;
		while (true)
		{
			ref var recordsPage = ref page.ReadMutable().AsRef<RecordsPage>();
			recordIndex = recordsPage.AddRecord(recordData);
			if (recordIndex != null)
			{
				if (page.PageIndex == systemInfoRef.Ref.FirstPageWithFreeSpace && recordsPage.FreeSpace < MinPageFreeSpaceForRecord)
				{
					systemInfoRef.Ref.FirstPageWithFreeSpace = GetNextPageWithFreeSpace(systemInfoRef.Ref.FirstPageWithFreeSpace).PageIndex;
				}

				break;
			}

			page = GetNextPageWithFreeSpace(page.PageIndex);
		}

		var newRecordAddress = new RecordAddress(page.PageIndex, recordIndex.Value);

		if (systemInfoRef.Ref.LastRecord != RecordAddress.Invalid)
		{
			using var prevPage = _pageManager.GetAllocatedPage(systemInfoRef.Ref.LastRecord.PageIndex);
			ref var prevRecordsPage = ref prevPage.ReadMutable().AsRef<RecordsPage>();
			prevRecordsPage.UpdateNextRecordAddress(systemInfoRef.Ref.LastRecord.RecordIndex, newRecordAddress);
		}

		page.Dispose();

		systemInfoRef.Ref.LastRecord = newRecordAddress;

		if (systemInfoRef.Ref.FirstRecord == RecordAddress.Invalid)
		{
			systemInfoRef.Ref.FirstRecord = newRecordAddress;
		}
	}

	public bool Remove(string key)
	{
		var findResult = Find(GetKeyBytes(key));
		if (findResult == null)
		{
			return false;
		}

		var (_, address) = findResult.Value;
		using var page = _pageManager.GetAllocatedPage(address.PageIndex);
		ref var recordsPage = ref page.ReadMutable().AsRef<RecordsPage>();
		recordsPage.RemoveRecord(address.RecordIndex);

		if (page.PageIndex < _systemInfo.ReadOnlyRef.FirstPageWithFreeSpace)
		{
			using var systemInfoRef = _systemInfo.GetMutableRef();
			systemInfoRef.Ref.FirstPageWithFreeSpace = page.PageIndex;
		}

		return true;
	}

	public void Dispose()
	{
		_dbFileStream.Dispose();
		_pageManager.Dispose();
	}

	private (RecordHeader Header, RecordAddress RecordAddress)? Find(ReadOnlySpan<byte> key)
	{
		if (_systemInfo.ReadOnlyRef.FirstRecord == RecordAddress.Invalid)
		{
			return null;
		}

		var recordAddress = _systemInfo.ReadOnlyRef.FirstRecord;
		while (recordAddress != RecordAddress.Invalid)
		{
			var page = _pageManager.GetAllocatedPage(recordAddress.PageIndex);
			ref readonly var recordsPage = ref page.Read().AsRef<RecordsPage>();
			var record = recordsPage.GetRecord(recordAddress.RecordIndex);

			if (record.Header.KeySize == key.Length && record.Key.SequenceEqual(key))
			{
				return (record.Header, recordAddress);
			}

			recordAddress = record.Header.NextRecord;
		}

		return null;
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

	private static ReadOnlySpan<byte> GetKeyBytes(string key) => MemoryMarshal.AsBytes(key.AsSpan());
}