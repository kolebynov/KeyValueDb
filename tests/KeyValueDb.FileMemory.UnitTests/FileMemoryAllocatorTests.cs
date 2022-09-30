namespace KeyValueDb.FileMemory.UnitTests;

[TestClass]
public class FileMemoryAllocatorTests
{
	[TestMethod]
	public void TestMethod()
	{
		using var dbStream = new FileStream("test_allocator.db", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 0, FileOptions.RandomAccess);
		var allocator = new FileMemoryAllocatorFactory().Create(dbStream, 0, dbStream.Length == 0);

		using var alloc1 = allocator.Allocate(20);
		using var alloc2 = allocator.Allocate(200);
		using var alloc3 = allocator.Allocate(2000);
		using var alloc4 = allocator.AllocateStruct<long>(1234);
		Console.WriteLine(alloc4.ValueRefMutable);
	}
}