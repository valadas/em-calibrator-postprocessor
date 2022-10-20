using CommandLine;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace em_calibrator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(options =>
                {
                    try
                    {
                        ProcessFile(options);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.Write(ex.Message);
                        throw;
                    }
                });
        }

        private static void ProcessFile(Options options)
        {
            if (options.Verbose)
            {
                Console.WriteLine($"Parsing Input file: {options.InputFile}");
            }

            var lines = File.ReadAllLines(options.InputFile);
            if (options.Verbose)
            {
                Console.WriteLine($"Processing {lines.Length} lines");
            }

            List<string> newLines = new List<string>();
            var passedTopLayer = false;
            var linesToProcess = new List<string>();
            foreach (var line in lines)
            {
                if (!passedTopLayer && line.Contains(";TYPE:Top solid infill"))
                {
                    passedTopLayer = true;
                }

                if (!passedTopLayer)
                {
                    newLines.Add(line);
                    continue;
                }

                linesToProcess.Add(line);
            }

            if (options.Verbose)
            {
                Console.WriteLine($"Processing {linesToProcess.Count} moves for the top layer infill.");
            }

            var linesWithPrintMoves = linesToProcess.Count(l =>
                l.Contains("G1")
                && l.Contains("E")
                && !l.Contains("E-"));
            var currentLine = 1;
            for (int i = 0; i < linesToProcess.Count; i++)
            {
                var line = linesToProcess[i];
                if (
                    line.Contains("G1")
                    && line.Contains("E")
                    && !line.Contains("E-"))
                {
                    var multiplier = (options.MinFlow + (options.MaxFlow - options.MinFlow) * (currentLine / (double)linesWithPrintMoves)) / 100;
                    currentLine++;
                    var regex = new Regex(@"E(\d)+\.(\d)+");
                    var eString = regex.Match(line).Value.Substring(1);
                    double originalValue = double.Parse(eString);
                    double adjustedValue = originalValue * multiplier;
                    var newLine = regex.Replace(line, $"E{adjustedValue.ToString("0.00000")}");
                    newLine = $"{newLine} ;Flow adjsuted from {originalValue} to {adjustedValue.ToString("0.00000")} with multiplier {multiplier}";
                    if (options.Verbose)
                    {
                        Console.WriteLine(newLine);
                    }
                    newLines.Add(newLine);
                }
                else
                {
                    newLines.Add(line);
                }
            }

            if (string.IsNullOrWhiteSpace(options.OutputFile))
            {
                options.OutputFile = options.InputFile;
            }

            var outFile = new FileInfo(options.OutputFile);
            if (
                outFile != null
                && outFile.Directory != null
                && !outFile.Directory.Exists)
            {
                outFile.Directory.Create();
            }

            File.WriteAllLines(options.OutputFile, newLines);
        }
    }
}