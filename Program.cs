using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using EEUTILITY.Enums;
using EnergyExemplar.PLEXOS.Utility.Enums;
using PLEXOS_NET.Core;

namespace PlexosSolutionQuery;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var o = Options.Parse(args);
            if (o.ShowHelp) { Options.PrintHelp(); return 0; }
            if (!File.Exists(o.SolutionPath)) throw new FileNotFoundException("Solution ZIP was not found.", o.SolutionPath);

            var solution = new Solution();
            try
            {
                solution.Connection(o.SolutionPath);
                var collectionIds = solution.FetchAllCollectionIds();
                if (!collectionIds.ContainsKey(o.CollectionKey))
                {
                    Console.Error.WriteLine($"Collection '{o.CollectionKey}' is not available. Available keys:");
                    foreach (var key in collectionIds.Keys.OrderBy(x => x)) Console.Error.WriteLine("  " + key);
                    return 2;
                }
                var values = solution.QueryToList(o.Phase, collectionIds[o.CollectionKey], o.ParentName, o.ChildName, o.Period, o.Series);
                var output = o.OutputPath ?? Path.ChangeExtension(o.SolutionPath, ".query.csv");
                WriteRows(values, output);
                Console.WriteLine($"Wrote results to: {output}");
            }
            finally { solution.Close(); }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("PLEXOS query failed: " + ex.Message); return 1; }
    }

    private static void WriteRows(object values, string outputPath)
    {
        var rows = (values as IEnumerable)?.Cast<object>().ToList() ?? throw new InvalidOperationException("QueryToList returned a non-enumerable result.");
        var columns = rows.SelectMany(PublicValues).Select(x => x.Key).Distinct().OrderBy(x => x).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(",", columns.Select(Csv)));
        foreach (var row in rows)
        {
            var map = PublicValues(row).ToDictionary(x => x.Key, x => x.Value);
            writer.WriteLine(string.Join(",", columns.Select(c => Csv(map.TryGetValue(c, out var v) ? v : ""))));
        }
        Console.WriteLine($"Retrieved {rows.Count} row(s).");
    }

    private static IEnumerable<KeyValuePair<string, string>> PublicValues(object row) => row.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
        .Select(p => new KeyValuePair<string, string>(p.Name, Convert.ToString(p.GetValue(row), CultureInfo.InvariantCulture) ?? ""));
    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private sealed class Options
    {
        public string SolutionPath { get; private set; } = "";
        public string CollectionKey { get; private set; } = "SystemGenerators";
        public string ParentName { get; private set; } = "";
        public string ChildName { get; private set; } = "";
        public string? OutputPath { get; private set; }
        public bool ShowHelp { get; private set; }
        public SimulationPhaseEnum Phase { get; private set; } = SimulationPhaseEnum.STSchedule;
        public PeriodEnum Period { get; private set; } = PeriodEnum.Interval;
        public SeriesTypeEnum Series { get; private set; } = SeriesTypeEnum.Properties;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (var i = 0; i < args.Length; i++)
            {
                string Next() => ++i < args.Length ? args[i] : throw new ArgumentException($"Missing value for {args[i - 1]}");
                switch (args[i])
                {
                    case "--solution": o.SolutionPath = Next(); break;
                    case "--collection": o.CollectionKey = Next(); break;
                    case "--parent": o.ParentName = Next(); break;
                    case "--child": o.ChildName = Next(); break;
                    case "--output": o.OutputPath = Next(); break;
                    case "--phase": o.Phase = (SimulationPhaseEnum)Enum.Parse(typeof(SimulationPhaseEnum), Next(), true); break;
                    case "--period": o.Period = (PeriodEnum)Enum.Parse(typeof(PeriodEnum), Next(), true); break;
                    case "--series": o.Series = (SeriesTypeEnum)Enum.Parse(typeof(SeriesTypeEnum), Next(), true); break;
                    case "--help": case "-h": o.ShowHelp = true; break;
                    default: throw new ArgumentException("Unknown option: " + args[i]);
                }
            }
            if (!o.ShowHelp && string.IsNullOrWhiteSpace(o.SolutionPath)) throw new ArgumentException("--solution is required.");
            return o;
        }
        public static void PrintHelp() => Console.WriteLine("Usage: PlexosSolutionQuery --solution <solution.zip> [--collection SystemGenerators] [--parent name] [--child name] [--output file.csv]\nUse --phase, --period, and --series to provide PLEXOS enum names. Defaults: STSchedule, Interval, Properties.");
    }
}
