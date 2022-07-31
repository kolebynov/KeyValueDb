using System.Runtime.InteropServices;
using KeyValueDb.Paging;

namespace KeyValueDb;

[StructLayout(LayoutKind.Sequential)]
public readonly struct RecordHeader
{
	public static readonly int Size = Marshal.SizeOf<RecordHeader>();

	public BlockAddress NextRecord { get; }

	public int KeySize { get; }

	public int DataSize { get; }

	public RecordHeader(BlockAddress nextRecord, int keySize, int dataSize)
	{
		NextRecord = nextRecord;
		KeySize = keySize;
		DataSize = dataSize;
	}
}