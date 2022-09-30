using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyValueDb.Common;
using KeyValueDb.Common.Extensions;
using KeyValueDb.FileMemory;

namespace KeyValueDb.Indexing;

public sealed class HashMapIndex
{
	public const int MaxPagesPerBucket = 16;
	public const int BucketCount = 128;

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

		var bucketPages = _header.ReadOnlyRef.GetBucketAddresses(findResult.BucketIndex);
		var resultBucketRecord = _fileMemoryAllocator.Get(bucketPages[^1]);
		ref var resultBucket = ref resultBucketRecord.ValueRefMutable;
		if (resultBucket.RecordAddresses.Length == HashMapBucket.MaxAddressesCount)
		{
			resultBucketRecord.AssignNewDisposableToVariable(AddNewPageToBucket(findResult.BucketIndex));
			resultBucket = ref resultBucketRecord.ValueRefMutable;
		}

		var recordDataToSave = new RecordData(key, value);
		using var newRecord = _fileMemoryAllocator.Allocate(recordDataToSave.Size);
		recordDataToSave.SerializeToSpan(newRecord.DataMutable);
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

		hashMapRecord = new HashMapRecord(_fileMemoryAllocator.Get(findResult.RecordAddress));
		return true;
	}

	public bool TryRemove(ReadOnlySpan<char> key)
	{
		var findResult = Find(key);
		if (!findResult.IsFound)
		{
			return false;
		}

		using var bucketRecord = _fileMemoryAllocator.Get(findResult.BucketAddress);
		ref var bucket = ref bucketRecord.ValueRefMutable;
		bucket.RemoveRecordAddress(findResult.IndexInBucket);
		_fileMemoryAllocator.Remove(findResult.RecordAddress);

		return true;
	}

	private FindResult Find(ReadOnlySpan<char> key)
	{
		var bucketIndex = (int)((uint)GetHashCode(key) % BucketCount);
		var bucketAddresses = _header.ReadOnlyRef.GetBucketAddresses(bucketIndex);

		foreach (var bucketAddress in bucketAddresses)
		{
			using var bucketRecord = _fileMemoryAllocator.Get(bucketAddress);
			ref readonly var bucket = ref bucketRecord.ValueRef;
			for (var i = 0; i < bucket.RecordAddresses.Length; i++)
			{
				var recordAddress = bucket.RecordAddresses[i];
				using var record = _fileMemoryAllocator.Get(recordAddress);
				var recordData = RecordData.DeserializeFromSpan(record.Data);
				if (key.Equals(recordData.Key, StringComparison.Ordinal))
				{
					return new FindResult(bucketIndex, recordAddress, i, bucketAddress);
				}
			}
		}

		return new FindResult(bucketIndex, FileMemoryAddress.Invalid, -1, FileMemoryAddress<HashMapBucket>.Invalid);
	}

	private AllocatedMemory<HashMapBucket> AddNewPageToBucket(int bucketIndex)
	{
		using var headerRef = _header.GetMutableRef();
		var newBucket = _fileMemoryAllocator.AllocateStruct(HashMapBucket.Initial);
		headerRef.Ref.AddBucketAddress(bucketIndex, newBucket.Address);

		return newBucket;
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

		public readonly FileMemoryAddress<HashMapBucket> BucketAddress;

		public readonly int IndexInBucket;

		public readonly FileMemoryAddress RecordAddress;

		public bool IsFound => RecordAddress != FileMemoryAddress.Invalid;

		public FindResult(int bucketIndex, FileMemoryAddress recordAddress, int indexInBucket, FileMemoryAddress<HashMapBucket> bucketAddress)
		{
			BucketIndex = bucketIndex;
			RecordAddress = recordAddress;
			IndexInBucket = indexInBucket;
			BucketAddress = bucketAddress;
		}
	}
}