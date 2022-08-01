using System.Runtime.InteropServices;

namespace KeyValueDb;

[StructLayout(LayoutKind.Sequential, Pack = 2, Size = Size)]
public unsafe struct RecordsPageHeader
{
	public const int Size = 128;

	private const int MaxRecordCount = 62;

	public ushort NextFreeOffsetIndex;
	public ushort LastFilledOffsetIndex;
#pragma warning disable CS0649
	private fixed ushort _recordEndOffsets[MaxRecordCount];
#pragma warning restore CS0649

	public Span<ushort> RecordEndOffsets => MemoryMarshal.CreateSpan(ref _recordEndOffsets[0], MaxRecordCount);

	public RecordsPageHeader()
	{
		NextFreeOffsetIndex = 0;
		LastFilledOffsetIndex = RecordsPage.InvalidOffsetIndex;
		RecordEndOffsets.Fill(RecordsPage.InvalidRecordEndOffset);
	}
}