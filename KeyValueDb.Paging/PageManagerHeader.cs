using System.Runtime.InteropServices;

namespace KeyValueDb.Paging;

[StructLayout(LayoutKind.Sequential)]
internal struct PageManagerHeader
{
	public FreePagesStack FreePagesStack;

	public static PageManagerHeader Initial { get; } = new() { FreePagesStack = FreePagesStack.Initial };
}