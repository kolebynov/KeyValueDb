using System.Runtime.InteropServices;

namespace KeyValueDb.Common.Extensions;

public static class SpanReaderWriterExtensions
{
	public static void WriteInt32(this ref SpanWriter<byte> spanWriter, int value)
	{
		spanWriter.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)));
	}

	public static int ReadInt32(this ref SpanReader<byte> spanReader)
	{
		return spanReader.Read(4).AsRef<int>();
	}
}