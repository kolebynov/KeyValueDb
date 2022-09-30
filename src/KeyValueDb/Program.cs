using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using KeyValueDb;

public static class Program
{
	private static readonly byte[] SmallString = GetString(38);
	private static readonly byte[] MediumString = GetString(100);
	private static readonly byte[] LargeString = GetString(200);
	private static readonly byte[] VeryLargeString = GetString(2000);
	private static readonly byte[][] Strings = new[] { SmallString, MediumString, LargeString, SmallString, MediumString };

	public static void Main()
	{
		foreach (DictionaryEntry pair in Environment.GetEnvironmentVariables())
		{
			Console.WriteLine($"{pair.Key}={pair.Value}");
		}

		TestRead();
	}

	private static void TestRead()
	{
		var timer = Stopwatch.StartNew();

		var buffer = new byte[200];

		Console.WriteLine($"Strings allocated: {timer.Elapsed}");

		using var db = new Database("test_read.db");

		db.Set("key1", SmallString);
		db.Set("key2", MediumString);
		db.Set("key3", LargeString);

		Console.WriteLine($"Strings stored: {timer.Elapsed}");

		var iterations = 200_000;

		for (var i = 0; i < iterations; i++)
		{
			db.TryGet("key1", buffer);
			db.TryGet("key2", buffer);
			db.TryGet("key3", buffer);
		}

		Console.WriteLine($"Strings read: {timer.Elapsed}, iterations: {iterations}");

		foreach (var key in new[] { "key1", "key2", "key3" })
		{
			Console.WriteLine(Encoding.UTF8.GetString(db.Get(key)!));
		}
	}

	private static byte[] GetString(int length) =>
		Encoding.ASCII.GetBytes(string.Join(string.Empty, Enumerable.Range(0, length).Select(x => (x % 10).ToString(CultureInfo.InvariantCulture))));
}