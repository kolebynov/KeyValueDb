using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Paging;

namespace KeyValueDb.Indexing;

[StructLayout(LayoutKind.Sequential, Size = Size)]
public unsafe struct HashMapIndexHeader
{
	public const int Size = BucketsPageIndexesSize;

	private const int BucketsPageIndexesSize = PageIndex.Size * HashMapIndex.MaxPagesPerBucket * HashMapIndex.BucketCount;

	private fixed byte _bucketsPageIndexes[BucketsPageIndexesSize];

	public static HashMapIndexHeader Initial { get; } = CreateInitial();

	public readonly ReadOnlySpan<PageIndex> GetBucketPageIndexes(int bucket)
	{
		var indexes = GetBucketAllPageIndexes(bucket);
		var count = GetPageIndexCount(indexes);

		return count > 0 ? indexes[..count] : ReadOnlySpan<PageIndex>.Empty;
	}

	public void AddPageIndexToBucket(int bucket, PageIndex pageIndex)
	{
		var indexes = GetBucketAllPageIndexes(bucket);
		indexes[GetPageIndexCount(indexes)] = pageIndex;
	}

	private readonly Span<PageIndex> BucketsPageIndexesMutable =>
		MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _bucketsPageIndexes[0]), BucketsPageIndexesSize).Cast<byte, PageIndex>();

	private readonly int GetPageIndexCount(ReadOnlySpan<PageIndex> bucketIndexes)
	{
		var count = 0;
		while (bucketIndexes[count] != PageIndex.Invalid)
		{
			count++;
		}

		return count;
	}

	private readonly Span<PageIndex> GetBucketAllPageIndexes(int bucket) =>
		BucketsPageIndexesMutable.Slice(bucket * HashMapIndex.MaxPagesPerBucket, HashMapIndex.MaxPagesPerBucket);

	private static HashMapIndexHeader CreateInitial()
	{
		var header = default(HashMapIndexHeader);
		header.BucketsPageIndexesMutable.Fill(PageIndex.Invalid);

		return header;
	}
}