using System.Runtime.InteropServices;
using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;

namespace KeyValueDb.Paging;

public sealed class PageManager
{
	private readonly FileStream _dbFileStream;
	private readonly long _firstPageOffset;
	private readonly Dictionary<PageIndex, Page> _cachedPages = new();
	private FileMappedStructure<PageManagerHeader> _header;

	public PageManager(FileStream dbFileStream, long offset, bool forceInitialize)
	{
		_dbFileStream = dbFileStream ?? throw new ArgumentNullException(nameof(dbFileStream));
		_firstPageOffset = offset + Marshal.SizeOf<PageManagerHeader>();
		_header = new FileMappedStructure<PageManagerHeader>(dbFileStream, offset, PageManagerHeader.Initial, forceInitialize);
	}

	public PageAccessor AllocatePage()
	{
		var pageIndex = _header.ReadOnlyRef.FreePagesStack.Count > 0
			? _header.ReadOnlyRef.FreePagesStack.Pop()
			: _header.ReadOnlyRef.LastAllocatedPage.IsInvalid ? 0 : _header.ReadOnlyRef.LastAllocatedPage + 1;

		if (pageIndex > _header.ReadOnlyRef.LastAllocatedPage || _header.ReadOnlyRef.LastAllocatedPage.IsInvalid)
		{
			using var headerMutableRef = _header.GetMutableRef();
			headerMutableRef.Ref.LastAllocatedPage = pageIndex;
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

		for (var i = pageIndex + 1; i <= _header.ReadOnlyRef.LastAllocatedPage; i++)
		{
			if (!_header.ReadOnlyRef.FreePagesStack.Contains(i))
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

		using var headerMutRef = _header.GetMutableRef();
		headerMutRef.Ref.FreePagesStack.Push(pageIndex);
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
		if (pageIndex.IsInvalid)
		{
			throw new ArgumentException($"Invalid page index {pageIndex}", nameof(pageIndex));
		}

		if (_header.ReadOnlyRef.FreePagesStack.Contains(pageIndex) || pageIndex > _header.ReadOnlyRef.LastAllocatedPage)
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

	private long GetPageAddress(PageIndex pageIndex) => _firstPageOffset + (pageIndex.Value * Constants.PageSize);
}