using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;

namespace KeyValueDb.Paging;

public sealed class PageManager
{
	private const int MaxPageCount = int.MaxValue;

	private readonly FileStream _dbFileStream;
	private readonly long _headerOffset;
	private readonly long _firstPageOffset;
	private readonly Dictionary<int, Page> _cachedPages = new();
	private PagesHeader _header;
	private Page _firstPageWithFreeBlocks;

	public PageManager(FileStream dbFileStream, long offset)
	{
		_dbFileStream = dbFileStream ?? throw new ArgumentNullException(nameof(dbFileStream));
		_headerOffset = offset;
		_firstPageOffset = offset + Marshal.SizeOf<PagesHeader>();

		if (_dbFileStream.Length <= offset)
		{
			_header = default;
		}
		else
		{
			_dbFileStream.ReadStructure(offset, out _header);
		}

		_firstPageWithFreeBlocks = ReadPage(_header.FirstPageWithFreeBlocks);
	}

	public PageAccessor GetPageWithFreeBlocks(int startIndex)
	{
		return startIndex <= _firstPageWithFreeBlocks.Index
			? new PageAccessor(_firstPageWithFreeBlocks, this)
			: GetPage(FindPageWithFreeBlocks(startIndex));
	}

	public PageAccessor GetPage(int index)
	{
		return new PageAccessor(GetPageInternal(index), this);
	}

	private Page GetPageInternal(int index)
	{
		CheckPageIndex(index);

		if (index == _firstPageWithFreeBlocks.Index)
		{
			return _firstPageWithFreeBlocks;
		}

		if (_cachedPages.TryGetValue(index, out var page))
		{
			return page;
		}

		return _cachedPages[index] = ReadPage(index);
	}

	private Page ReadPage(int index)
	{
		var pageAddress = GetPageAddress(index);

		PageData pageData;
		if (_dbFileStream.Length <= pageAddress)
		{
			pageData = default;
		}
		else
		{
			_dbFileStream.ReadStructure(pageAddress, out pageData);
		}

		return new Page(ref pageData, index);
	}

	private void CommitPage(Page page)
	{
		if (!page.HasChanges)
		{
			return;
		}

		_dbFileStream.WriteStructure(GetPageAddress(page.Index), ref page.PageData);
		page.ResetChanges();

		if (page.Index == _header.FirstPageWithFreeBlocks && !page.HasFreeBlocks)
		{
			UpdateFirstPageWithFreeBlocks(FindPageWithFreeBlocks(page.Index + 1));
		}
		else if (page.HasFreeBlocks && page.Index < _header.FirstPageWithFreeBlocks)
		{
			UpdateFirstPageWithFreeBlocks(page.Index);
		}
	}

	private int FindPageWithFreeBlocks(int start)
	{
		for (var pageIndex = start; ; pageIndex++)
		{
			if (_cachedPages.TryGetValue(pageIndex, out var cachedPage) && cachedPage.HasFreeBlocks)
			{
				return pageIndex;
			}

			var pageAddress = GetPageAddress(pageIndex);
			if (_dbFileStream.Length <= pageAddress)
			{
				return pageIndex;
			}

			_dbFileStream.Position = pageAddress;
			if (_dbFileStream.ReadByte() != Constants.InvalidBlockIndex)
			{
				return pageIndex;
			}
		}
	}

	private void UpdateFirstPageWithFreeBlocks(int pageIndex)
	{
		_header.FirstPageWithFreeBlocks = pageIndex;
		_dbFileStream.WriteStructure(_headerOffset, ref _header);

		if (!_cachedPages.TryGetValue(pageIndex, out _firstPageWithFreeBlocks!))
		{
			_firstPageWithFreeBlocks = ReadPage(pageIndex);
		}
	}

	private long GetPageAddress(int index) => _firstPageOffset + (index * Constants.PageSize);

	private static void CheckPageIndex(int index)
	{
		if (index >= MaxPageCount)
		{
			throw new ArgumentOutOfRangeException(nameof(index), $"Index must be [0; {MaxPageCount - 1}]");
		}
	}

	public readonly struct PageAccessor : IDisposable
	{
		private readonly PageManager _pageManager;

		public Page Page { get; }

		public PageAccessor(Page page, PageManager pageManager)
		{
			Page = page;
			_pageManager = pageManager;
		}

		public void Dispose()
		{
			if (Page != null)
			{
				_pageManager.CommitPage(Page);
			}
		}
	}

	private struct PagesHeader
	{
		public int FirstPageWithFreeBlocks { get; set; }
	}
}