using System.Runtime.InteropServices;

namespace KeyValueDb.Paging;

[StructLayout(LayoutKind.Sequential)]
public readonly struct PageIndex : IEquatable<PageIndex>
{
	public const int Size = 4;

	public readonly uint Value;

	public static PageIndex Invalid { get; } = new(uint.MaxValue);

	public bool IsInvalid => this == Invalid;

	public PageIndex(uint value)
	{
		Value = value;
	}

	public override string ToString() => Value.ToString();

	public static implicit operator PageIndex(uint value) => new(value);

	public bool Equals(PageIndex other) => Value == other.Value;

	public override bool Equals(object? obj) => obj is PageIndex other && Equals(other);

	public override int GetHashCode() => Value.GetHashCode();

	public static bool operator ==(PageIndex left, PageIndex right) => left.Equals(right);

	public static bool operator !=(PageIndex left, PageIndex right) => !left.Equals(right);

	public static bool operator >(PageIndex left, PageIndex right) => left.Value > right.Value;

	public static bool operator >=(PageIndex left, PageIndex right) => left.Value >= right.Value;

	public static bool operator <(PageIndex left, PageIndex right) => left.Value < right.Value;

	public static bool operator <=(PageIndex left, PageIndex right) => left.Value <= right.Value;

	public static PageIndex operator +(PageIndex left, int right) => (uint)(left.Value + right);

	public static PageIndex operator ++(PageIndex left) => left.Value + 1;
}