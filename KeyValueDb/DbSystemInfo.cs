using System.Runtime.InteropServices;
using KeyValueDb.Paging;

namespace KeyValueDb;

public struct DbSystemInfo
{
	public static readonly int Size = Marshal.SizeOf<DbSystemInfo>();

	public RecordAddress FirstRecord;

	public RecordAddress LastRecord;

	public PageIndex FirstPageWithFreeSpace;

	public static DbSystemInfo Initial { get; } = new()
	{
		FirstRecord = RecordAddress.Invalid,
		LastRecord = RecordAddress.Invalid,
		FirstPageWithFreeSpace = PageIndex.Invalid,
	};
}