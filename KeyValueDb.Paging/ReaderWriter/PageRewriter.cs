using KeyValueDb.Common;
using KeyValueDb.Paging.Extensions;

namespace KeyValueDb.Paging.ReaderWriter;

public struct PageRewriter
{
	private readonly PageManager _pageManager;
	private readonly PageList _pageList = new();
	private int _blockOffset = 0;
	private BlockAddress _currentBlockAddress;

	public PageRewriter(PageManager pageManager, BlockAddress startAddress)
	{
		_pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));
		_pageList.Add(pageManager.GetPage(startAddress.PageIndex));
		_currentBlockAddress = startAddress;
	}

	public void Write(ReadOnlySpan<byte> data)
	{
		while (true)
		{
			var remainingBlockSpace = ReaderWriterConstants.BlockDataSize - _blockOffset;
			if (data.Length <= remainingBlockSpace)
			{
				CopyToBlock(data);
				return;
			}

			CopyToBlock(data[..remainingBlockSpace]);
			GoToNextBlock();

			data = data[remainingBlockSpace..];
		}
	}

	public void Commit()
	{
		if (_pageManager == null)
		{
			return;
		}

		var page = GetPageByIndex(_currentBlockAddress.PageIndex);
		var nextBlockAddress = page.Page.GetNextBlockAddress(_currentBlockAddress.BlockIndex);
		if (nextBlockAddress != BlockAddress.Invalid)
		{
			using var blocksToFreePoolItem = ListPool<BlockAddress>.Instance.Get();
			var blocksToFree = blocksToFreePoolItem.Instance;
			while (nextBlockAddress != BlockAddress.Invalid)
			{
				blocksToFree.Add(nextBlockAddress);
				nextBlockAddress = GetPageByIndex(nextBlockAddress.PageIndex).Page
					.GetNextBlockAddress(nextBlockAddress.BlockIndex);
			}

			foreach (var blockAddress in blocksToFree)
			{
				GetPageByIndex(blockAddress.PageIndex).Page.FreeBlock(blockAddress.BlockIndex);
			}

			page.Page.SetNextBlockAddress(_currentBlockAddress.BlockIndex, BlockAddress.Invalid);
		}

		_pageList.Dispose();
	}

	private void GoToNextBlock()
	{
		_blockOffset = 0;

		var currentPage = GetPageByIndex(_currentBlockAddress.PageIndex);
		var nextBlockAddress = currentPage.Page.GetNextBlockAddress(_currentBlockAddress.BlockIndex);
		if (nextBlockAddress != BlockAddress.Invalid)
		{
			_currentBlockAddress = nextBlockAddress;
			return;
		}

		if (!currentPage.Page.HasFreeBlocks)
		{
			currentPage = _pageManager.GetPageWithFreeBlocks(currentPage.Page.Index + 1);
			_pageList.Add(currentPage);
		}

		var prevBlockAddress = _currentBlockAddress;
		_currentBlockAddress = new BlockAddress(currentPage.Page.Index, currentPage.Page.FirstFreeBlock);

		currentPage.Page.SetNextBlockAddress(_currentBlockAddress.BlockIndex, BlockAddress.Invalid);
		if (prevBlockAddress == BlockAddress.Invalid)
		{
			return;
		}

		var pageIndex = prevBlockAddress.PageIndex;
		var prevBlockPage = pageIndex == currentPage.Page.Index ? currentPage.Page : GetPageByIndex(pageIndex).Page;
		prevBlockPage.SetNextBlockAddress(prevBlockAddress.BlockIndex, _currentBlockAddress);
	}

	private void CopyToBlock(ReadOnlySpan<byte> data)
	{
		GetPageByIndex(_currentBlockAddress.PageIndex).Page.SetBlockData(_currentBlockAddress.BlockIndex, data, _blockOffset);
		_blockOffset += data.Length;
	}

	private PageManager.PageAccessor GetPageByIndex(int pageIndex)
	{
		if (_pageList.TryGetByIndex(pageIndex, out var result))
		{
			return result;
		}

		result = _pageManager.GetPage(pageIndex);
		_pageList.Add(result);

		return result;
	}
}