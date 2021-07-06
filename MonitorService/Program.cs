using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using MonitorService.Config;

namespace MonitorService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
	            .UseWindowsService()
	            .ConfigureAppConfiguration((context, config) =>
	            {
		            // configure the app here.
	            })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>()
                    //.ConfigureLogging(configureLogging => configureLogging.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information))
                    .Configure<EventLogSettings>(config =>
                        {
                            config.LogName = "NetworkMonitor Service";
                            config.SourceName = "NetworkMonitor Service Source";
                        });
                    services.Configure<AppSettings>(hostContext.Configuration.GetSection("AppSettings"));
                });
    }
}
