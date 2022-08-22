using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using KeyValueDb.Benchmarks;

var job = Job.Default
	.WithEnvironmentVariables(
		new EnvironmentVariable("DOTNET_TC_QuickJitForLoops", "1"),
		new EnvironmentVariable("DOTNET_TieredPGO", "1"),
		new EnvironmentVariable("DOTNET_ReadyToRun", "0"));
BenchmarkRunner.Run<DatabaseBenchmarks>(DefaultConfig.Instance
	.AddJob(job.WithRuntime(CoreRuntime.CreateForNewVersion("net7.0", ".NET 7.0"))));