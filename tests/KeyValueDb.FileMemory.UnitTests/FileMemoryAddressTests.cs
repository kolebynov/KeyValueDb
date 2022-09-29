using FluentAssertions;

namespace KeyValueDb.FileMemory.UnitTests;

[TestClass]
public class FileMemoryAddressTests
{
	[DataTestMethod]
	[DataRow(1UL, (ushort)1)]
	[DataRow((ulong)uint.MaxValue, (ushort)(ushort.MaxValue - 1))]
	[DataRow((ulong)((1L << 48) - 1), ushort.MaxValue)]
	public void FileMemoryAddress_ForAnyInputPageIndexAndBlockIndex_ReturnCorrectPageIndexAndBlockIndex(ulong pageIndex, ushort blockIndex)
	{
		// Arrange

		var target = new FileMemoryAddress(pageIndex, blockIndex);

		// Assert

		target.PageIndex.Value.Should().Be(pageIndex);
		target.BlockIndex.Should().Be(blockIndex);
	}

	[DataTestMethod]
	[DataRow((((1UL << 48) - 1) << 16) - 2, false)]
	[DataRow((((1UL << 48) - 1) << 16) - 1, true)]
	[DataRow(((1UL << 48) - 1) << 16, true)]
	[DataRow(ulong.MaxValue, true)]
	public void IsInvalid_ForUlongValue_ReturnCorrectResult(ulong value, bool isInvalid)
	{
		// Arrange

		var target = new FileMemoryAddress(value);

		// Assert

		target.IsInvalid.Should().Be(isInvalid);
	}

	[DataTestMethod]
	[DataRow(1UL, (ushort)1, false)]
	[DataRow((1UL << 48) - 2, (ushort)(ushort.MaxValue - 1), false)]
	[DataRow((1UL << 48) - 2, ushort.MaxValue, true)]
	[DataRow((1UL << 48) - 1, (ushort)0, true)]
	[DataRow((1UL << 48) - 1, ushort.MaxValue, true)]
	public void IsInvalid_ForPageIndexAndBlockIndex_ReturnCorrectResult(ulong pageIndex, ushort blockIndex, bool isInvalid)
	{
		// Arrange

		var target = new FileMemoryAddress(pageIndex, blockIndex);

		// Assert

		target.IsInvalid.Should().Be(isInvalid);
	}
}