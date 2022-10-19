using System.Text.RegularExpressions;

namespace em_calibrator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var filename = "";
            if (args.Length == 0)
            {
                Console.WriteLine("Adjusts the from of the top infill gradually line per line between two values.");
                Console.WriteLine();
                Console.WriteLine("Note that this only works for a single object with a single top solid infill.");
                Console.WriteLine("Usage: em_calibrator.exe somefile.gcode min max");
                Console.WriteLine("Where min is the minimum and max is the maximum flow multiplier.");
                Console.WriteLine();
                Console.WriteLine("Enter the filename to process from 0.9 to 1.1 or press enter to exit.");

                filename = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(filename))
                {
                    return;
                }
            }
            else
            {
                filename = args[0];
            }

            double min = 0.8;
            if (args.Length > 1)
            {
                min = double.Parse(args[1]);
            }
            double max = 1.2;
            if (args.Length > 2)
            {
                max = double.Parse(args[2]);
            }

            var lines = File.ReadAllLines(filename);
            List<string> newLines = new List<string>();
            var passedTopLayer = false;
            var linesToProcess = new List<string>();
            foreach (var line in lines)
            {
                if (line.Contains(";TYPE:Top solid infill"))
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

            var moves = linesToProcess.Count(l => l.Contains("G1"));
            for (int i = 0; i < linesToProcess.Count; i++)
            {
                var line = linesToProcess[i];
                if (
                    line.Contains("G1")
                    && line.Contains("E")
                    && !line.Contains("E-"))
                {
                    var multiplier = min + (max - min) * (i / (double)moves);
                    var regex = new Regex(@"E(\d)+\.(\d)+");
                    var eString = regex.Match(line).Value.Substring(1);
                    double originalValue = double.Parse(eString);
                    double adjustedValue = originalValue * multiplier;
                    var newLine = regex.Replace(line, $"E{adjustedValue.ToString("0.00000")}");
                    newLines.Add(newLine);
                }
                else
                {
                    newLines.Add(line);
                }
            }

            File.WriteAllLines(filename, newLines);
        }
    }
}