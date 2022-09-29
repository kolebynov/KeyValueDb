namespace KeyValueDb.Common;

public ref struct SpanWriter<T>
{
	private readonly Span<T> _span;
	private int _currentIndex;

	public SpanWriter(Span<T> span)
	{
		_span = span;
		_currentIndex = 0;
	}

	public void Write(ReadOnlySpan<T> data)
	{
		if (_currentIndex + data.Length > _span.Length)
		{
			throw new ArgumentException("Data length too big", nameof(data));
		}

		data.CopyTo(_span[_currentIndex..]);
		_currentIndex += data.Length;
	}
}