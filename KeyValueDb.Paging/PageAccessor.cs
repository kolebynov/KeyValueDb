namespace KeyValueDb.Paging;

public readonly struct PageAccessor : IDisposable
{
	private readonly PageManager _pageManager;
	private readonly Page _page;

	public PageIndex PageIndex { get; }

	public ReadOnlySpan<byte> Read(int offset = 0, int length = 0) => _page.Read(offset, length);

	public Span<byte> ReadMutable(int offset = 0, int length = 0) => _page.ReadMutable(offset, length);

	public void Write(ReadOnlySpan<byte> data, int offset = 0) => _page.Write(data, offset);

	public void Dispose()
	{
		if (_page != null)
		{
			_pageManager.CommitPage(_page, PageIndex);
		}
	}

	internal PageAccessor(Page page, PageManager pageManager, PageIndex pageIndex)
	{
		_page = page;
		_pageManager = pageManager;
		PageIndex = pageIndex;
	}
}