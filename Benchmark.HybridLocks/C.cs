using System;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Benchmark.Paging.PhysicalLevel
{
    public class C : ManualConfig
    {
        

        public C(string group)
        {
            var exp = new BenchmarkDotNet.Exporters.Csv.CsvExporter(BenchmarkDotNet.Exporters.Csv.CsvSeparator.Semicolon);
            Add(Job.Core.WithInvocationCount(96 * 2).WithLaunchCount(3).WithAnalyzeLaunchVariance(true));          
            Add(DefaultConfig.Instance.GetLoggers().ToArray());
            Add(TargetMethodColumn.Method);
            Add(DefaultColumnProviders.Params);
            var ass = AppDomain.CurrentDomain.GetAssemblies().First(k => k.FullName.Contains("IO.Paging.PhysicalLevel"));
            var version = ass.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
            Add(new DataColumn("Version",version));
            Add(new DataColumn("Group", group));
            Add(StatisticColumn.Mean, StatisticColumn.Error);            
        }


        private class DataColumn : IColumn
        {
            private readonly string _value;
            private readonly string _key;

            public DataColumn(string key, string value)
            {
                _value = value;
                _key = key;
            }
            public string Id => _key;

            public string ColumnName => _key;

            public bool AlwaysShow => true;

            public ColumnCategory Category => ColumnCategory.Meta;

            public int PriorityInCategory => 0;

            public bool IsNumeric => false;

            public UnitType UnitType => UnitType.Dimensionless;

            public string Legend => "";

            public string GetValue(Summary summary, BenchmarkDotNet.Running.Benchmark benchmark)
            {
                return _value;
            }

            public string GetValue(Summary summary, BenchmarkDotNet.Running.Benchmark benchmark, ISummaryStyle style)
            {
                return _value;
            }

            public bool IsAvailable(Summary summary)
            {
                return true;
            }

            public bool IsDefault(Summary summary, BenchmarkDotNet.Running.Benchmark benchmark)
            {
                return true;
            }
        }

    }
}