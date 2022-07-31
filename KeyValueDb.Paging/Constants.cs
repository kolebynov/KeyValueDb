namespace KeyValueDb.Paging;

public static class Constants
{
	public const int BlockSize = 64;

	public const int PageSize = 4096;

	public const byte PageBlockCount = (PageSize / BlockSize) - 1;

	public const byte InvalidBlockIndex = PageBlockCount;

	public const int InvalidPageIndex = -1;

	internal const int PagePayloadSize = BlockSize * PageBlockCount;
}