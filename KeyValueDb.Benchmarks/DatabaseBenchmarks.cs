using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace KeyValueDb.Benchmarks;

#pragma warning disable CA1001
public class DatabaseBenchmarks
#pragma warning restore CA1001
{
	private readonly byte[] _buffer = new byte[200];
	private Database _database = null!;

	[GlobalSetup]
	public void Setup()
	{
		_database = new Database("benchmark.db");
		_database.Set("key", GetString(38));
	}

	[Benchmark]
	public void TryGetBenchmark()
	{
		_database.TryGet("key1", _buffer);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_database.Dispose();
	}

	private static byte[] GetString(int length) =>
		Encoding.ASCII.GetBytes(string.Join(
			string.Empty,
			Enumerable.Range(0, length).Select(x => (x % 10).ToString(CultureInfo.InvariantCulture))));
}