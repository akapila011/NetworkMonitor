using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using CommunicationTools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonitorService.Config;
using NetworkTools;

namespace MonitorService.Services
{
	public interface INetworkService
	{
		IList<(string url, int totalHops, int hopTimeouts, int slowHops, IReadOnlyList<long> hopReplyTimes)> RunNetworkTraces(string[] urls, int timeout, long slowThreshold);
		Task<(string csvPath, string dailyEmailStatus, string weeklyEmailStatus)> SaveNetworkTraceReport(
			AppSettings settings,
			DateTimeOffset currentTime,
			IList<(string url, int totalHops, int hopTimeouts, int slowHops, IReadOnlyList<long> hopReplyTimes)> traceResults,
			bool trySendDailyEmail,
			bool trySendWeeklyEmail);
	}

	public class NetworkService : INetworkService
	{
		private readonly ILogger<NetworkService> logger;

		public NetworkService(ILogger<NetworkService> logger)
		{
			this.logger = logger;
		}

		public IList<(string url, int totalHops, int hopTimeouts, int slowHops, IReadOnlyList<long> hopReplyTimes)> RunNetworkTraces(string[] urls, int timeout, long slowThreshold)
		{
			var results = new List<(string url, int totalHops, int hopTimeouts, int slowHops, IReadOnlyList<long> hopReplyTimes)>(urls.Length);
			var trace = new TraceRoute(); // TODO: DI
			Parallel.ForEach(urls, (url, state, index) =>
			{
				results.Add((null, 0, 0, 0, null)); // hack - we need proper objects returned or we can't create an array, list does not fill the initial capacity
				try
				{
					this.logger.LogInformation($"Tracing {url}");
					var hops = trace.Tracert(url, timeout: timeout);
					var hopTimeouts = 0;
					var slowHops = 0;
					var totalHops = 0;
					var hopReplyTimes = new List<long>();
					foreach (var hop in hops)
					{
						// this.logger.LogInformation(
						// 	$"{hop.HopID} {hop.ReplyTime}ms {hop.ReplyStatus} {hop.Address} ({hop.Hostname}) " +
						// 	$"isSlowHop:{hop.IsSlowHop(this.settings.HopSlowThresholdMs)} | {url}");
						totalHops += 1;
						hopTimeouts += hop.ReplyStatus == IPStatus.TimedOut || hop.ReplyTime >= timeout ? 1 : 0;
						slowHops += hop.IsSlowHop(slowThreshold) ? 1 : 0;
						hopReplyTimes.Add(hop.ReplyTime);
					}

					results[Convert.ToInt32(index)] = (url, totalHops, hopTimeouts, slowHops, hopReplyTimes.AsReadOnly());
				}
				catch (Exception ex)
				{
					this.logger.LogError($"Something went wrong while tracing {url} : {ex.Message} | {ex.StackTrace} | {index} {Convert.ToInt32(index)}");
				}
			});
			return results;
		}
		public async Task<(string csvPath, string dailyEmailStatus, string weeklyEmailStatus)> SaveNetworkTraceReport(
			AppSettings settings,
			DateTimeOffset currentTime,
			IList<(string url, int totalHops, int hopTimeouts, int slowHops, IReadOnlyList<long> hopReplyTimes)> traceResults,
			bool trySendDailyEmail,
			bool trySendWeeklyEmail
			)
		{
			var csvRows = new List<(string url, int totalHops, int hopTimeouts, int slowHops, bool success)>(traceResults.Count);
			foreach (var traceResult in traceResults)
			{
				var (url, totalHops, hopTimeouts, slowHops, hopReplyTimes) = traceResult;
				var success = !(slowHops >= (totalHops / 4)) || !(hopTimeouts >= 2 && totalHops > 2);
				csvRows.Add((url, totalHops, hopTimeouts, slowHops, success));
				//this.logger.LogInformation($"{currentTime} {url} : hops: {totalHops} | timeouts: {hopTimeouts} | slow hops: {slowHops}");
			}

			var storage = new StorageService(settings.DataFolder, currentTime);
			var (weekPath, dayDirPath, recordPath) = storage.GetWeekFilePaths();
			await storage.SaveNetworkEntriesCsv(recordPath, csvRows);

			var (dailyEmailStatus, weeklyEmailStatus) = (string.Empty, string.Empty);
			if (settings.ShouldSendEmail)
			{
				using (var client = new Emailo(settings.EmailReport.SenderSmtp, settings.EmailReport.GetSecureSenderPassword(), sendCallback, smtp: settings.EmailReport.SmtpHost, port: Convert.ToInt32(settings.EmailReport.SmtpPort)))
				{
					dailyEmailStatus = await TrySendDailyEmail(trySendDailyEmail, currentTime, client, dayDirPath, recordPath, settings);
				}
			}
			else
			{
				dailyEmailStatus = "Should not send email - settings not configured";
				weeklyEmailStatus = "Should not send email - settings not configured";
			}

			return (recordPath, dailyEmailStatus, weeklyEmailStatus);
		}

		private async Task<string> TrySendDailyEmail(bool sendDailyEmail, DateTimeOffset currentTime, Emailo client, string dayDirPath, string dailyCsvPath, AppSettings settings)
		{
			if (!sendDailyEmail)
			{
				return "Not supposed to send daily email";
			}

			if (currentTime.Hour < 14)
			{
				return "Daily email reports will be sent after 9 PM";
			}

			var emailFilePath = Path.Join(dayDirPath, "daily-email.txt");
			if (File.Exists(emailFilePath))
			{
				return $"Email has already been sent for {currentTime.DayOfWeek}";
			}
			var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(currentTime.Month);
			var subject = $"Network Monitor Report for {currentTime.DayOfWeek} {monthName} {currentTime.Year}";
			var body = BuildDailyEmailBody(dailyCsvPath);
			try
			{
				await File.WriteAllTextAsync(emailFilePath, $"{subject}\n\n{body}");
				await client.SendEmailAsync(settings.EmailReport.To, subject, body, isBodyHtml: true);
			}
			catch (Exception ex)
			{
				File.Delete(emailFilePath);
				return $"Could not send Daily Email for {subject}";
			}

			return $"Sending Daily Email for {subject}";
		}

		private string BuildDailyEmailBody(string dailyCsvPath)
		{
			var body = new StringBuilder("Hello,\n\nFrom Network traces done on this day the following was observed:\n");
			var file = new StreamReader(dailyCsvPath);
			int counter = 0;
			string line;
			var (totalRows, totalSuccess) = (0, 0);
			while((line = file.ReadLine()) != null)
			{
				counter++;
				if (counter == 1) continue; // skip headers
				var parts = line.Split(',');
				if (parts.Length >= 6)
				{
					totalRows++;
					if (parts[5].Equals("true", StringComparison.InvariantCultureIgnoreCase))
					{
						totalSuccess++;
					}
				}
			}

			body.AppendLine($"There were a total of <b>{totalRows}</b> done on this day and " +
			                $"out of those <b>{totalSuccess}</b> successful ones, and <b>{totalRows - totalSuccess}</b> failures.\n" +
			                $"The data can be found in {dailyCsvPath}");

			return body.ToString();
		}

		private void sendCallback(object sender, AsyncCompletedEventArgs args)
		{
			if (args.Cancelled || args.Error != null)
			{
				this.logger.LogError($"Error sending message {args.Error?.Message} | cancelled: {args.Cancelled}");
				return;
			}
			this.logger.LogInformation("Email sent!");
		}
	}
}
