using KeyValueDb.Common.Extensions;

namespace KeyValueDb.Common;

public class FileMappedStructure<T>
	where T : unmanaged
{
	private readonly FileStream _fileStream;
	private readonly long _filePosition;
	private T _structure;

	public ref readonly T ReadOnlyRef => ref _structure;

	public FileMappedStructure(FileStream fileStream, long filePosition, T initial, bool useInitial)
	{
		_fileStream = fileStream;
		_filePosition = filePosition;
		_structure = initial;

		if (useInitial)
		{
			Write();
		}
		else
		{
			Read();
		}
	}

	public MutableRef GetMutableRef() => new(this);

	private void Read()
	{
		_fileStream.ReadStructure(_filePosition, ref _structure);
	}

	private void Write()
	{
		_fileStream.WriteStructure(_filePosition, in _structure);
	}

	public readonly struct MutableRef : IDisposable
	{
		private readonly FileMappedStructure<T> _fileMappedStructure;

		public ref T Ref => ref _fileMappedStructure._structure;

		public MutableRef(FileMappedStructure<T> fileMappedStructure)
		{
			_fileMappedStructure = fileMappedStructure;
		}

		public void Dispose() => _fileMappedStructure?.Write();
	}
}