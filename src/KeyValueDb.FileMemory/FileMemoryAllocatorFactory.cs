using KeyValueDb.FileMemory.Paging;

namespace KeyValueDb.FileMemory;

public sealed class FileMemoryAllocatorFactory
{
	public FileMemoryAllocator Create(FileStream fileStream, long offset, bool forceInitialize) =>
		new(fileStream, new PageManager(fileStream, offset + FileMemoryAllocatorHeader.Size, forceInitialize), offset, forceInitialize);
}