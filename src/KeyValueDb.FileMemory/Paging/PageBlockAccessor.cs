namespace KeyValueDb.FileMemory.Paging;

internal readonly struct PageBlockAccessor : IDisposable
{
	private readonly PageManager _pageManager;
	private readonly PageBlock _pageBlock;

	public PageIndex PageIndex => _pageBlock.PageIndex;

	public ReadOnlySpan<byte> Read(int offset = 0, int length = 0) => _pageBlock.Read(offset, length);

	public Span<byte> ReadMutable(int offset = 0, int length = 0) => _pageBlock.ReadMutable(offset, length);

	public void Write(ReadOnlySpan<byte> data, int offset = 0) => _pageBlock.Write(data, offset);

	public void Dispose()
	{
		if (_pageBlock != null)
		{
			_pageManager.CommitPageBlock(_pageBlock);
		}
	}

	internal PageBlockAccessor(PageBlock pageBlock, PageManager pageManager)
	{
		_pageBlock = pageBlock;
		_pageManager = pageManager;
	}
}