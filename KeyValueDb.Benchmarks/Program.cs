using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using KeyValueDb.Benchmarks;

BenchmarkRunner.Run<DatabaseBenchmarks>(DefaultConfig.Instance
	.AddJob(Job.Default
		.WithRuntime(CoreRuntime.Core60)
		.WithEnvironmentVariables(
			new EnvironmentVariable("DOTNET_TC_QuickJitForLoops", "1"),
			new EnvironmentVariable("DOTNET_TieredPGO", "1"),
			new EnvironmentVariable("DOTNET_ReadyToRun", "0")))
	.AddLogger(ConsoleLogger.Default));