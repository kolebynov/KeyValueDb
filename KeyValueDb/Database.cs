using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Paging;
using KeyValueDb.Paging.Extensions;
using KeyValueDb.Paging.ReaderWriter;

namespace KeyValueDb;

public sealed class Database : IDisposable
{
	private readonly FileStream _dbFileStream;
	private readonly PageManager _pageManager;
	private readonly ThreadLocal<byte[]> _tempKeyBuffer = new(() => new byte[32]);
	private DbSystemInfo _systemInfo;

	public Database(string path)
	{
		_dbFileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 0,
			FileOptions.Asynchronous | FileOptions.RandomAccess);
		_pageManager = new PageManager(_dbFileStream, DbSystemInfo.Size);

		if (_dbFileStream.Length == 0)
		{
			InitializeDatabase();
		}

		ReadSystemInfo();
	}

	public byte[]? Get(string key)
	{
		var findResult = Find(GetKeyBytes(key));
		if (findResult == null)
		{
			return null;
		}

		var (recordHeader, reader, _) = findResult.Value;
		var valueArray = new byte[recordHeader.DataSize];
		reader.Read(valueArray);
		return valueArray;
	}

	public bool TryGet(string key, Span<byte> buffer)
	{
		var findResult = Find(GetKeyBytes(key));
		if (findResult == null)
		{
			return false;
		}

		var (recordHeader, reader, _) = findResult.Value;
		if (buffer.Length < recordHeader.DataSize)
		{
			throw new ArgumentException(string.Empty, nameof(buffer));
		}

		reader.Read(buffer[0..recordHeader.DataSize]);
		return true;
	}

	public void Set(string key, byte[] value)
	{
		var keyBytes = GetKeyBytes(key);
		var findResult = Find(keyBytes);
		if (findResult != null)
		{
			var (oldHeader, _, recordAddress) = findResult.Value;
			var pageRewriter = new PageRewriter(_pageManager, recordAddress);
			pageRewriter.WriteStructure(new RecordHeader(oldHeader.NextRecord, keyBytes.Length, value.Length));
			pageRewriter.Write(keyBytes);
			pageRewriter.Write(value);
			pageRewriter.Commit();
			return;
		}

		var pageWriter = new PageWriter(_pageManager);
		pageWriter.WriteStructure(new RecordHeader(BlockAddress.Invalid, keyBytes.Length, value.Length));
		pageWriter.Write(keyBytes);
		pageWriter.Write(value);
		var newRecordAddress = pageWriter.Commit();

		if (_systemInfo.LastRecord != BlockAddress.Invalid)
		{
			using var page = _pageManager.GetPage(_systemInfo.LastRecord.PageIndex);
			var blockIndex = _systemInfo.LastRecord.BlockIndex;
			ref readonly var lastRecordHeader = ref page.Page.GetBlockData(blockIndex, length: RecordHeader.Size)
				.AsRef<RecordHeader>();
			var updatedHeader = new RecordHeader(newRecordAddress, lastRecordHeader.KeySize, lastRecordHeader.DataSize);
			page.Page.SetBlockData(blockIndex, updatedHeader.AsReadOnlyBytes());
		}

		_systemInfo.LastRecord = newRecordAddress;

		if (_systemInfo.FirstRecord == BlockAddress.Invalid)
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

		return true;
	}

	public void Dispose()
	{
		_dbFileStream.Dispose();
		_tempKeyBuffer.Dispose();
	}

	private (RecordHeader Header, PageReader Reader, BlockAddress RecordAddress)? Find(ReadOnlySpan<byte> key)
	{
		if (_systemInfo.FirstRecord == BlockAddress.Invalid)
		{
			return null;
		}

		var recordAddress = _systemInfo.FirstRecord;
		var recordKey = _tempKeyBuffer.Value!;
		while (recordAddress != BlockAddress.Invalid)
		{
			var reader = new PageReader(_pageManager, recordAddress);
			reader.ReadStructure(out RecordHeader recordHeader);

			if (recordHeader.KeySize == key.Length)
			{
				if (recordHeader.KeySize > recordKey.Length)
				{
					Array.Resize(ref recordKey, recordHeader.KeySize);
					_tempKeyBuffer.Value = recordKey;
				}

				var keySpan = recordKey.AsSpan(..recordHeader.KeySize);
				reader.Read(keySpan);

				if (keySpan.SequenceEqual(key))
				{
					return (recordHeader, reader, recordAddress);
				}
			}

			recordAddress = recordHeader.NextRecord;
		}

		return null;
	}

	private void InitializeDatabase()
	{
		_systemInfo = new DbSystemInfo
		{
			FirstRecord = BlockAddress.Invalid,
			LastRecord = BlockAddress.Invalid,
		};
		WriteSystemInfo();
	}

	private void ReadSystemInfo()
	{
		_dbFileStream.ReadStructure(0, out _systemInfo);
	}

	private void WriteSystemInfo()
	{
		_dbFileStream.WriteStructure(0, ref _systemInfo);
	}

	private static ReadOnlySpan<byte> GetKeyBytes(string key)
	{
		return MemoryMarshal.AsBytes(key.AsSpan());
	}
}