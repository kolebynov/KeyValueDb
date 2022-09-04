using System.Runtime.InteropServices;
using KeyValueDb.Common;
using KeyValueDb.Paging;
using KeyValueDb.Records;

namespace KeyValueDb;

public sealed class Database : IDisposable
{
	private readonly FileStream _dbFileStream;
	private readonly RecordManager _recordManager;

	public RecordManager RecordManager => _recordManager;

	public Database(string path)
	{
		_dbFileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 0,
			FileOptions.Asynchronous | FileOptions.RandomAccess);
		var forceInitialize = _dbFileStream.Length == 0;
		_recordManager = new RecordManager(_dbFileStream, new PageManager(_dbFileStream, RecordManagerHeader.Size, forceInitialize),
			forceInitialize);
	}

	public byte[]? Get(string key)
	{
		var findRecordAddress = Find(GetKeyBytes(key));
		if (findRecordAddress == null)
		{
			return null;
		}

		var record = _recordManager.Get(findRecordAddress.Value);
		var valueArray = new byte[record.Length];
		record.CopyTo(valueArray);

		return valueArray;
	}

	public bool TryGet(string key, Span<byte> buffer)
	{
		var findRecordAddress = Find(GetKeyBytes(key));
		if (findRecordAddress == null)
		{
			return false;
		}

		var record = _recordManager.Get(findRecordAddress.Value);
		if (buffer.Length < record.Length)
		{
			throw new ArgumentException(string.Empty, nameof(buffer));
		}

		record.CopyTo(buffer);

		return true;
	}

	public void Set(string key, byte[] value)
	{
		var keyBytes = GetKeyBytes(key);

		if (keyBytes.Length + value.Length > RecordsPage.PagePayload)
		{
			// TODO: Add handling of values that are bigger than page payload
			throw new NotImplementedException("Add handling of values that are bigger than page payload");
		}

		var findResult = Find(keyBytes);
		if (findResult != null)
		{
			return;
		}

		var record = new byte[keyBytes.Length + value.Length];
		var spanWriter = new SpanWriter<byte>(record);
		spanWriter.Write(keyBytes);
		spanWriter.Write(value);
		_recordManager.Add(record);
	}

	public bool Remove(string key)
	{
		var findResult = Find(GetKeyBytes(key));
		if (findResult == null)
		{
			return false;
		}

		_recordManager.Remove(findResult.Value);

		return true;
	}

	public void Dispose()
	{
		_dbFileStream.Dispose();
	}

	private RecordAddress? Find(ReadOnlySpan<byte> key)
	{
		return null;
	}

	private static ReadOnlySpan<byte> GetKeyBytes(string key) => MemoryMarshal.AsBytes(key.AsSpan());
}