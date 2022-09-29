using System.Runtime.InteropServices;
using KeyValueDb.Common.Exceptions;

namespace KeyValueDb.Common;

[StructLayout(LayoutKind.Sequential)]
public struct FixedList<TListProvider, TItem>
	where TListProvider : struct, IFixedListProvider<TItem>
	where TItem : struct, IEquatable<TItem>
{
	private int _count;

	public TListProvider ListProvider;

	public readonly ReadOnlySpan<TItem> ItemsReadOnly => ListProvider.List[.._count];

	public Span<TItem> Items => ListProvider.List[.._count];

	public FixedList(TListProvider listProvider, TItem initialValue)
	{
		ListProvider = listProvider;
		_count = 0;
		ListProvider.List.Fill(initialValue);
	}

	public void Add(TItem item)
	{
		var list = ListProvider.List;
		if (_count >= list.Length)
		{
			throw new FixedListFullException();
		}

		list[_count++] = item;
	}

	public bool Remove(TItem item)
	{
		var index = FindIndex(item);
		if (index < 0)
		{
			return false;
		}

		RemoveAt(index);
		return true;
	}

	public readonly int FindIndex(TItem item)
	{
		for (var i = 0; i < _count; i++)
		{
			if (item.Equals(ListProvider.List[i]))
			{
				return i;
			}
		}

		return -1;
	}

	public void RemoveAt(int index)
	{
		if (index != _count - 1)
		{
			var list = ListProvider.List;
			list[(index + 1)..].CopyTo(list[index..]);
		}

		_count--;
	}
}