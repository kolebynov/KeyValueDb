using System.Runtime.InteropServices;
using KeyValueDb.FileMemory.Paging;

namespace KeyValueDb.FileMemory;

internal struct FileMemoryAllocatorHeader
{
	public static readonly int Size = Marshal.SizeOf<FileMemoryAllocatorHeader>();

	public PageIndex MemoryBlocksFirstPage;

	public static FileMemoryAllocatorHeader Initial { get; } = new()
	{
		MemoryBlocksFirstPage = PageIndex.Invalid,
	};
}