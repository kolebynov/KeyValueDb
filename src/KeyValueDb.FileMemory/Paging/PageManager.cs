namespace KeyValueDb.FileMemory.Paging;

internal sealed class PageManager
{
	private readonly FileStream _dbFileStream;
	private readonly long _firstPageOffset;
	private readonly Dictionary<PageIndex, PageBlock> _cachedPages = new();
	private readonly FreePageRangeList _freePageRangeList;
	private readonly AllocatedPageRangeList _allocatedPageRangeList;

	public PageManager(FileStream dbFileStream, long offset, bool forceInitialize)
	{
		_dbFileStream = dbFileStream ?? throw new ArgumentNullException(nameof(dbFileStream));
		_allocatedPageRangeList = new AllocatedPageRangeList(dbFileStream, offset, forceInitialize);
		var freePagesOffset = offset + _allocatedPageRangeList.FileDataSize;
		_freePageRangeList = new FreePageRangeList(dbFileStream, freePagesOffset, forceInitialize);
		_firstPageOffset = freePagesOffset + _freePageRangeList.FileDataSize;
	}

	public PageBlockAccessor AllocatePageBlock(ushort pageCount)
	{
		var needAddToAllocatedRanges = pageCount > 1;

		PageRange allocatedPageRange;
		if (_freePageRangeList.TryRemoveFreePageBlock(pageCount, out var pageIndex))
		{
			allocatedPageRange = new PageRange(pageIndex, pageCount);
		}
		else
		{
			var lastAllocatedPageRange = _allocatedPageRangeList.LastAllocatedPageRange;
			allocatedPageRange = lastAllocatedPageRange.IsInvalid
				? new PageRange(0, pageCount)
				: new PageRange(lastAllocatedPageRange.PageIndex + lastAllocatedPageRange.PageCount, pageCount);
			needAddToAllocatedRanges = true;
		}

		if (needAddToAllocatedRanges)
		{
			_allocatedPageRangeList.Add(allocatedPageRange);
		}

		return new PageBlockAccessor(GetPageBlockInternal(allocatedPageRange), this);
	}

	public PageBlockAccessor GetAllocatedPageBlock(PageIndex pageIndex)
	{
		CheckPageIndex(pageIndex);

		if (_cachedPages.TryGetValue(pageIndex, out var page))
		{
			return new PageBlockAccessor(page, this);
		}

		var pageRange = _allocatedPageRangeList.TryGetByPageIndex(pageIndex, out var p) ? p : new PageRange(pageIndex, 1);

		return new PageBlockAccessor(GetPageBlockInternal(pageRange), this);
	}

	public bool TryGetNextAllocatedPageBlock(PageIndex pageIndex, out PageBlockAccessor pageBlockAccessor)
	{
		CheckPageIndex(pageIndex);

		var foundAllocatedPageRange = FindAllocatedPageRange(pageIndex + 1, _allocatedPageRangeList.LastAllocatedPageRange.PageIndex);
		if (!foundAllocatedPageRange.Equals(PageRange.Invalid))
		{
			pageBlockAccessor = new PageBlockAccessor(GetPageBlockInternal(foundAllocatedPageRange), this);
			return true;
		}

		pageBlockAccessor = default;
		return false;
	}

	public void FreePageBlock(PageIndex pageIndex)
	{
		CheckPageIndex(pageIndex);

		if (pageIndex == _allocatedPageRangeList.LastAllocatedPageRange.PageIndex)
		{
			_allocatedPageRangeList.Remove(_allocatedPageRangeList.LastAllocatedPageRange);
			var trimPageIndex = pageIndex;
			if (_freePageRangeList.TryRemoveByPageIndex(pageIndex - 1, out var freePageBlock))
			{
				trimPageIndex = freePageBlock.PageIndex;
			}

			if (!_allocatedPageRangeList.LastAllocatedPageRange.IsEndOfRange(trimPageIndex))
			{
				_allocatedPageRangeList.Add(new PageRange(trimPageIndex - 1, 1));
			}

			_dbFileStream.SetLength(GetPageFilePosition(trimPageIndex));
			return;
		}

		var pageCount = _allocatedPageRangeList.TryRemoveByPageIndex(pageIndex, out var pageRange) ? pageRange.PageCount : 1;
		_freePageRangeList.Add(new PageRange(pageIndex, pageCount));
	}

	public void CommitPageBlock(PageBlock pageBlock)
	{
		if (!pageBlock.HasChanges)
		{
			return;
		}

		_dbFileStream.Position = GetPageFilePosition(pageBlock.PageIndex);
		_dbFileStream.Write(pageBlock.Data);
		pageBlock.HasChanges = false;
	}

	private PageRange FindAllocatedPageRange(PageIndex startIndex, PageIndex endIndex)
	{
		var forward = endIndex > startIndex;
		for (var i = startIndex; i <= endIndex;)
		{
			if (_freePageRangeList.TryGetByPageIndex(i, out var pageRange))
			{
				i = forward ? pageRange.PageIndex + pageRange.PageCount : pageRange.PageIndex - 1;
			}
			else if (_allocatedPageRangeList.TryGetByPageIndex(i, out pageRange))
			{
				return pageRange;
			}
			else
			{
				return new PageRange(i, 1);
			}
		}

		return PageRange.Invalid;
	}

	private void CheckPageIndex(PageIndex pageIndex)
	{
		if (pageIndex.IsInvalid)
		{
			throw new ArgumentException($"Invalid page index {pageIndex}", nameof(pageIndex));
		}

		if (_freePageRangeList.IsFreePageBlock(pageIndex) || pageIndex > _allocatedPageRangeList.LastAllocatedPageRange.PageIndex)
		{
			throw new ArgumentException($"You can't access not allocated page (index: {pageIndex})", nameof(pageIndex));
		}
	}

	private PageBlock GetPageBlockInternal(PageRange pageRange)
	{
		if (_cachedPages.TryGetValue(pageRange.PageIndex, out var page))
		{
			return page;
		}

		return _cachedPages[pageRange.PageIndex] = ReadPageBlock(pageRange);
	}

	private PageBlock ReadPageBlock(PageRange pageRange)
	{
		var pageFilePosition = GetPageFilePosition(pageRange.PageIndex);

		var pageBlockData = new byte[Constants.PageSize * pageRange.PageCount];
		if (_dbFileStream.Length <= pageFilePosition)
		{
			_dbFileStream.SetLength(pageFilePosition + pageBlockData.Length);
		}
		else
		{
			_dbFileStream.Position = pageFilePosition;
			_dbFileStream.ReadExactly(pageBlockData);
		}

		return new PageBlock(pageRange.PageIndex, pageBlockData);
	}

	private long GetPageFilePosition(PageIndex pageIndex) => _firstPageOffset + ((long)pageIndex.Value * Constants.PageSize);
}