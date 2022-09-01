using System.Runtime.InteropServices;

namespace KeyValueDb.Records;

[StructLayout(LayoutKind.Sequential)]
public readonly struct RecordHeader
{
	public static readonly int Size = Marshal.SizeOf<RecordHeader>();

	public int DataSize { get; }

	public RecordHeader(int dataSize)
	{
		DataSize = dataSize;
	}
}