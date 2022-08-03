using System.Runtime.InteropServices;

namespace KeyValueDb.Paging;

[StructLayout(LayoutKind.Sequential)]
internal struct PageManagerHeader
{
	public FreePagesStack FreePagesStack;

	public PageIndex LastAllocatedPage;

	public static PageManagerHeader Initial { get; } = new()
	{
		FreePagesStack = FreePagesStack.Initial,
		LastAllocatedPage = PageIndex.Invalid,
	};
}