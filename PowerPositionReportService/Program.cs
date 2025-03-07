using Serilog;
using PowerPositionReportService;
using Services;
using PowerPositionReportService.Utils;

namespace PowerPositionService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(
                    new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .Build())
                .CreateLogger();

            try
            {
                Log.Information("Starting up the service...");
                var builder = Host.CreateDefaultBuilder(args)
                    .UseSerilog() // Use Serilog for logging
                    .ConfigureServices((hostContext, services) =>
                    {
                        services.AddHostedService<PowerPositionReportWorker>();
                        services.AddSingleton<IPowerService, PowerService>();
                        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
                        services.AddSingleton<IFileWriter, FileWriter>();
                    });

                if (!Environment.UserInteractive)
                {
                    // Production: running as a Windows service
                    builder.UseWindowsService();
                }
                else
                {
                    // Local development: running as a console application
                    builder.UseConsoleLifetime();
                }

                await builder.Build().RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "The service terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
