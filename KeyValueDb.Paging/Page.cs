using KeyValueDb.Common.Extensions;

namespace KeyValueDb.Paging;

internal sealed class Page
{
	private PageData _pageData;

	public bool HasChanges { get; set; }

	public Page(ref PageData pageData)
	{
		_pageData = pageData;
	}

	public Span<byte> GetPageData() => _pageData.AsBytes();

	public ReadOnlySpan<byte> Read(int offset = 0, int length = 0) => GetSlice(offset, length);

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

		return _pageData.AsBytes()[offset..(offset + len)];
	}
}