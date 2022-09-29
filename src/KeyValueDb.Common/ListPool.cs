namespace KeyValueDb.Common;

public static class ListPool<T>
{
	public static ObjectPool<List<T>> Instance { get; } =
		new(() => new List<T>(), x => x.Clear());
}