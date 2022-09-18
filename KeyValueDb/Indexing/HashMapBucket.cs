using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Records;

namespace KeyValueDb.Indexing;

[StructLayout(LayoutKind.Sequential, Size = Size)]
public unsafe struct HashMapBucket
{
	public const int MaxAddressesCount = AddressesSize / RecordAddress.Size;

	public static readonly HashMapBucket Initial = CreateInitial();

	private const int Size = RecordManager.MaxRecordSize;
	private const int AddressesSize = Size - 4;

	private int _count;
	private fixed byte _recordAddresses[RecordAddress.Size * MaxAddressesCount];

	public readonly ReadOnlySpan<RecordAddress> RecordAddresses =>
		_count > 0 ? AllRecordAddresses[.._count] : ReadOnlySpan<RecordAddress>.Empty;

	public void AddRecordAddress(RecordAddress recordAddress)
	{
		if (_count == MaxAddressesCount)
		{
			throw new InvalidOperationException("Bucket is full");
		}

		AllRecordAddressesMutable[_count++] = recordAddress;
	}

	public void RemoveRecordAddress(int index)
	{
		if (index >= _count)
		{
			throw new ArgumentException("Invalid index", nameof(index));
		}

		if (index != _count - 1)
		{
			AllRecordAddressesMutable[(index + 1)..].CopyTo(AllRecordAddressesMutable);
		}

		_count--;
	}

	private readonly ReadOnlySpan<RecordAddress> AllRecordAddresses =>
		MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _recordAddresses[0]), RecordAddress.Size * MaxAddressesCount).Cast<byte, RecordAddress>();

	private Span<RecordAddress> AllRecordAddressesMutable =>
		MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _recordAddresses[0]), RecordAddress.Size * MaxAddressesCount).Cast<byte, RecordAddress>();

	private static HashMapBucket CreateInitial()
	{
		var bucket = default(HashMapBucket);
		bucket.AllRecordAddressesMutable.Fill(RecordAddress.Invalid);

		return bucket;
	}
}