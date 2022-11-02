using CommandLine;

namespace Calibrator
{
    public class Options
    {
        [Option(
            'v',
            "verbose",
            Required = false,
            HelpText = "Set output to verbose messages.",
            Default = false)]
        public bool Verbose { get; set; }

        [Option(
            'i',
            "input-file",
            Required = true,
            HelpText = "Input file to be processed.")]
        public string InputFile { get; set; } = "";

        [Option(
            "min-flow",
            Required = true,
            HelpText = "The minimum flow multiplier to use in percentage.")]
        public double MinFlow { get; set; }

        [Option(
            "max-flow",
            Required = true,
            HelpText = "The maximum flow multiplier to use in percentage.")]
        public double MaxFlow { get; set; }

        [Option(
            'o',
            "output-file",
            Required = false,
            HelpText = "Output file to be written.")]
        public string OutputFile { get; set; } = "";

        [Option(
            'a',
            "all-layers",
            Required = false,
            HelpText = "Process all layers, not just the top solid infill.")]
        public bool AllLayers { get; set; } = false;
    }
}
