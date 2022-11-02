using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;

namespace em_calibrator.Extensions
{
    internal static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddSerilog(this IServiceCollection services)
        {
            var levelSwitch = new LoggingLevelSwitch();
            levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Console()
                .CreateLogger();
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            services.AddSingleton(Log.Logger);
            services.AddSingleton(levelSwitch);

            return services;
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            Log.CloseAndFlush();
        }
    }
}
