using CommandLine;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Calibrator
{
    /// <summary>
    /// The executor to use with dependency injection services inject.
    /// </summary>
    public class Executor
    {
        private readonly ILogger logger;
        private readonly LoggingLevelSwitch levelSwitch;
        private readonly IExtrusionMultiplierService extrusionMultiplierService;

        public Executor(
            ILogger logger,
            LoggingLevelSwitch levelSwitch,
            IExtrusionMultiplierService extrusionMultiplierService)
        {
            this.logger = logger;
            this.levelSwitch = levelSwitch;
            this.extrusionMultiplierService = extrusionMultiplierService;
        }

        public void Execute(Options options)
        {
            if (options is null)
            {
                return;
            }
            
            try
            {
                this.levelSwitch.MinimumLevel = options.Verbose ?
                    LogEventLevel.Verbose :
                    LogEventLevel.Information;
                this.extrusionMultiplierService.ProcessFile(options);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, ex.Message);
                throw;
            }
        }
    }
}