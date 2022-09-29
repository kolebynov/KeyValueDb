namespace KeyValueDb;

public sealed class SimpleReaderWriterLock<TResource>
{
	private readonly TResource _resource;
	private int _acquiringWriteLock;
	private int _acquiredReadLocks;

	public SimpleReaderWriterLock(TResource resource)
	{
		_resource = resource;
	}

	public ReadLock AcquireReadLock()
	{
		while (true)
		{
			while (_acquiringWriteLock == 1)
			{
			}

			Interlocked.Increment(ref _acquiredReadLocks);

			if (_acquiringWriteLock == 1)
			{
				Interlocked.Decrement(ref _acquiredReadLocks);
				continue;
			}

			break;
		}

		return new ReadLock(this);
	}

	public WriteLock AcquireWriteLock()
	{
		while (true)
		{
			while (_acquiringWriteLock == 1)
			{
			}

			if (Interlocked.CompareExchange(ref _acquiredReadLocks, 1, 0) == 1)
			{
				continue;
			}

			while (_acquiredReadLocks > 0)
			{
			}

			break;
		}

		return new WriteLock(this);
	}

	private void ReleaseReadLock() => Interlocked.Decrement(ref _acquiredReadLocks);

	private void ReleaseWriteLock() => _acquiringWriteLock = 0;

	public readonly struct ReadLock : IDisposable
	{
		private readonly SimpleReaderWriterLock<TResource> _lock;

		public TResource Resource => _lock._resource;

		public ReadLock(SimpleReaderWriterLock<TResource> @lock)
		{
			_lock = @lock;
		}

		public void Dispose()
		{
			_lock.ReleaseReadLock();
		}
	}

	public readonly struct WriteLock : IDisposable
	{
		private readonly SimpleReaderWriterLock<TResource> _lock;

		public TResource Resource => _lock._resource;

		public WriteLock(SimpleReaderWriterLock<TResource> @lock)
		{
			_lock = @lock;
		}

		public void Dispose()
		{
			_lock.ReleaseWriteLock();
		}
	}
}