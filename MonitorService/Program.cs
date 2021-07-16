using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using MonitorService.Config;
using MonitorService.Services;

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
	            // .ConfigureLogging((hostingContext, logging) =>
	            // {
		           //  logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
		           //  logging.AddEventLog(hostingContext.Configuration.GetSection("Logging:EventLog").Get<EventLogSettings>());
	            // })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>()
                    .Configure<EventLogSettings>(config =>
                        {
                            config.LogName = "NetworkMonitor Service";
                            config.SourceName = "NetworkMonitor Service Source";
                        });
                    services.Configure<AppSettings>(hostContext.Configuration.GetSection("AppSettings"));
                    services.AddScoped<INetworkService, NetworkService>();
                });
    }
}
