using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using KeyValueDb.FileMemory;

namespace KeyValueDb.Indexing;

[StructLayout(LayoutKind.Sequential, Size = Size)]
public unsafe struct HashMapIndexHeader
{
	public const int Size = BucketsPageIndexesSize;

	private const int BucketsPageIndexesSize = FileMemoryAddress.Size * HashMapIndex.MaxPagesPerBucket * HashMapIndex.BucketCount;

	private fixed byte _bucketsPageIndexes[BucketsPageIndexesSize];

	public static HashMapIndexHeader Initial { get; } = CreateInitial();

	public readonly ReadOnlySpan<FileMemoryAddress<HashMapBucket>> GetBucketAddresses(int bucket)
	{
		var indexes = GetBucketAllAddresses(bucket);
		var count = GetPageAddressCount(indexes);

		return count > 0 ? indexes[..count] : ReadOnlySpan<FileMemoryAddress<HashMapBucket>>.Empty;
	}

	public void AddBucketAddress(int bucket, FileMemoryAddress<HashMapBucket> bucketAddress)
	{
		var indexes = GetBucketAllAddresses(bucket);
		indexes[GetPageAddressCount(indexes)] = bucketAddress;
	}

	private readonly Span<FileMemoryAddress<HashMapBucket>> BucketsAddressesMutable =>
		MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _bucketsPageIndexes[0]), BucketsPageIndexesSize).Cast<byte, FileMemoryAddress<HashMapBucket>>();

	private readonly int GetPageAddressCount(ReadOnlySpan<FileMemoryAddress<HashMapBucket>> bucketAddresses)
	{
		var count = 0;
		while (bucketAddresses[count] != FileMemoryAddress<HashMapBucket>.Invalid)
		{
			count++;
		}

		return count;
	}

	private readonly Span<FileMemoryAddress<HashMapBucket>> GetBucketAllAddresses(int bucket) =>
		BucketsAddressesMutable.Slice(bucket * HashMapIndex.MaxPagesPerBucket, HashMapIndex.MaxPagesPerBucket);

	private static HashMapIndexHeader CreateInitial()
	{
		var header = default(HashMapIndexHeader);
		header.BucketsAddressesMutable.Fill(FileMemoryAddress<HashMapBucket>.Invalid);

		return header;
	}
}