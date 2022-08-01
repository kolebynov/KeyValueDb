using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;

namespace KeyValueDb.Paging;

public sealed class PageManager : IDisposable
{
	private readonly FileStream _dbFileStream;
	private readonly long _headerOffset;
	private readonly long _firstPageOffset;
	private readonly Dictionary<uint, Page> _cachedPages = new();
	private PageManagerHeader _header;

	public PageManager(FileStream dbFileStream, long offset)
	{
		_dbFileStream = dbFileStream ?? throw new ArgumentNullException(nameof(dbFileStream));
		_headerOffset = offset;
		_firstPageOffset = offset + Marshal.SizeOf<PageManagerHeader>();

		if (_dbFileStream.Length <= _headerOffset)
		{
			_header = new PageManagerHeader();
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
			: (uint)((_dbFileStream.Length - _firstPageOffset) / Constants.PageSize);

		return new PageAccessor(GetPageInternal(pageIndex), this, pageIndex);
	}

	public PageAccessor GetPage(uint pageIndex)
	{
		CheckPageIndex(pageIndex);

		return new PageAccessor(GetPageInternal(pageIndex), this, pageIndex);
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

	internal void CommitPage(Page page, uint pageIndex)
	{
		if (!page.HasChanges)
		{
			return;
		}

		_dbFileStream.WriteStructure(GetPageAddress(pageIndex), ref page.GetPageData());
		page.HasChanges = false;
	}

	private void CheckPageIndex(uint pageIndex)
	{
		if (pageIndex == Constants.InvalidPageIndex)
		{
			throw new ArgumentException($"Invalid page index {pageIndex}", nameof(pageIndex));
		}

		if (_header.FreePagesStack.Contains(pageIndex) || _dbFileStream.Length <= GetPageAddress(pageIndex))
		{
			throw new ArgumentException($"You can't access not allocated page (index: {pageIndex})", nameof(pageIndex));
		}
	}

	private Page GetPageInternal(uint pageIndex)
	{
		if (_cachedPages.TryGetValue(pageIndex, out var page))
		{
			return page;
		}

		return _cachedPages[pageIndex] = ReadPage(pageIndex);
	}

	private Page ReadPage(uint pageIndex)
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

	private long GetPageAddress(uint pageIndex) => _firstPageOffset + (pageIndex * Constants.PageSize);
}