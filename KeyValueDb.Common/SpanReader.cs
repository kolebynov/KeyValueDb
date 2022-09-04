﻿namespace KeyValueDb.Common;

public ref struct SpanReader<T>
{
	private readonly Span<T> _span;
	private int _currentIndex;

	public SpanReader(Span<T> span)
	{
		_span = span;
		_currentIndex = 0;
	}

	public Span<T> Read(int length)
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