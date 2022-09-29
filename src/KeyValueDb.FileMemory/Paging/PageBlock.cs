namespace KeyValueDb.FileMemory.Paging;

internal sealed class PageBlock
{
	private readonly byte[] _pageBlockData;

	public ReadOnlySpan<byte> Data => _pageBlockData;

	public PageIndex PageIndex { get; }

	public int PageCount { get; }

	public bool HasChanges { get; set; }

	public PageBlock(PageIndex pageIndex, byte[] pageBlockData)
	{
		_pageBlockData = pageBlockData ?? throw new ArgumentNullException(nameof(pageBlockData));
		PageIndex = pageIndex;
		if (_pageBlockData.Length % Constants.PageSize != 0)
		{
			throw new ArgumentException($"Page block data must be aligned by {Constants.PageSize} bytes", nameof(pageBlockData));
		}

		PageCount = _pageBlockData.Length / Constants.PageSize;
	}

	public ReadOnlySpan<byte> Read(int offset = 0, int length = 0) => GetSlice(offset, length);

	public Span<byte> ReadMutable(int offset = 0, int length = 0)
	{
		HasChanges = true;
		return GetSlice(offset, length);
	}

	public void Write(ReadOnlySpan<byte> data, int offset = 0)
	{
		var slice = GetSlice(offset, data.Length);
		if (data.SequenceEqual(slice))
		{
			return;
		}

		data.CopyTo(slice);

		HasChanges = true;
	}

	private Span<byte> GetSlice(int offset, int length)
	{
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(offset));
		}

		if (length < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(length));
		}

		var len = length > 0 ? length : Constants.PageSize - offset;

		if (len + offset > Constants.PageSize)
		{
			throw new ArgumentException(string.Empty, nameof(offset));
		}

		return _pageBlockData.AsSpan()[offset..(offset + len)];
	}
}