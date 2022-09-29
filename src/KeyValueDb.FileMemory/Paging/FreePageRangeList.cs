using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;

namespace KeyValueDb.FileMemory.Paging;

internal sealed class FreePageRangeList
{
	private const int MaxFreePagesCount = 1023;

	private readonly FileMappedStructure<FixedList<FileData, PageRange>> _fileData;

	public int FileDataSize => Unsafe.SizeOf<FixedList<FileData, PageRange>>();

	public FreePageRangeList(FileStream fileStream, long offset, bool forceInitialize)
	{
		_fileData = new FileMappedStructure<FixedList<FileData, PageRange>>(fileStream, offset,
			new FixedList<FileData, PageRange>(default, PageRange.Invalid), forceInitialize);
	}

	public void Add(PageRange pageRange)
	{
		using var fileDataRef = _fileData.GetMutableRef();
		for (var i = 0; i < _fileData.ReadOnlyRef.ItemsReadOnly.Length; i++)
		{
			var freePagesRange = _fileData.ReadOnlyRef.ItemsReadOnly[i];
			if (freePagesRange.IsInRange(pageRange.PageIndex))
			{
				throw new ArgumentException("Page index already is in free pages list", nameof(pageRange.PageIndex));
			}

			if (freePagesRange.IsEndOfRange(pageRange.PageIndex))
			{
				fileDataRef.Ref.Items[i] = new PageRange(freePagesRange.PageIndex, freePagesRange.PageCount + pageRange.PageCount);
				return;
			}
		}

		fileDataRef.Ref.Add(pageRange);
	}

	public bool IsFreePageBlock(PageIndex pageIndex)
	{
		foreach (var freePagesRange in _fileData.ReadOnlyRef.ItemsReadOnly)
		{
			if (freePagesRange.IsInRange(pageIndex))
			{
				return true;
			}
		}

		return false;
	}

	public bool TryGetByPageIndex(PageIndex freePageIndex, out PageRange pageRange)
	{
		foreach (var freePageBlock in _fileData.ReadOnlyRef.ItemsReadOnly)
		{
			if (freePageBlock.IsInRange(freePageIndex))
			{
				pageRange = freePageBlock;
				return true;
			}
		}

		pageRange = PageRange.Invalid;
		return false;
	}

	public bool TryRemoveByPageIndex(PageIndex freePageIndex, out PageRange pageRange)
	{
		for (var i = 0; i < _fileData.ReadOnlyRef.ItemsReadOnly.Length; i++)
		{
			var freePageBlock = _fileData.ReadOnlyRef.ItemsReadOnly[i];
			if (freePageBlock.IsInRange(freePageIndex))
			{
				using var fileDataRef = _fileData.GetMutableRef();
				fileDataRef.Ref.RemoveAt(i);
				pageRange = freePageBlock;
				return true;
			}
		}

		pageRange = PageRange.Invalid;
		return false;
	}

	public bool TryRemoveFreePageBlock(int pageCount, out PageIndex pageIndex)
	{
		using var fileDataRef = _fileData.GetMutableRef();
		var moreFreePagesIndex = -1;
		for (var i = 0; i < _fileData.ReadOnlyRef.ItemsReadOnly.Length; i++)
		{
			var freePagesRange = _fileData.ReadOnlyRef.ItemsReadOnly[i];
			if (freePagesRange.PageCount == pageCount)
			{
				fileDataRef.Ref.RemoveAt(i);
				pageIndex = freePagesRange.PageIndex;
				return true;
			}

			if (freePagesRange.PageCount > pageCount && moreFreePagesIndex == -1)
			{
				moreFreePagesIndex = i;
			}
		}

		if (moreFreePagesIndex != -1)
		{
			var freePagesRange = fileDataRef.Ref.Items[moreFreePagesIndex];
			fileDataRef.Ref.Items[moreFreePagesIndex] =
				new PageRange(freePagesRange.PageIndex + pageCount, freePagesRange.PageCount - pageCount);
			pageIndex = freePagesRange.PageIndex;
			return true;
		}

		pageIndex = PageIndex.Invalid;
		return false;
	}

	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct FileData : IFixedListProvider<PageRange>
	{
		private const int FreePagesListSize = MaxFreePagesCount * PageRange.Size;

#pragma warning disable CS0649
		private fixed byte _freePagesList[FreePagesListSize];
#pragma warning restore CS0649

		public Span<PageRange> List => MemoryMarshal.CreateSpan(ref _freePagesList[0], FreePagesListSize).Cast<byte, PageRange>();
	}
}