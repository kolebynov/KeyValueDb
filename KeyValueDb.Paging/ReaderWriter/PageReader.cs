using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Paging.Extensions;

namespace KeyValueDb.Paging.ReaderWriter;

public struct PageReader
{
	private readonly PageManager _pageManager;
	private PageManager.PageAccessor _currentPage;
	private byte _currentBlockIndex;
	private int _currentBlockOffset = 0;

	public PageReader(PageManager pageManager, BlockAddress startAddress)
	{
		_pageManager = pageManager ?? throw new ArgumentNullException(nameof(pageManager));
		_currentPage = pageManager.GetPage(startAddress.PageIndex);
		_currentBlockIndex = startAddress.BlockIndex;
	}

	public void Read(Span<byte> buffer)
	{
		while (true)
		{
			var remainingBlockSpace = ReaderWriterConstants.BlockDataSize - _currentBlockOffset;
			if (buffer.Length <= remainingBlockSpace)
			{
				CopyBlock(buffer);
				return;
			}

			CopyBlock(buffer[..remainingBlockSpace]);
			buffer = buffer[remainingBlockSpace..];

			var nextBlockAddress = _currentPage.Page.GetNextBlockAddress(_currentBlockIndex);
			if (nextBlockAddress == BlockAddress.Invalid)
			{
				throw new InvalidOperationException();
			}

			if (nextBlockAddress.PageIndex != _currentPage.Page.Index)
			{
				_currentPage.Dispose();
				_currentPage = _pageManager.GetPage(nextBlockAddress.PageIndex);
			}

			_currentBlockIndex = nextBlockAddress.BlockIndex;
			_currentBlockOffset = 0;
		}
	}

	private void CopyBlock(Span<byte> buffer)
	{
		if (buffer.Length > ReaderWriterConstants.BlockDataSize - _currentBlockOffset)
		{
			throw new ArgumentException(string.Empty, nameof(buffer));
		}

		_currentPage.Page.GetBlockData(_currentBlockIndex, _currentBlockOffset, buffer.Length).CopyTo(buffer);
		_currentBlockOffset += buffer.Length;
	}
}