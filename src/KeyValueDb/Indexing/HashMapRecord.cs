using KeyValueDb.FileMemory;

namespace KeyValueDb.Indexing;

public readonly struct HashMapRecord : IDisposable
{
	private readonly FileMemoryAllocator.Record _record;

	public ReadOnlySpan<byte> Value
	{
		get
		{
			var recordData = RecordData.DeserializeFromSpan(_record.Read());
			return recordData.Value;
		}
	}

	public HashMapRecord(FileMemoryAllocator.Record record)
	{
		_record = record;
	}

	public void Dispose()
	{
		_record.Dispose();
	}
}