namespace Calibrator
{
    /// <summary>
    /// A service to process gcode files.
    /// </summary>
    public interface IFileProcessorService
    {
        /// <summary>
        /// Processes a gcode file.
        /// </summary>
        /// <param name="options">The command line options provided.</param>
        public void ProcessFile(Options options);
    }
}