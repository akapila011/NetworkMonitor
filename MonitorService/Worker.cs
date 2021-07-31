using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
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
	                var storage = new StorageService(settings.DataFolder, currentTime);
	                var (weekPath, dayDirPath, year, weekNo, dayOfWeek) = storage.GetWeekFilePaths();

	                string networkTraceFilePath = null;

	                if (this.settings.Workloads.Contains("TraceRoute"))
	                {
		                networkTraceFilePath = await NetworkTraceJobAsync(networkService,
			                storage, dayDirPath, dayOfWeek);
	                }

	                // TODO: add workload ping

	                if (this.settings.ShouldSendEmail)
	                {
		                await this.HandleSendingEmails(networkService, currentTime, dayDirPath, weekPath, networkTraceFilePath);
	                }


	            }
                await Task.Delay(this.settings.Interval * 1000, stoppingToken);
            }
        }

        private async Task<string> NetworkTraceJobAsync(INetworkService networkService, StorageService storage, string dayDirPath, string dayOfWeek)
        {
	        var networkTraceFilePath = StorageService.Combine(dayDirPath, $"{dayOfWeek}-networktrace.csv");
	        var traceResults = networkService.RunNetworkTraces(this.settings.Urls, Convert.ToInt32(this.settings.HopTimeoutMs), this.settings.HopSlowThresholdMs);
	        await networkService.SaveNetworkTraceReport(storage , networkTraceFilePath, traceResults);
	        return networkTraceFilePath;
        }

        private async Task HandleSendingEmails(INetworkService networkService, DateTimeOffset currentTime, string dayDirPath, string weekDirPath, string networkTraceFilePath)
        {
	        var dailyEmailPath = Path.Join(dayDirPath, "daily-email.txt");
	        var weeklyEmailPath = Path.Join(weekDirPath, "weekly-email.txt");

	        var shouldSendDailyEmail = this.settings.EmailReport.Frequency.Contains("Daily") &&
	                                   currentTime.Hour > this.settings.EmailReport.HourSendReport &&
	                                   !File.Exists(dailyEmailPath);
	        var shouldSendWeeklyEmail = this.settings.EmailReport.Frequency.Contains("Weekly") &&
	                                    currentTime.DayOfWeek.ToString().Equals(this.settings.EmailReport.DaySendReport, StringComparison.OrdinalIgnoreCase) &&
	                                    currentTime.Hour > this.settings.EmailReport.HourSendReport &&
	                                    !File.Exists(weeklyEmailPath);

	        using (var client = new Emailo(settings.EmailReport.SenderSmtp, settings.EmailReport.GetSecureSenderPassword(), EmailSendCallback, smtp: settings.EmailReport.SmtpHost, port: Convert.ToInt32(settings.EmailReport.SmtpPort)))
	        {
		        if (shouldSendDailyEmail)
		        {
			        var dailyEmailStatus = await networkService.TrySendDailyEmail(currentTime, client, this.settings, dailyEmailPath, networkTraceFilePath);
		        }
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
