using System.Runtime.InteropServices;

namespace KeyValueDb.Paging;

[StructLayout(LayoutKind.Sequential)]
internal struct PageManagerHeader
{
	public FreePagesStack FreePagesStack;

	public PageManagerHeader()
	{
		FreePagesStack = new FreePagesStack();
	}
}