using KeyValueDb.FileMemory;

namespace KeyValueDb.Indexing;

public readonly struct HashMapRecord : IDisposable
{
	private readonly AllocatedMemory _record;

	public ReadOnlySpan<byte> Value
	{
		get
		{
			var recordData = RecordData.DeserializeFromSpan(_record.Data);
			return recordData.Value;
		}
	}

	public HashMapRecord(AllocatedMemory record)
	{
		_record = record;
	}

	public void Dispose()
	{
		_record.Dispose();
	}
}