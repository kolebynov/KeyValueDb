using KeyValueDb.Common;

namespace KeyValueDb.Paging.ReaderWriter;

internal readonly struct PageList : IDisposable
{
	private readonly ObjectPool<List<PageManager.PageAccessor>>.PoolItem _pagesPoolItem;

	public int Count => Pages.Count;

	public PageList()
	{
		_pagesPoolItem = ListPool<PageManager.PageAccessor>.Instance.Get();
	}

	public void Add(PageManager.PageAccessor pageAccessor)
	{
		if (FindIndexInList(pageAccessor.Page.Index) > -1)
		{
			throw new ArgumentException($"The page with the index {pageAccessor.Page.Index} is already in list", nameof(pageAccessor));
		}

		Pages.Add(pageAccessor);
	}

	public PageManager.PageAccessor GetByIndex(int pageIndex) =>
		TryGetByIndex(pageIndex, out var result)
			? result
			: throw new ArgumentException($"A page with index {pageIndex} not found", nameof(pageIndex));

	public bool TryGetByIndex(int pageIndex, out PageManager.PageAccessor pageAccessor)
	{
		var index = FindIndexInList(pageIndex);
		if (index < 0)
		{
			pageAccessor = default;
			return false;
		}

		pageAccessor = Pages[index];
		return true;
	}

	public void Dispose()
	{
		foreach (var pageAccessor in Pages)
		{
			pageAccessor.Dispose();
		}

		_pagesPoolItem.Dispose();
	}

	private List<PageManager.PageAccessor> Pages => _pagesPoolItem.Instance;

	private int FindIndexInList(int pageIndex)
	{
		for (var i = 0; i < Pages.Count; i++)
		{
			if (Pages[i].Page.Index == pageIndex)
			{
				return i;
			}
		}

		return -1;
	}
}