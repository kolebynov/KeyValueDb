using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;
using KeyValueDb.FileMemory;
using KeyValueDb.FileMemory.Paging;

namespace KeyValueDb.Indexing;

public sealed class HashMapIndex
{
	public const int MaxPagesPerBucket = 16;
	public const int BucketCount = 128;

	private static readonly FileMemoryAddress HeaderAddress = new(0, 0);

	private readonly FileMemoryAllocator _fileMemoryAllocator;
	private readonly FileMappedStructure<HashMapIndexHeader> _header;

	public HashMapIndex(FileMemoryAllocator fileMemoryAllocator, FileStream dbStream, long offset, bool forceInitialize)
	{
		_fileMemoryAllocator = fileMemoryAllocator;
		_header = new FileMappedStructure<HashMapIndexHeader>(dbStream, offset, HashMapIndexHeader.Initial, forceInitialize);

		if (forceInitialize)
		{
			for (var i = 0; i < BucketCount; i++)
			{
				AddNewPageToBucket(i);
			}
		}
	}

	public bool TryAdd(ReadOnlySpan<char> key, ReadOnlySpan<byte> value)
	{
		var findResult = Find(key);
		if (findResult.IsFound)
		{
			return false;
		}

		var bucketPages = _header.ReadOnlyRef.GetBucketPageIndexes(findResult.BucketIndex);
		var resultBucketRecord = _fileMemoryAllocator.Get(new FileMemoryAddress(bucketPages[^1], 0));
		ref var resultBucket = ref resultBucketRecord.ReadMutable().AsRef<HashMapBucket>();
		if (resultBucket.RecordAddresses.Length == HashMapBucket.MaxAddressesCount)
		{
			resultBucketRecord.AssignNewDisposableToVariable(_fileMemoryAllocator.Get(new FileMemoryAddress(AddNewPageToBucket(findResult.BucketIndex), 0)));
			resultBucket = ref resultBucketRecord.ReadMutable().AsRef<HashMapBucket>();
		}

		var recordDataToSave = new RecordData(key, value);
		using var newRecord = _fileMemoryAllocator.Create(recordDataToSave.Size);
		recordDataToSave.SerializeToSpan(newRecord.ReadMutable());
		resultBucket.AddRecordAddress(newRecord.Address);

		resultBucketRecord.Dispose();

		return true;
	}

	public bool TryGet(ReadOnlySpan<char> key, out HashMapRecord hashMapRecord)
	{
		var findResult = Find(key);
		if (!findResult.IsFound)
		{
			hashMapRecord = default;
			return false;
		}

		hashMapRecord = new HashMapRecord(_fileMemoryAllocator.Get(findResult.FileMemoryAddress));
		return true;
	}

	public bool TryRemove(ReadOnlySpan<char> key)
	{
		var findResult = Find(key);
		if (!findResult.IsFound)
		{
			return false;
		}

		using var bucketRecord = _fileMemoryAllocator.Get(new FileMemoryAddress(findResult.BucketPage, 0));
		ref var bucket = ref bucketRecord.ReadMutable().AsRef<HashMapBucket>();
		bucket.RemoveRecordAddress(findResult.IndexInBucket);
		_fileMemoryAllocator.Remove(findResult.FileMemoryAddress);

		return true;
	}

	private FindResult Find(ReadOnlySpan<char> key)
	{
		var bucketIndex = (int)((uint)GetHashCode(key) % BucketCount);
		var bucketPages = _header.ReadOnlyRef.GetBucketPageIndexes(bucketIndex);

		foreach (var bucketPageIndex in bucketPages)
		{
			using var bucketRecord = _fileMemoryAllocator.Get(new FileMemoryAddress(bucketPageIndex, 0));
			ref readonly var bucket = ref bucketRecord.Read().AsRef<HashMapBucket>();
			for (var i = 0; i < bucket.RecordAddresses.Length; i++)
			{
				var recordAddress = bucket.RecordAddresses[i];
				using var record = _fileMemoryAllocator.Get(recordAddress);
				var recordData = RecordData.DeserializeFromSpan(record.Read());
				if (key.Equals(recordData.Key, StringComparison.Ordinal))
				{
					return new FindResult(bucketIndex, recordAddress, i, bucketPageIndex);
				}
			}
		}

		return new FindResult(bucketIndex, FileMemoryAddress.Invalid, -1, PageIndex.Invalid);
	}

	private PageIndex AddNewPageToBucket(int bucketIndex)
	{
		using var headerRef = _header.GetMutableRef();
		var address = _fileMemoryAllocator.CreateAndSaveData(SpanExtensions.AsReadOnlyBytes(in HashMapBucket.Initial));
		headerRef.Ref.AddPageIndexToBucket(bucketIndex, address.PageIndex);

		return address.PageIndex;
	}

	private static int GetHashCode(ReadOnlySpan<char> key)
	{
		var seed = Marvin.DefaultSeed;
		return Marvin.ComputeHash32(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(key)), (uint)key.Length * 2, (uint)seed,
			(uint)(seed >> 32));
	}

	private readonly struct FindResult
	{
		public readonly int BucketIndex;

		public readonly PageIndex BucketPage;

		public readonly int IndexInBucket;

		public readonly FileMemoryAddress FileMemoryAddress;

		public bool IsFound => FileMemoryAddress != FileMemoryAddress.Invalid;

		public FindResult(int bucketIndex, FileMemoryAddress fileMemoryAddress, int indexInBucket, PageIndex bucketPage)
		{
			BucketIndex = bucketIndex;
			FileMemoryAddress = fileMemoryAddress;
			IndexInBucket = indexInBucket;
			BucketPage = bucketPage;
		}
	}
}