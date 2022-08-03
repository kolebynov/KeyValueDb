namespace KeyValueDb.Common;

public ref struct SpanReader<T>
{
	private readonly ReadOnlySpan<T> _span;
	private int _currentIndex;

	public SpanReader(ReadOnlySpan<T> span)
	{
		_span = span;
		_currentIndex = 0;
	}

	public ReadOnlySpan<T> Read(int length)
	{
		if (_currentIndex + length > _span.Length)
		{
			throw new ArgumentException("Length too big", nameof(length));
		}

		var result = _span.Slice(_currentIndex, length);
		_currentIndex += length;

		return result;
	}
}