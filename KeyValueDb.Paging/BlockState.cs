namespace KeyValueDb.Paging;

public enum BlockState : byte
{
	Free = 0,
	Busy = 1,
}