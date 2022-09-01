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
		_recordManager = new RecordManager(_dbFileStream, new PageManager(_dbFileStream, RecordManagerHeader.Size));
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
		_recordManager.Get(recordAddress).CopyTo(valueArray);

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

		_recordManager.Get(recordAddress).CopyTo(buffer);

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
			var (oldHeader, recordAddress) = findResult.Value;
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

		var (_, address) = findResult.Value;
		_recordManager.Remove(address);

		return true;
	}

	public void Dispose()
	{
		_dbFileStream.Dispose();
	}

	private (RecordHeader Header, RecordAddress RecordAddress)? Find(ReadOnlySpan<byte> key)
	{
		return null;
	}

	private static ReadOnlySpan<byte> GetKeyBytes(string key) => MemoryMarshal.AsBytes(key.AsSpan());
}