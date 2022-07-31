﻿using System.Runtime.InteropServices;
using KeyValueDb.Paging;

namespace KeyValueDb;

public struct DbSystemInfo
{
	public static readonly int Size = Marshal.SizeOf<DbSystemInfo>();

	public BlockAddress FirstRecord;

	public BlockAddress LastRecord;
}