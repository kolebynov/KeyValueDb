using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KeyValueDb.Paging;

[StructLayout(LayoutKind.Sequential)]
public readonly struct PageIndex : IEquatable<PageIndex>
{
	public readonly uint Value;

	public static PageIndex Invalid { get; } = new(uint.MaxValue);

	public PageIndex(uint value)
	{
		Value = value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator PageIndex(uint value) => new(value);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(PageIndex other) => Value == other.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object? obj) => obj is PageIndex other && Equals(other);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => Value.GetHashCode();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(PageIndex left, PageIndex right) => left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(PageIndex left, PageIndex right) => !left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >(PageIndex left, PageIndex right) => left.Value > right.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >=(PageIndex left, PageIndex right) => left.Value >= right.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <(PageIndex left, PageIndex right) => left.Value < right.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <=(PageIndex left, PageIndex right) => left.Value <= right.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static PageIndex operator +(PageIndex left, int right) => (uint)(left.Value + right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static PageIndex operator ++(PageIndex left) => left.Value + 1;
}