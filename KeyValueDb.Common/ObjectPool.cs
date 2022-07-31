namespace KeyValueDb.Common;

public class ObjectPool<T>
	where T : class
{
	private readonly ThreadLocal<Queue<T>> _objectsQueue = new(() => new Queue<T>());
	private readonly Func<T> _factory;
	private readonly Action<T>? _resetAction;

	public ObjectPool(Func<T> factory, Action<T>? resetAction)
	{
		_factory = factory ?? throw new ArgumentNullException(nameof(factory));
		_resetAction = resetAction;
	}

	public PoolItem Get()
	{
		var queue = _objectsQueue.Value!;
		if (queue.TryDequeue(out var instance))
		{
			_resetAction?.Invoke(instance);
			return new PoolItem(instance, this);
		}

		return new PoolItem(_factory(), this);
	}

	private void Return(T instance) => _objectsQueue.Value!.Enqueue(instance);

	public readonly struct PoolItem : IDisposable
	{
		private readonly ObjectPool<T> _pool;

		public T Instance { get; }

		public PoolItem(T instance, ObjectPool<T> pool)
		{
			Instance = instance;
			_pool = pool;
		}

		public void Dispose()
		{
			if (Instance != null)
			{
				_pool.Return(Instance);
			}
		}
	}
}