using System.Runtime.InteropServices;
using KeyValueDb.FileMemory;
using KeyValueDb.Indexing;

namespace KeyValueDb;

public sealed class Database : IDisposable
{
	private readonly FileStream _dbFileStream;
	private readonly HashMapIndex _index;

	public HashMapIndex Index => _index;

	public Database(string path)
	{
		_dbFileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 0, FileOptions.RandomAccess);
		var forceInitialize = _dbFileStream.Length == 0;
		_index = new HashMapIndex(
			new FileMemoryAllocatorFactory().Create(_dbFileStream, HashMapIndexHeader.Size, forceInitialize),
			_dbFileStream, 0, forceInitialize);
	}

	public byte[]? Get(string key)
	{
		if (!_index.TryGet(key, out var hashMapRecord))
		{
			return null;
		}

		var value = hashMapRecord.Value;
		var result = new byte[value.Length];
		value.CopyTo(result);

		return result;
	}

	public bool TryGet(string key, Span<byte> buffer)
	{
		if (!_index.TryGet(key, out var hashMapRecord))
		{
			return false;
		}

		hashMapRecord.Value.CopyTo(buffer);
		return true;
	}

	public void Set(string key, byte[] value)
	{
		_index.TryAdd(key, value);
	}

	public void Dispose()
	{
		_dbFileStream.Dispose();
	}

	private static ReadOnlySpan<byte> GetKeyBytes(string key) => MemoryMarshal.AsBytes(key.AsSpan());
}