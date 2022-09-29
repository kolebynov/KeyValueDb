namespace KeyValueDb.Common.Extensions;

public static class DisposableExtensions
{
	public static void AssignNewDisposableToVariable<T>(this ref T disposableVariable, T newDisposable)
		where T : struct, IDisposable
	{
		disposableVariable.Dispose();
		disposableVariable = newDisposable;
	}
}