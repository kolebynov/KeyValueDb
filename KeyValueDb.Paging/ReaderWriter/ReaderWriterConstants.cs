namespace KeyValueDb.Paging.ReaderWriter;

internal static class ReaderWriterConstants
{
	internal const int BlockDataSize = Constants.BlockSize - BlockAddress.Size;
}