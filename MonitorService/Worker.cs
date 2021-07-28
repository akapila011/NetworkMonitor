using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CommunicationTools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MonitorService.Config;
using MonitorService.Services;
using NetworkTools;

namespace MonitorService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;
        private readonly AppSettings settings;
        private readonly IServiceProvider services;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> settings, IServiceProvider services)
        {
            this.logger = logger;
            this.settings = settings.Value;
            this.services = services;

            if (!this.settings.Validate(out var settingWarnings, out var settingErrors))
            {
	            foreach (var errorMessage in settingErrors)
	            {
		            this.logger.LogWarning(errorMessage);
	            }
	            throw new ArgumentException("Invalid settings, please check the service logs");
            }
            foreach (var warningMessage in settingWarnings)
            {
	            this.logger.LogWarning(warningMessage);
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
	        this.logger.LogInformation($"Service starting with validated settings (ShouldSendEmailReports = {this.settings.ShouldSendEmail})");
	        return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
	            var currentTime = DateTimeOffset.Now;
                this.logger.LogInformation("Worker running at: {time}", currentTime);

                using (var scope = this.services.CreateScope())
                {
	                var networkService = scope.ServiceProvider.GetRequiredService<INetworkService>();
	                var traceResults = networkService.RunNetworkTraces(this.settings.Urls, Convert.ToInt32(this.settings.HopTimeoutMs), this.settings.HopSlowThresholdMs);
	                var (saveFile, dailyEmailStatus, weeklyEmailStatus) = await networkService.SaveNetworkTraceReport(
		                this.settings ,currentTime, traceResults,
		                this.settings.EmailReport.Frequency.Contains("Daily"),
		                this.settings.EmailReport.Frequency.Contains("Weekly"));
                }

                await Task.Delay(this.settings.Interval, stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
	        this.logger.LogInformation("Stopping Service");
	        await base.StopAsync(cancellationToken);
        }

        private void EmailSendCallback(object sender, AsyncCompletedEventArgs args)
        {
	        if (args.Cancelled || args.Error != null)
	        {
		        this.logger.LogError($"Error sending message {args.Error?.Message} | cancelled: {args.Cancelled}");
		        return;
	        }
	        this.logger.LogDebug("Email sent!");
        }
    }
}
