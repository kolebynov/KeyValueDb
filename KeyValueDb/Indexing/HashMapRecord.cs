using KeyValueDb.Records;

namespace KeyValueDb.Indexing;

public readonly struct HashMapRecord : IDisposable
{
	private readonly RecordManager.Record _record;

	public ReadOnlySpan<byte> Value
	{
		get
		{
			var recordData = RecordData.DeserializeFromSpan(_record.Read());
			return recordData.Value;
		}
	}

	public HashMapRecord(RecordManager.Record record)
	{
		_record = record;
	}

	public void Dispose()
	{
		_record.Dispose();
	}
}