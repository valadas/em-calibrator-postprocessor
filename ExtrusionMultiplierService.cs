using Serilog;
using System.Text;
using System.Text.RegularExpressions;

namespace Calibrator
{
    /// <summary>
    /// Provides methods to tune extrusion multiplier on a gcode file.
    /// </summary>
    internal class ExtrusionMultiplierService : IExtrusionMultiplierService
    {
        readonly ILogger logger;

        public ExtrusionMultiplierService(ILogger logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public void ProcessFile(Options options)
        {
            logger.Information($"Processing {options.InputFile}");
            var fileContents = File.ReadAllText(options.InputFile);
            var layers = Regex.Split(fileContents, "(?<=;LAYER_CHANGE)");
            logger.Information($"Found {layers.Length - 1} layers");
            var result = new StringBuilder();

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (i == 0)
                {
                    result.Append(layer);
                    continue;
                }
                
                if (!options.AllLayers && !layer.Contains(";TYPE:Top solid infill"))
                {
                    result.Append(layer);
                    continue;
                }

                this.logger.Information($"Adjusting infill on layer {i}");
                result.Append(this.AdjustLayer(layer, options));
            }

            this.SaveFile(result, options);
        }

        private string AdjustLayer(string layer, Options options)
        {
            var result = new StringBuilder();
            var lines = Regex.Split(layer, "\r\n|\r|\n");
            var passedInfill = false;
            var infillLines = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Contains(";TYPE") && line.Contains("infill", StringComparison.OrdinalIgnoreCase))
                {
                    passedInfill = true;
                }
                
                if (!passedInfill)
                {
                    result.AppendLine(line);
                    continue;
                }

                infillLines.Add(line);
            }

            result.Append(this.AdjustInfill(infillLines, options));
            
            return result.ToString();
        }

        private string AdjustInfill(List<string>? infillLines, Options options)
        {
            var newLines = new List<string>();
            if (infillLines is null || !infillLines.Any())
            {
                return String.Empty;
            }

            var printMovesCount = infillLines.Count(l =>
                l.Contains("G1") &&
                l.Contains("E") &&
                !l.Contains("E-"));
            var currentLine = 1;
            for (int i = 0; i < infillLines.Count; i++)
            {
                var line = infillLines[i];
                var lineWithoutComment = line.Split(';')[0];
                if (line is null || string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (
                    lineWithoutComment.Contains("G1")
                    && lineWithoutComment.Contains("E")
                    && !lineWithoutComment.Contains("E-"))
                {
                    var multiplier = (options.MinFlow + (options.MaxFlow - options.MinFlow) * (currentLine / (double)printMovesCount)) / 100;
                    currentLine++;
                    var regex = new Regex(@"E(\d)+\.(\d)+");
                    try
                    {
                        var eString = regex.Match(line).Value.Substring(1);
                        double originalValue = double.Parse(eString);
                        double adjustedValue = originalValue * multiplier;
                        var newLine = regex.Replace(line, $"E{adjustedValue.ToString("0.00000")}");
                        newLine = $"{newLine} ;Flow adjsuted from {originalValue} to {adjustedValue.ToString("0.00000")} with multiplier {multiplier.ToString("0.00000")}";
                        this.logger.Verbose(newLine);
                        newLines.Add(newLine);
                    }
                    catch (Exception)
                    {
                        Console.Error.WriteLine(i);
                        Console.Error.WriteLine(infillLines);
                        Console.Error.WriteLine(infillLines[i]);
                        throw;
                    }
                }
                else
                {
                    newLines.Add(line);
                }
            }

            return string.Join("\r\n", newLines);
        }

        private void SaveFile(StringBuilder result, Options options)
        {
            if (string.IsNullOrWhiteSpace(options.OutputFile))
            {
                options.OutputFile = options.InputFile;
            }

            var outFile = new FileInfo(options.OutputFile);
            if (outFile != null && outFile.Directory != null && !outFile.Directory.Exists)
            {
                outFile.Directory.Create();
            }

            File.WriteAllText(options.OutputFile, result.ToString());
            Log.Information($"Saving modified file {options.OutputFile}");
        }
    }
}
