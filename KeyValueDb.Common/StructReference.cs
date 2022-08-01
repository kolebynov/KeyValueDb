using System.Runtime.CompilerServices;

namespace KeyValueDb.Common;

public readonly unsafe struct StructReference<T>
	where T : struct
{
	private readonly void* _pointer;

	public ref T Value => ref Unsafe.AsRef<T>(_pointer);

	public StructReference(ref T value)
	{
		_pointer = Unsafe.AsPointer(ref value);
	}
}