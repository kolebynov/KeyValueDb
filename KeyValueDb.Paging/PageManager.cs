using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;

namespace KeyValueDb.Paging;

public sealed class PageManager : IDisposable
{
	private readonly FileStream _dbFileStream;
	private readonly long _headerOffset;
	private readonly long _firstPageOffset;
	private readonly Dictionary<PageIndex, Page> _cachedPages = new();
	private PageManagerHeader _header;

	public PageManager(FileStream dbFileStream, long offset)
	{
		_dbFileStream = dbFileStream ?? throw new ArgumentNullException(nameof(dbFileStream));
		_headerOffset = offset;
		_firstPageOffset = offset + Marshal.SizeOf<PageManagerHeader>();

		if (_dbFileStream.Length <= _headerOffset)
		{
			_header = PageManagerHeader.Initial;
			WriteHeader();
		}
		else
		{
			_dbFileStream.ReadStructure(_headerOffset, ref _header);
		}
	}

	public PageAccessor AllocatePage()
	{
		var pageIndex = _header.FreePagesStack.Count > 0
			? _header.FreePagesStack.Pop()
			: _header.LastAllocatedPage == PageIndex.Invalid ? 0 : _header.LastAllocatedPage + 1;

		if (pageIndex > _header.LastAllocatedPage || _header.LastAllocatedPage == PageIndex.Invalid)
		{
			_header.LastAllocatedPage = pageIndex;
			WriteHeader();
		}

		return new PageAccessor(GetPageInternal(pageIndex), this, pageIndex);
	}

	public PageAccessor GetAllocatedPage(PageIndex pageIndex)
	{
		CheckPageIndex(pageIndex);

		return new PageAccessor(GetPageInternal(pageIndex), this, pageIndex);
	}

	public bool TryGetNextAllocatedPage(PageIndex pageIndex, out PageAccessor pageAccessor)
	{
		CheckPageIndex(pageIndex);

		for (var i = pageIndex + 1; i <= _header.LastAllocatedPage; i++)
		{
			if (!_header.FreePagesStack.Contains(i))
			{
				pageAccessor = GetAllocatedPage(i);
				return true;
			}
		}

		pageAccessor = default;
		return false;
	}

	public void FreePage(uint pageIndex)
	{
		CheckPageIndex(pageIndex);

		var pageAddress = GetPageAddress(pageIndex);
		var isLastPage = _dbFileStream.Length == pageAddress + Constants.PageSize;
		if (isLastPage)
		{
			_dbFileStream.SetLength(pageAddress);
			return;
		}

		_header.FreePagesStack.Push(pageIndex);
		WriteHeader();
	}

	public void Dispose()
	{
		_dbFileStream.Dispose();
	}

	internal void CommitPage(Page page, PageIndex pageIndex)
	{
		if (!page.HasChanges)
		{
			return;
		}

		_dbFileStream.Position = GetPageAddress(pageIndex);
		_dbFileStream.Write(page.GetPageData());
		page.HasChanges = false;
	}

	private void CheckPageIndex(PageIndex pageIndex)
	{
		if (pageIndex == PageIndex.Invalid)
		{
			throw new ArgumentException($"Invalid page index {pageIndex}", nameof(pageIndex));
		}

		if (_header.FreePagesStack.Contains(pageIndex) || pageIndex > _header.LastAllocatedPage)
		{
			throw new ArgumentException($"You can't access not allocated page (index: {pageIndex})", nameof(pageIndex));
		}
	}

	private Page GetPageInternal(PageIndex pageIndex)
	{
		if (_cachedPages.TryGetValue(pageIndex, out var page))
		{
			return page;
		}

		return _cachedPages[pageIndex] = ReadPage(pageIndex);
	}

	private Page ReadPage(PageIndex pageIndex)
	{
		var pageAddress = GetPageAddress(pageIndex);

		PageData pageData;
		if (_dbFileStream.Length <= pageAddress)
		{
			pageData = default;
			_dbFileStream.SetLength(pageAddress + Constants.PageSize);
		}
		else
		{
			_dbFileStream.ReadStructure(pageAddress, ref pageData);
		}

		return new Page(ref pageData);
	}

	private void WriteHeader() => _dbFileStream.WriteStructure(_headerOffset, ref _header);

	private long GetPageAddress(PageIndex pageIndex) => _firstPageOffset + (pageIndex.Value * Constants.PageSize);
}