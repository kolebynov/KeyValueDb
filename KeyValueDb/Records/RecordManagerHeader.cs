using System.Runtime.InteropServices;
using KeyValueDb.Paging;

namespace KeyValueDb.Records;

public struct RecordManagerHeader
{
	public static readonly int Size = Marshal.SizeOf<RecordManagerHeader>();

	public PageIndex FirstPageWithFreeSpace;

	public static RecordManagerHeader Initial { get; } = new()
	{
		FirstPageWithFreeSpace = PageIndex.Invalid,
	};
}