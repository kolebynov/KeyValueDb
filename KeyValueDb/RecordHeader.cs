using System.Runtime.InteropServices;

namespace KeyValueDb;

[StructLayout(LayoutKind.Sequential)]
public readonly struct RecordHeader
{
	public static readonly int Size = Marshal.SizeOf<RecordHeader>();

	public RecordAddress NextRecord { get; }

	public int KeySize { get; }

	public int DataSize { get; }

	public RecordHeader(RecordAddress nextRecord, int keySize, int dataSize)
	{
		NextRecord = nextRecord;
		KeySize = keySize;
		DataSize = dataSize;
	}
}