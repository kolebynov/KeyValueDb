namespace KeyValueDb.Paging;

public sealed class Page
{
#pragma warning disable SA1401
	internal PageData PageData;
#pragma warning restore SA1401

	public int Index { get; }

	public bool HasFreeBlocks => PageData.HasFreeBlocks;

	public byte FirstFreeBlock => PageData.FirstFreeBlock;

	public ReadOnlySpan<byte> GetBlockData(byte index, int offset = 0, int length = 0) =>
		PageData.GetBlockData(index, offset, length);

	public void SetBlockData(byte index, ReadOnlySpan<byte> data, int offset = 0) =>
		HasChanges = PageData.SetBlockData(index, data, offset) || HasChanges;

	public void FreeBlock(byte index) => HasChanges = PageData.FreeBlock(index) || HasChanges;

	internal bool HasChanges { get; private set; }

	internal Page(ref PageData pageData, int index)
	{
		PageData = pageData;
		Index = index;
	}

	internal void ResetChanges()
	{
		HasChanges = false;
	}
}