using System.Runtime.InteropServices;
using KeyValueDb.Paging.Exceptions;

namespace KeyValueDb.Paging;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct FreePagesStack
{
	private const int MaxFreePagesCount = 1023;

	private int _head;

#pragma warning disable CS0649
	private fixed uint _freePagesList[MaxFreePagesCount];
#pragma warning restore CS0649

	public int Count => _head;

	public static FreePagesStack Initial { get; } = CreateInitial();

	public void Push(uint pageIndex)
	{
		var list = List;
		if (_head >= list.Length)
		{
			// TODO: Handle it in the calling code
			throw new FreePagesStackFullException();
		}

		list[_head++] = pageIndex;
	}

	public uint Pop()
	{
		if (Count == 0)
		{
			throw new InvalidOperationException("Free pages stack is empty");
		}

		return List[--_head];
	}

	public bool Contains(uint pageIndex) =>
		_head > 0 && List[.._head].Contains(pageIndex);

	private Span<uint> List => MemoryMarshal.CreateSpan(ref _freePagesList[0], MaxFreePagesCount);

	private static FreePagesStack CreateInitial()
	{
		var value = new FreePagesStack { _head = 0 };
		value.List.Fill(Constants.InvalidPageIndex);

		return value;
	}
}