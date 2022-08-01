namespace KeyValueDb.Paging;

public readonly struct PageAccessor : IDisposable
{
	private readonly PageManager _pageManager;
	private readonly Page _page;

	public uint PageIndex { get; }

	public ReadOnlySpan<byte> Read(int offset = 0, int length = 0) => _page.Read(offset, length);

	public void Write(ReadOnlySpan<byte> data, int offset = 0) => _page.Write(data, offset);

	public PageDataAccessor GetRawPageData() => new(_page);

	internal PageAccessor(Page page, PageManager pageManager, uint pageIndex)
	{
		_page = page;
		_pageManager = pageManager;
		PageIndex = pageIndex;
	}

	public void Dispose()
	{
		if (_page != null)
		{
			_pageManager.CommitPage(_page, PageIndex);
		}
	}

	public readonly struct PageDataAccessor : IDisposable
	{
		private readonly Page _page;

		public Span<byte> PageData => _page.GetPageData();

		public void Dispose()
		{
			if (_page != null)
			{
				_page.HasChanges = true;
			}
		}

		internal PageDataAccessor(Page page)
		{
			_page = page;
		}
	}
}