using System.Runtime.InteropServices;

namespace KeyValueDb.Records;

[StructLayout(LayoutKind.Sequential, Pack = 2, Size = Size)]
public unsafe struct RecordsPageHeader
{
	public const int Size = 128;

	private const int MaxRecordCount = 62;

	public ushort NextFreeOffsetIndex;
	public ushort LastFilledOffsetIndex;
	private fixed ushort _recordEndOffsets[MaxRecordCount];

	public Span<ushort> RecordEndOffsets => MemoryMarshal.CreateSpan(ref _recordEndOffsets[0], MaxRecordCount);

	public static RecordsPageHeader Initial { get; } = CreateInitial();

	private static RecordsPageHeader CreateInitial()
	{
		var value = new RecordsPageHeader
		{
			NextFreeOffsetIndex = 0,
			LastFilledOffsetIndex = RecordsPage.InvalidOffsetIndex,
		};

		value.RecordEndOffsets.Fill(RecordsPage.InvalidRecordEndOffset);

		return value;
	}
}