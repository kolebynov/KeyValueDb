using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using KeyValueDb;

foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().OrderBy(x => x.Key))
{
	Console.WriteLine($"{entry.Key}={entry.Value}");
}

var timer = Stopwatch.StartNew();

var smallString = GetString(38);
var mediumString = GetString(100);
var largeString = GetString(200);
var veryLargeString = GetString(2000);

var strings = new[] { smallString, mediumString, largeString, smallString, mediumString };
var buffer = new byte[200];

Console.WriteLine($"Strings allocated: {timer.Elapsed}");

using var db = new Database("test_new_paging4.db");

db.Set("key1", smallString);
db.Set("key2", mediumString);
db.Set("key3", largeString);

Console.WriteLine($"Strings stored: {timer.Elapsed}");

// var iterations = 10_000_000;

// for (var i = 0; i < iterations; i++)
// {
// 	db.TryGet("key1", buffer);
// 	db.TryGet("key2", buffer);
// 	db.TryGet("key3", buffer);
// }

var recordCount = 100_000;
for (var i = 0; i < recordCount; i++)
{
	if (i % 5000 == 0)
	{
		Console.WriteLine($"{i} records added");
	}

	db.Set($"{i}_key_{i}", strings[i % 5]);
}

Console.WriteLine($"Strings stored: {timer.Elapsed}, record count: {recordCount}");

static byte[] GetString(int length) =>
	Encoding.ASCII.GetBytes(string.Join(string.Empty, Enumerable.Range(0, length).Select(x => (x % 10).ToString(CultureInfo.InvariantCulture))));