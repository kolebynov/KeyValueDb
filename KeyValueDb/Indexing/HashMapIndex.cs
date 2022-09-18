using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Paging;
using KeyValueDb.Records;

namespace KeyValueDb.Indexing;

public sealed class HashMapIndex
{
	public const int MaxPagesPerBucket = 16;
	public const int BucketCount = 128;

	private static readonly RecordAddress HeaderAddress = new(0, 0);

	private readonly RecordManager _recordManager;
	private readonly FileMappedStructure<HashMapIndexHeader> _header;

	public HashMapIndex(RecordManager recordManager, FileStream dbStream, long offset, bool forceInitialize)
	{
		_recordManager = recordManager;
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
		var resultBucketRecord = _recordManager.Get(new RecordAddress(bucketPages[^1], 0));
		ref var resultBucket = ref resultBucketRecord.ReadMutable().AsRef<HashMapBucket>();
		if (resultBucket.RecordAddresses.Length == HashMapBucket.MaxAddressesCount)
		{
			resultBucketRecord.AssignNewDisposableToVariable(_recordManager.Get(new RecordAddress(AddNewPageToBucket(findResult.BucketIndex), 0)));
			resultBucket = ref resultBucketRecord.ReadMutable().AsRef<HashMapBucket>();
		}

		var recordDataToSave = new RecordData(key, value);
		using var newRecord = _recordManager.Create(recordDataToSave.Size);
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

		hashMapRecord = new HashMapRecord(_recordManager.Get(findResult.RecordAddress));
		return true;
	}

	public bool TryRemove(ReadOnlySpan<char> key)
	{
		var findResult = Find(key);
		if (!findResult.IsFound)
		{
			return false;
		}

		using var bucketRecord = _recordManager.Get(new RecordAddress(findResult.BucketPage, 0));
		ref var bucket = ref bucketRecord.ReadMutable().AsRef<HashMapBucket>();
		bucket.RemoveRecordAddress(findResult.IndexInBucket);
		_recordManager.Remove(findResult.RecordAddress);

		return true;
	}

	private FindResult Find(ReadOnlySpan<char> key)
	{
		var bucketIndex = (int)((uint)GetHashCode(key) % BucketCount);
		var bucketPages = _header.ReadOnlyRef.GetBucketPageIndexes(bucketIndex);

		foreach (var bucketPageIndex in bucketPages)
		{
			using var bucketRecord = _recordManager.Get(new RecordAddress(bucketPageIndex, 0));
			ref readonly var bucket = ref bucketRecord.Read().AsRef<HashMapBucket>();
			for (var i = 0; i < bucket.RecordAddresses.Length; i++)
			{
				var recordAddress = bucket.RecordAddresses[i];
				using var record = _recordManager.Get(recordAddress);
				var recordData = RecordData.DeserializeFromSpan(record.Read());
				if (key.Equals(recordData.Key, StringComparison.Ordinal))
				{
					return new FindResult(bucketIndex, recordAddress, i, bucketPageIndex);
				}
			}
		}

		return new FindResult(bucketIndex, RecordAddress.Invalid, -1, PageIndex.Invalid);
	}

	private PageIndex AddNewPageToBucket(int bucketIndex)
	{
		using var headerRef = _header.GetMutableRef();
		var address = _recordManager.CreateAndSaveData(SpanExtensions.AsReadOnlyBytes(in HashMapBucket.Initial));
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

		public readonly RecordAddress RecordAddress;

		public bool IsFound => RecordAddress != RecordAddress.Invalid;

		public FindResult(int bucketIndex, RecordAddress recordAddress, int indexInBucket, PageIndex bucketPage)
		{
			BucketIndex = bucketIndex;
			RecordAddress = recordAddress;
			IndexInBucket = indexInBucket;
			BucketPage = bucketPage;
		}
	}
}