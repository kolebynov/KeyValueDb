using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using KeyValueDb.Common;

namespace KeyValueDb.FileMemory.Paging;

internal sealed class AllocatedPageRangeList
{
	private const int MaxPageRangeCount = 1023;

	private readonly FileMappedStructure<FixedList<FileData, PageRange>> _fileData;

	public int FileDataSize => Marshal.SizeOf<FileData>();

	public PageRange LastAllocatedPageRange => _fileData.ReadOnlyRef.ListProvider.LastAllocatedPage;

	public AllocatedPageRangeList(FileStream fileStream, long offset, bool forceInitialize)
	{
		_fileData = new FileMappedStructure<FixedList<FileData, PageRange>>(fileStream, offset,
			new FixedList<FileData, PageRange>(FileData.Initial, PageRange.Invalid), forceInitialize);
	}

	public void Add(PageRange pageRange)
	{
		using var fileDataRef = _fileData.GetMutableRef();
		fileDataRef.Ref.Add(pageRange);
		if (fileDataRef.Ref.ListProvider.LastAllocatedPage.PageIndex < pageRange.PageIndex
			|| fileDataRef.Ref.ListProvider.LastAllocatedPage.IsInvalid)
		{
			fileDataRef.Ref.ListProvider.LastAllocatedPage = pageRange;
		}
	}

	public void Remove(PageRange pageRange)
	{
		using var fileDataRef = _fileData.GetMutableRef();
		fileDataRef.Ref.Remove(pageRange);

		if (!pageRange.Equals(fileDataRef.Ref.ListProvider.LastAllocatedPage))
		{
			return;
		}

		fileDataRef.Ref.ListProvider.LastAllocatedPage = PageRange.Invalid;

		foreach (var allocatedPageRange in fileDataRef.Ref.ItemsReadOnly)
		{
			if (fileDataRef.Ref.ListProvider.LastAllocatedPage.PageIndex < allocatedPageRange.PageIndex
				|| fileDataRef.Ref.ListProvider.LastAllocatedPage.IsInvalid)
			{
				fileDataRef.Ref.ListProvider.LastAllocatedPage = allocatedPageRange;
			}
		}
	}

	public bool TryGetByPageIndex(PageIndex pageIndex, out PageRange pageRange)
	{
		foreach (var allocatedPageRange in _fileData.ReadOnlyRef.ItemsReadOnly)
		{
			if (allocatedPageRange.PageIndex == pageIndex)
			{
				pageRange = allocatedPageRange;
				return true;
			}
		}

		pageRange = PageRange.Invalid;
		return false;
	}

	public bool TryRemoveByPageIndex(PageIndex pageIndex, out PageRange pageRange)
	{
		foreach (var allocatedPageRange in _fileData.ReadOnlyRef.ItemsReadOnly)
		{
			if (allocatedPageRange.PageIndex == pageIndex)
			{
				Remove(allocatedPageRange);
				pageRange = allocatedPageRange;
				return true;
			}
		}

		pageRange = PageRange.Invalid;
		return false;
	}

	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct FileData : IFixedListProvider<PageRange>
	{
		private const int PageRangeListSize = MaxPageRangeCount * PageRange.Size;

		public PageRange LastAllocatedPage;
#pragma warning disable CS0649
		private fixed byte _blockList[PageRangeListSize];
#pragma warning restore CS0649

		public Span<PageRange> List => MemoryMarshal.CreateSpan(ref _blockList[0], PageRangeListSize).Cast<byte, PageRange>();

		public static FileData Initial { get; } = new() { LastAllocatedPage = PageRange.Invalid };
	}
}