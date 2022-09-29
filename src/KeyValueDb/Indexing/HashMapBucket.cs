using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;
using KeyValueDb.FileMemory;

namespace KeyValueDb.Indexing;

[StructLayout(LayoutKind.Sequential, Size = Size)]
public unsafe struct HashMapBucket
{
	public const int MaxAddressesCount = AddressesSize / FileMemoryAddress.Size;

	public static readonly HashMapBucket Initial = CreateInitial();

	private const int Size = FileMemoryAllocator.MaxRecordSize;
	private const int AddressesSize = Size - 4;

	private int _count;
	private fixed byte _recordAddresses[FileMemoryAddress.Size * MaxAddressesCount];

	public readonly ReadOnlySpan<FileMemoryAddress> RecordAddresses =>
		_count > 0 ? AllRecordAddresses[.._count] : ReadOnlySpan<FileMemoryAddress>.Empty;

	public void AddRecordAddress(FileMemoryAddress fileMemoryAddress)
	{
		if (_count == MaxAddressesCount)
		{
			throw new InvalidOperationException("Bucket is full");
		}

		AllRecordAddressesMutable[_count++] = fileMemoryAddress;
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

	private readonly ReadOnlySpan<FileMemoryAddress> AllRecordAddresses =>
		MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _recordAddresses[0]), FileMemoryAddress.Size * MaxAddressesCount).Cast<byte, FileMemoryAddress>();

	private Span<FileMemoryAddress> AllRecordAddressesMutable =>
		MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _recordAddresses[0]), FileMemoryAddress.Size * MaxAddressesCount).Cast<byte, FileMemoryAddress>();

	private static HashMapBucket CreateInitial()
	{
		var bucket = default(HashMapBucket);
		bucket.AllRecordAddressesMutable.Fill(FileMemoryAddress.Invalid);

		return bucket;
	}
}