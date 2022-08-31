using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KeyValueDb.Common.Extensions;

public static class SpanExtensions
{
	public static Span<byte> AsBytes<T>(this ref T instance, int length = 1)
		where T : struct =>
		MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref instance, length));

	public static ReadOnlySpan<byte> AsReadOnlyBytes<T>(in T instance, int length = 1)
		where T : struct =>
		MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in instance), length));

	public static ref T AsRef<T>(this Span<byte> span) where T : struct => ref MemoryMarshal.AsRef<T>(span);

	public static ref readonly T AsRef<T>(this ReadOnlySpan<byte> span) where T : struct => ref MemoryMarshal.AsRef<T>(span);
}