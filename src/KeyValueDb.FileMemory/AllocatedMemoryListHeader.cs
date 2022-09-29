using System.Runtime.InteropServices;

namespace KeyValueDb.FileMemory;

[StructLayout(LayoutKind.Sequential, Pack = 2, Size = Size)]
public struct AllocatedMemoryListHeader
{
	public const int Size = 6;

	public ushort NextFreeOffsetIndex;
	public ushort LastFilledOffsetIndex;
	public ushort MaxBlockCount;

	public readonly int CalculatePayloadSize(int dataSize) => dataSize - Size - (MaxBlockCount * 4);

	public static AllocatedMemoryListHeader CreateInitial(ushort maxBlockCount)
	{
		return new AllocatedMemoryListHeader
		{
			NextFreeOffsetIndex = 0,
			LastFilledOffsetIndex = AllocatedMemoryList.InvalidOffsetIndex,
			MaxBlockCount = maxBlockCount,
		};
	}
}