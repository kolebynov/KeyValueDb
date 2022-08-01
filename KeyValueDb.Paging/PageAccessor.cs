﻿namespace KeyValueDb.Paging;

public readonly struct PageAccessor : IDisposable
{
	private readonly PageManager _pageManager;
	private readonly Page _page;
	private readonly uint _pageIndex;

	public ReadOnlySpan<byte> Read(int offset = 0, int length = 0) => _page.Read(offset, length);

	public void Write(ReadOnlySpan<byte> data, int offset = 0) => _page.Write(data, offset);

	internal PageAccessor(Page page, PageManager pageManager, uint pageIndex)
	{
		_page = page;
		_pageManager = pageManager;
		_pageIndex = pageIndex;
	}

	public void Dispose()
	{
		if (_page != null)
		{
			_pageManager.CommitPage(_page, _pageIndex);
		}
	}
}