using CommunityToolkit.HighPerformance;
using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;

namespace KeyValueDb.Indexing;

internal readonly ref struct RecordData
{
	public readonly ReadOnlySpan<char> Key;

	public readonly ReadOnlySpan<byte> Value;

	public int Size => 4 + Key.Cast<char, byte>().Length + Value.Length;

	public RecordData(ReadOnlySpan<char> key, ReadOnlySpan<byte> value)
	{
		Key = key;
		Value = value;
	}

	public void SerializeToSpan(Span<byte> span)
	{
		var spanWriter = new SpanWriter<byte>(span);
		var keyBytes = Key.Cast<char, byte>();

		spanWriter.WriteInt32(keyBytes.Length);
		spanWriter.Write(keyBytes);
		spanWriter.Write(Value);
	}

	public static RecordData DeserializeFromSpan(ReadOnlySpan<byte> recordData)
	{
		var spanReader = new SpanReader<byte>(recordData);
		var keySize = spanReader.ReadInt32();
		return new RecordData(spanReader.Read(keySize).Cast<byte, char>(), spanReader.ReadToEnd());
	}
}