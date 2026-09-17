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

namespace PlexosSolutionQuery
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            dynamic solution = null;

            try
            {
                Options options = Options.Parse(args);

                if (options.ShowHelp)
                {
                    Options.PrintHelp();
                    return 0;
                }

                if (!File.Exists(options.SolutionPath))
                {
                    throw new FileNotFoundException(
                        "PLEXOS solution ZIP was not found.",
                        options.SolutionPath);
                }

                solution = new Solution();
                solution.Connection(options.SolutionPath);

                dynamic collectionIds = solution.FetchAllCollectionIds();

                if (!collectionIds.ContainsKey(options.CollectionKey))
                {
                    Console.Error.WriteLine(
                        "Collection '" + options.CollectionKey + "' is not available.");
                    Console.Error.WriteLine("Available collection keys:");

                    foreach (string key in collectionIds.Keys)
                    {
                        Console.Error.WriteLine("  " + key);
                    }

                    return 2;
                }

                // QueryToList is the PLEXOS-recommended faster .NET query method.
                dynamic values = solution.QueryToList(
                    options.Phase,
                    collectionIds[options.CollectionKey],
                    options.ParentName,
                    options.ChildName,
                    options.Period,
                    options.Series);

                string outputPath = options.OutputPath;

                if (String.IsNullOrWhiteSpace(outputPath))
                {
                    outputPath = Path.ChangeExtension(
                        options.SolutionPath,
                        ".query.csv");
                }

                WriteRows(values, outputPath);

                Console.WriteLine("Wrote results to:");
                Console.WriteLine(outputPath);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("PLEXOS query failed:");
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                if (solution != null)
                {
                    try
                    {
                        solution.Close();
                    }
                    catch
                    {
                        // Keep the original error, if any.
                    }
                }
            }
        }

        private static void WriteRows(object values, string outputPath)
        {
            IEnumerable enumerable = values as IEnumerable;

            if (enumerable == null)
            {
                throw new InvalidOperationException(
                    "PLEXOS QueryToList returned a result that cannot be read as rows.");
            }

            List<object> rows = enumerable.Cast<object>().ToList();

            List<string> columns = rows
                .SelectMany(PublicValues)
                .Select(item => item.Key)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            string fullOutputPath = Path.GetFullPath(outputPath);
            string outputDirectory = Path.GetDirectoryName(fullOutputPath);

            if (!String.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            using (StreamWriter writer = new StreamWriter(
                fullOutputPath,
                false,
                new UTF8Encoding(true)))
            {
                writer.WriteLine(String.Join(",", columns.Select(Csv)));

                foreach (object row in rows)
                {
                    Dictionary<string, string> rowValues = PublicValues(row)
                        .ToDictionary(item => item.Key, item => item.Value);

                    writer.WriteLine(String.Join(
                        ",",
                        columns.Select(column =>
                            Csv(rowValues.ContainsKey(column)
                                ? rowValues[column]
                                : ""))));
                }
            }

            Console.WriteLine("Retrieved " + rows.Count + " row(s).");
        }

        private static IEnumerable<KeyValuePair<string, string>> PublicValues(object row)
        {
            return row.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead &&
                                   property.GetIndexParameters().Length == 0)
                .Select(property => new KeyValuePair<string, string>(
                    property.Name,
                    Convert.ToString(
                        property.GetValue(row, null),
                        CultureInfo.InvariantCulture) ?? ""));
        }

        private static string Csv(string value)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private sealed class Options
        {
            public string SolutionPath { get; private set; }
            public string CollectionKey { get; private set; }
            public string ParentName { get; private set; }
            public string ChildName { get; private set; }
            public string OutputPath { get; private set; }
            public bool ShowHelp { get; private set; }

            public SimulationPhaseEnum Phase { get; private set; }
            public PeriodEnum Period { get; private set; }
            public SeriesTypeEnum Series { get; private set; }

            public Options()
            {
                SolutionPath = "";
                CollectionKey = "SystemGenerators";
                ParentName = "";
                ChildName = "";
                OutputPath = "";
                Phase = SimulationPhaseEnum.STSchedule;
                Period = PeriodEnum.Interval;
                Series = SeriesTypeEnum.Properties;
            }

            public static Options Parse(string[] args)
            {
                Options options = new Options();

                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--solution":
                            options.SolutionPath = NextValue(args, ref i);
                            break;

                        case "--collection":
                            options.CollectionKey = NextValue(args, ref i);
                            break;

                        case "--parent":
                            options.ParentName = NextValue(args, ref i);
                            break;

                        case "--child":
                            options.ChildName = NextValue(args, ref i);
                            break;

                        case "--output":
                            options.OutputPath = NextValue(args, ref i);
                            break;

                        case "--phase":
                            options.Phase = (SimulationPhaseEnum)Enum.Parse(
                                typeof(SimulationPhaseEnum),
                                NextValue(args, ref i),
                                true);
                            break;

                        case "--period":
                            options.Period = (PeriodEnum)Enum.Parse(
                                typeof(PeriodEnum),
                                NextValue(args, ref i),
                                true);
                            break;

                        case "--series":
                            options.Series = (SeriesTypeEnum)Enum.Parse(
                                typeof(SeriesTypeEnum),
                                NextValue(args, ref i),
                                true);
                            break;

                        case "--help":
                        case "-h":
                            options.ShowHelp = true;
                            break;

                        default:
                            throw new ArgumentException(
                                "Unknown option: " + args[i]);
                    }
                }

                if (!options.ShowHelp &&
                    String.IsNullOrWhiteSpace(options.SolutionPath))
                {
                    throw new ArgumentException("--solution is required.");
                }

                return options;
            }

            private static string NextValue(string[] args, ref int index)
            {
                index++;

                if (index >= args.Length)
                {
                    throw new ArgumentException("A command-line value is missing.");
                }

                return args[index];
            }

            public static void PrintHelp()
            {
                Console.WriteLine(
                    "Usage: PlexosSolutionQuery --solution <solution.zip> [options]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --collection <key>  Default: SystemGenerators");
                Console.WriteLine("  --parent <name>     Optional parent-object filter");
                Console.WriteLine("  --child <name>      Optional child-object filter");
                Console.WriteLine("  --phase <enum>      Default: STSchedule");
                Console.WriteLine("  --period <enum>     Default: Interval");
                Console.WriteLine("  --series <enum>     Default: Properties");
                Console.WriteLine("  --output <file>     CSV output location");
            }
        }
    }
}
