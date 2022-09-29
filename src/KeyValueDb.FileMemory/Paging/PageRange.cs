using System.Runtime.InteropServices;

namespace KeyValueDb.FileMemory.Paging;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = Size)]
internal readonly struct PageRange : IEquatable<PageRange>
{
	public const int Size = 12;

	public readonly PageIndex PageIndex;

	public readonly int PageCount;

	public bool IsInvalid => PageIndex == PageIndex.Invalid;

	public static PageRange Invalid { get; } = new(PageIndex.Invalid, 0);

	public PageRange(PageIndex pageIndex, int pageCount)
	{
		PageIndex = pageIndex;
		PageCount = pageCount;
	}

	public bool IsInRange(PageIndex pageIndex) => pageIndex >= PageIndex && pageIndex < PageIndex + PageCount;

	public bool IsEndOfRange(PageIndex pageIndex) => pageIndex == PageIndex + PageCount;

	public bool Equals(PageRange other)
	{
		return PageIndex.Equals(other.PageIndex) && PageCount == other.PageCount;
	}

	public override bool Equals(object? obj)
	{
		return obj is PageRange other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(PageIndex, PageCount);
	}
}