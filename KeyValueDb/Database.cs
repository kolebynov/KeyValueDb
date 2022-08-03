using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Paging;

namespace KeyValueDb;

public sealed class Database : IDisposable
{
	private static readonly int MinPageFreeSpaceForRecord = RecordHeader.Size + 8;

	private readonly FileStream _dbFileStream;
	private readonly PageManager _pageManager;
	private DbSystemInfo _systemInfo;

	public Database(string path)
	{
		_dbFileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 0,
			FileOptions.Asynchronous | FileOptions.RandomAccess);

		if (_dbFileStream.Length == 0)
		{
			InitializeDatabase();
		}

		ReadSystemInfo();

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
		if (_systemInfo.FirstPageWithFreeSpace != Constants.InvalidPageIndex)
		{
			page = _pageManager.GetAllocatedPage(_systemInfo.FirstPageWithFreeSpace);
		}
		else
		{
			page = AllocateRecordsPage();
			_systemInfo.FirstPageWithFreeSpace = page.PageIndex;
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
				if (page.PageIndex == _systemInfo.FirstPageWithFreeSpace && recordsPage.FreeSpace < MinPageFreeSpaceForRecord)
				{
					_systemInfo.FirstPageWithFreeSpace = GetNextPageWithFreeSpace(_systemInfo.FirstPageWithFreeSpace).PageIndex;
				}

				break;
			}

			page = GetNextPageWithFreeSpace(page.PageIndex);
		}

		var newRecordAddress = new RecordAddress(page.PageIndex, recordIndex.Value);

		if (_systemInfo.LastRecord != RecordAddress.Invalid)
		{
			var prevPage = _pageManager.GetAllocatedPage(_systemInfo.LastRecord.PageIndex);
			ref var prevRecordsPage = ref prevPage.ReadMutable().AsRef<RecordsPage>();
			prevRecordsPage.UpdateNextRecordAddress(_systemInfo.LastRecord.RecordIndex, newRecordAddress);
			prevPage.Commit();
		}

		page.Commit();

		_systemInfo.LastRecord = newRecordAddress;

		if (_systemInfo.FirstRecord == RecordAddress.Invalid)
		{
			_systemInfo.FirstRecord = newRecordAddress;
		}

		WriteSystemInfo();
	}

	public bool Remove(string key)
	{
		var findResult = Find(GetKeyBytes(key));
		if (findResult == null)
		{
			return false;
		}

		var (_, address) = findResult.Value;
		var page = _pageManager.GetAllocatedPage(address.PageIndex);
		ref var recordsPage = ref page.ReadMutable().AsRef<RecordsPage>();
		recordsPage.RemoveRecord(address.RecordIndex);
		page.Commit();

		if (page.PageIndex < _systemInfo.FirstPageWithFreeSpace)
		{
			_systemInfo.FirstPageWithFreeSpace = page.PageIndex;
			WriteSystemInfo();
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
		if (_systemInfo.FirstRecord == RecordAddress.Invalid)
		{
			return null;
		}

		var recordAddress = _systemInfo.FirstRecord;
		while (recordAddress != RecordAddress.Invalid)
		{
			var page = _pageManager.GetAllocatedPage(recordAddress.PageIndex);
			ref readonly var recordsPage = ref page.Read().AsRef<RecordsPage>();
			var record = recordsPage.GetRecord(recordAddress.RecordIndex);

			if (record.Header.KeySize == key.Length)
			{
				if (record.Key.SequenceEqual(key))
				{
					return (record.Header, recordAddress);
				}
			}

			recordAddress = record.Header.NextRecord;
		}

		return null;
	}

	private PageAccessor GetNextPageWithFreeSpace(uint startPageIndex)
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
		page.Write(recordsPageInitial.AsReadOnlyBytes());

		return page;
	}

	private void InitializeDatabase()
	{
		_systemInfo = DbSystemInfo.Initial;
		WriteSystemInfo();
	}

	private void ReadSystemInfo() => _dbFileStream.ReadStructure(0, ref _systemInfo);

	private void WriteSystemInfo() => _dbFileStream.WriteStructure(0, ref _systemInfo);

	private static ReadOnlySpan<byte> GetKeyBytes(string key) => MemoryMarshal.AsBytes(key.AsSpan());
}