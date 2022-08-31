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

var strings = new[] { smallString, mediumString, largeString, smallString, mediumString };

Console.WriteLine($"Strings allocated: {timer.Elapsed}");

using var db = new Database("test_new_paging.db");
db.Set("key1", smallString);
db.Set("key2", mediumString);
db.Set("key3", largeString);
var buffer = new byte[200];

Console.WriteLine($"Strings stored: {timer.Elapsed}");
var iterations = 20_000_000;

for (var i = 0; i < iterations; i++)
{
	db.TryGet("key1", buffer);
	db.TryGet("key2", buffer);
	db.TryGet("key3", buffer);
}

Console.WriteLine($"Strings read: {timer.Elapsed}, iterations: {iterations}");
Console.WriteLine("Result strings:");

foreach (var s in new[] { "key1", "key2", "key3" })
{
	Console.WriteLine(Encoding.ASCII.GetString(db.Get(s)!));
}

static byte[] GetString(int length) =>
	Encoding.ASCII.GetBytes(string.Join(string.Empty, Enumerable.Range(0, length).Select(x => (x % 10).ToString(CultureInfo.InvariantCulture))));