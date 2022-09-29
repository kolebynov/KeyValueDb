using System.Runtime.InteropServices;

namespace KeyValueDb.FileMemory.Paging;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct PageIndex : IEquatable<PageIndex>
{
	public const int Size = 8;

	public readonly ulong Value;

	public static PageIndex Invalid { get; } = new((1UL << 48) - 1);

	public bool IsInvalid => this >= Invalid;

	public PageIndex(ulong value)
	{
		Value = value;
	}

	public override string ToString() => Value.ToString();

	public static implicit operator PageIndex(ulong value) => new(value);

	public bool Equals(PageIndex other) => Value == other.Value;

	public override bool Equals(object? obj) => obj is PageIndex other && Equals(other);

	public override int GetHashCode() => Value.GetHashCode();

	public static bool operator ==(PageIndex left, PageIndex right) => left.Equals(right);

	public static bool operator !=(PageIndex left, PageIndex right) => !left.Equals(right);

	public static bool operator >(PageIndex left, PageIndex right) => left.Value > right.Value;

	public static bool operator >=(PageIndex left, PageIndex right) => left.Value >= right.Value;

	public static bool operator <(PageIndex left, PageIndex right) => left.Value < right.Value;

	public static bool operator <=(PageIndex left, PageIndex right) => left.Value <= right.Value;

	public static PageIndex operator +(PageIndex left, int right) => (ulong)((long)left.Value + right);

	public static PageIndex operator -(PageIndex left, int right) => (ulong)((long)left.Value - right);

	public static PageIndex operator +(PageIndex left, uint right) => left.Value + right;

	public static PageIndex operator -(PageIndex left, uint right) => left.Value - right;

	public static PageIndex operator ++(PageIndex left) => left.Value + 1;
}