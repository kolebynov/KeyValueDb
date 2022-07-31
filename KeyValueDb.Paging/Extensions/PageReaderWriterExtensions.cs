using KeyValueDb.Common.Extensions;
using KeyValueDb.Paging.ReaderWriter;

namespace KeyValueDb.Paging.Extensions;

public static class PageReaderWriterExtensions
{
	public static void ReadStructure<T>(this ref PageReader reader, out T structure)
		where T : unmanaged
	{
		structure = default;
		reader.Read(structure.AsBytes());
	}

	public static void WriteStructure<T>(this ref PageWriter pageWriter, ref T structure)
		where T : unmanaged
	{
		pageWriter.Write(structure.AsReadOnlyBytes());
	}

	public static void WriteStructure<T>(this ref PageWriter pageWriter, T structure)
		where T : unmanaged
	{
		pageWriter.Write(structure.AsReadOnlyBytes());
	}

	public static void WriteStructure<T>(this ref PageRewriter pageRewriter, ref T structure)
		where T : unmanaged
	{
		pageRewriter.Write(structure.AsReadOnlyBytes());
	}

	public static void WriteStructure<T>(this ref PageRewriter pageRewriter, T structure)
		where T : unmanaged
	{
		pageRewriter.Write(structure.AsReadOnlyBytes());
	}

	internal static void SetNextBlockAddress(this Page page, byte blockIndex, BlockAddress nextBlockAddress) =>
		page.SetBlockData(blockIndex, nextBlockAddress.AsReadOnlyBytes(), ReaderWriterConstants.BlockDataSize);

	internal static BlockAddress GetNextBlockAddress(this Page page, byte blockIndex) =>
		page.GetBlockData(blockIndex, ReaderWriterConstants.BlockDataSize).AsRef<BlockAddress>();
}