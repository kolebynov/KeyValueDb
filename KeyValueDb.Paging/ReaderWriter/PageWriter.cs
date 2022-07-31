using KeyValueDb.Paging.Extensions;

namespace KeyValueDb.Paging.ReaderWriter;

public struct PageWriter
{
	private readonly PageManager _pageManager;
	private readonly PageList _pageList = new();
	private int _blockOffset = 0;
	private BlockAddress _currentBlockAddress = BlockAddress.Invalid;
	private BlockAddress _startAddress = BlockAddress.Invalid;

	public PageWriter(PageManager pageManager)
	{
		_pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));
	}

	public void Write(ReadOnlySpan<byte> data)
	{
		if (_currentBlockAddress == BlockAddress.Invalid)
		{
			GoToNextBlock();
		}

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

	public BlockAddress Commit()
	{
		if (_pageManager == null)
		{
			return BlockAddress.Invalid;
		}

		_pageList.Dispose();

		return _startAddress;
	}

	private void GoToNextBlock()
	{
		PageManager.PageAccessor currentPage;
		if (_pageList.Count == 0)
		{
			currentPage = _pageManager.GetPageWithFreeBlocks(0);
			_pageList.Add(currentPage);
		}
		else
		{
			currentPage = _pageList.GetByIndex(_currentBlockAddress.PageIndex);
		}

		if (!currentPage.Page.HasFreeBlocks)
		{
			currentPage = _pageManager.GetPageWithFreeBlocks(currentPage.Page.Index + 1);
			_pageList.Add(currentPage);
		}

		var prevBlockAddress = _currentBlockAddress;
		_currentBlockAddress = new BlockAddress(currentPage.Page.Index, currentPage.Page.FirstFreeBlock);

		currentPage.Page.SetNextBlockAddress(_currentBlockAddress.BlockIndex, BlockAddress.Invalid);
		if (prevBlockAddress != BlockAddress.Invalid)
		{
			var pageIndex = prevBlockAddress.PageIndex;
			var prevBlockPage = pageIndex == currentPage.Page.Index
				? currentPage.Page
				: _pageList.GetByIndex(pageIndex).Page;
			prevBlockPage.SetNextBlockAddress(prevBlockAddress.BlockIndex, _currentBlockAddress);
		}

		_blockOffset = 0;

		if (_startAddress == BlockAddress.Invalid)
		{
			_startAddress = _currentBlockAddress;
		}
	}

	private void CopyToBlock(ReadOnlySpan<byte> data)
	{
		_pageList.GetByIndex(_currentBlockAddress.PageIndex).Page
			.SetBlockData(_currentBlockAddress.BlockIndex, data, _blockOffset);
		_blockOffset += data.Length;
	}
}