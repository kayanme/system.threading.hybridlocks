using Benchmark.Paging.PhysicalLevel;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Benchmark.HybridLocks
{
    class Program
    {
        static void Main(string[] args)
        {
             RunAndPrint<LockBenchmark>("Lock");
        }

        private static void RunAndPrint<T>(string name)
        {
            var r = BenchmarkConverter.TypeToBenchmarks(typeof(T), new C(name));
            var ass = AppDomain.CurrentDomain.GetAssemblies().First(k => k.FullName.Contains("HybridLock"));
            var version = ass.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            var result = BenchmarkRunnerCore.Run(r, _ => new InProcessToolchain(false));
            foreach (var c in MarkdownExporter.GitHub.ExportToFiles(result, BenchmarkDotNet.Loggers.ConsoleLogger.Default))
            {
                var path = Path.GetFullPath($"Benchmarks//{name}_{version}.md");
                System.IO.File.Move(c, path);
                ConsoleLogger.Default.WriteLine($"results at {path}");
            }
            foreach (var c in HtmlExporter.Default.ExportToFiles(result, BenchmarkDotNet.Loggers.ConsoleLogger.Default))
            {
                var path = Path.GetFullPath($"Benchmarks//{name}_{version}.html");
                System.IO.File.Move(c, path);
                ConsoleLogger.Default.WriteLine($"results at {path}");
            }
            var exp = new BenchmarkDotNet.Exporters.Csv.CsvExporter(BenchmarkDotNet.Exporters.Csv.CsvSeparator.Semicolon);
            foreach (var c in exp.ExportToFiles(result, BenchmarkDotNet.Loggers.ConsoleLogger.Default))
            {
                var path = Path.GetFullPath($"Benchmarks//{name}_{version}.csv");
                System.IO.File.Move(c, path);
                ConsoleLogger.Default.WriteLine($"results at {path}");
            }


        }
    }
}
