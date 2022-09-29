namespace KeyValueDb.Common;

public interface IFixedListProvider<T>
{
	Span<T> List { get; }
}