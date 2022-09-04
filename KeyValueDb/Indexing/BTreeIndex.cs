using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KeyValueDb.Common.Extensions;
using KeyValueDb.Records;

namespace KeyValueDb.Indexing;

public sealed class BTreeIndex
{
	private const ushort T = 36;
	private const ushort MinEntriesPerNode = T - 1;
	private const ushort MaxEntriesPerNode = (2 * T) - 1;

	private static readonly RecordAddress RootNodeAddress = new(0, 0);

	private readonly RecordManager _recordManager;

	public BTreeIndex(RecordManager recordManager, bool forceInitialize)
	{
		_recordManager = recordManager;
		if (forceInitialize)
		{
			var rootNode = NodeData.Initial;
			if (_recordManager.Add(rootNode.AsBytes()) != RootNodeAddress)
			{
				throw new InvalidOperationException($"Root node must be placed by address {RootNodeAddress}");
			}
		}
	}

	public bool TryAdd(ReadOnlySpan<char> key, ReadOnlySpan<byte> value)
	{
		ref readonly var rootNode = ref GetNode(RootNodeAddress);
		if (rootNode.EntryCount == MaxEntriesPerNode)
		{
		}

		return false;
	}

	public bool TryGet(ReadOnlySpan<char> key, out ReadOnlySpan<byte> value)
	{
		value = default!;
		return false;
	}

	public bool TryRemove(ReadOnlySpan<char> key)
	{
		return false;
	}

	private ref readonly NodeData GetNode(RecordAddress nodeAddress) => ref _recordManager.Get(nodeAddress).AsRef<NodeData>();

	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct NodeData
	{
		private fixed byte _entryAddresses[MaxEntriesPerNode * RecordAddress.Size];
		private fixed byte _childrenAddresses[(MaxEntriesPerNode + 1) * RecordAddress.Size];

		public ushort EntryCount;
		public bool IsLeaf;

		public Span<RecordAddress> EntryAddresses =>
			MemoryMarshal.CreateSpan(ref Unsafe.As<byte, RecordAddress>(ref _entryAddresses[0]), MaxEntriesPerNode);
		public Span<RecordAddress> ChildrenAddresses =>
			MemoryMarshal.CreateSpan(ref Unsafe.As<byte, RecordAddress>(ref _childrenAddresses[0]), MaxEntriesPerNode + 1);

		public static NodeData Initial => default;
	}
}