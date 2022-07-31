﻿using System.Runtime.InteropServices;

namespace KeyValueDb.Common.Extensions;

public static class FileStreamExtensions
{
	public static void ReadStructure<T>(this FileStream fileStream, long position, out T structure)
		where T : unmanaged
	{
		fileStream.Position = position;
		structure = default;
		if (fileStream.Read(structure.AsBytes()) != Marshal.SizeOf<T>())
		{
			throw new InvalidOperationException();
		}
	}

	public static void WriteStructure<T>(this FileStream fileStream, long position, ref T structure)
		where T : unmanaged
	{
		fileStream.Position = position;
		fileStream.Write(structure.AsReadOnlyBytes());
	}

	public static void Write(this FileStream fileStream, long position, ReadOnlySpan<byte> data)
	{
		fileStream.Position = position;
		fileStream.Write(data);
	}
}