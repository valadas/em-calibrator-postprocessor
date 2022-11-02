using CommandLine;
using em_calibrator.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Calibrator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServies(services);
            var options = Parser.Default.ParseArguments<Options>(args)
                    .Value;
            services
                .AddSingleton<Executor, Executor>()
                .BuildServiceProvider()
                .GetRequiredService<Executor>()
                .Execute(options);
        }

        private static void ConfigureServies(ServiceCollection services)
        {
            services
                .AddSerilog()
                .AddSingleton<IExtrusionMultiplierService, ExtrusionMultiplierService>();
        }
    }
}