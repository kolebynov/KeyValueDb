namespace KeyValueDb.Paging.Exceptions;

public class FreePagesStackFullException : Exception
{
	public FreePagesStackFullException()
	{
	}

	public FreePagesStackFullException(string? message)
		: base(message)
	{
	}

	public FreePagesStackFullException(string? message, Exception? innerException)
		: base(message, innerException)
	{
	}
}