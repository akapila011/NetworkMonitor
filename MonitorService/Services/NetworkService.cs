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
using MonitorService.DTO;
using NetworkTools;

namespace MonitorService.Services
{
	public interface INetworkService
	{
		TraceEntrySummaryRow[] RunNetworkTraces(string[] urls, int timeout, long slowThreshold);
		Task SaveNetworkTraceReport(
			StorageService storage,
			string filePath,
			TraceEntrySummaryRow[] traceResults);

		Task<string> TrySendDailyEmail(DateTimeOffset currentTime, Emailo client, AppSettings settings,
			string dailyEmailPath, string networkTraceFilePath = null);

	}

	public class NetworkService : INetworkService
	{
		private readonly ILogger<NetworkService> logger;

		public NetworkService(ILogger<NetworkService> logger)
		{
			this.logger = logger;
		}

		public TraceEntrySummaryRow[] RunNetworkTraces(string[] urls, int timeout, long slowThreshold)
		{
			var results = new TraceEntrySummaryRow[urls.Length];
			var trace = new TraceRoute(); // TODO: DI
			Parallel.ForEach(urls, (url, state, index) =>
			{
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

					results[Convert.ToInt32(index)] = new TraceEntrySummaryRow()
					{
						URL = url,
						TotalHops = totalHops,
						HopTimeouts = hopTimeouts,
						SlowHops = slowHops,
						HopReplyTimes = hopReplyTimes.AsReadOnly()
					};
				}
				catch (Exception ex)
				{
					this.logger.LogError($"Something went wrong while tracing {url} : {ex.Message} | {ex.StackTrace} | {index} {Convert.ToInt32(index)}");
				}
			});
			return results;
		}
		public async Task SaveNetworkTraceReport(
			StorageService storage,
			string filePath,
			TraceEntrySummaryRow[] traceResults
			)
		{
			var csvRows = new List<(string url, int totalHops, int hopTimeouts, int slowHops, bool success)>(traceResults.Length);
			foreach (var traceResult in traceResults)
			{
				var success = !(traceResult.SlowHops >= (traceResult.TotalHops / 4)) || !(traceResult.HopTimeouts >= 2 && traceResult.TotalHops > 2);
				csvRows.Add((traceResult.URL, traceResult.TotalHops, traceResult.HopTimeouts, traceResult.SlowHops, success));
				//this.logger.LogInformation($"{currentTime} {url} : hops: {totalHops} | timeouts: {hopTimeouts} | slow hops: {slowHops}");
			}

			await storage.SaveNetworkTraceEntriesCsv(filePath, csvRows);
		}

		public async Task<string> TrySendDailyEmail(DateTimeOffset currentTime, Emailo client, AppSettings settings, string dailyEmailPath, string networkTraceFilePath = null)
		{
			var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(currentTime.Month);
			var subject = $"Network Monitor Report for {currentTime.DayOfWeek} {monthName} {currentTime.Year}";
			var body = new StringBuilder("Hello\n");
			if (!string.IsNullOrEmpty(networkTraceFilePath))
			{
				body.Append(this.BuildNetworkTraceDailyBody(networkTraceFilePath));
			}
			try
			{
				await File.WriteAllTextAsync(dailyEmailPath, $"{subject}\n\n{body}");
				await client.SendEmailAsync(settings.EmailReport.To, subject, body.ToString(), isBodyHtml: true);
			}
			catch (Exception ex)
			{
				File.Delete(dailyEmailPath);
				return $"Could not send Daily Email for {subject}";
			}

			return $"Sending Daily Email for {subject}";
		}

		private string BuildNetworkTraceDailyBody(string dailyCsvPath)
		{
			var body = new StringBuilder("\nNetwork trace routes summary:\n");
			var file = new StreamReader(dailyCsvPath);
			var counter = 0;
			string line;
			var (totalRows, totalSuccess) = (0, 0);
			var urlDataSummary = new Dictionary<string, (int timeouts, int slowHops, int success)>(); // maps a url to counters
			while((line = file.ReadLine()) != null)
			{
				counter++;
				if (counter == 1) continue; // skip headers
				var parts = line.Split(',');
				if (parts.Length >= 6)
				{
					totalRows++;
					var successTraceRoute = parts[5].Equals("true", StringComparison.OrdinalIgnoreCase);
					var url = parts[1];
					Int32.TryParse( parts[2], out var hopTimeouts);
					Int32.TryParse( parts[3], out var slowHops);
					(int timeouts, int slowHops, int success) value = (0, 0, 0);
					urlDataSummary.TryGetValue(url, out value);
					value.timeouts += hopTimeouts;
					value.slowHops += slowHops;
					if (successTraceRoute)
					{
						value.success++;
						totalSuccess++;
					}

					urlDataSummary[url] = value;
				}
			}

			var filename = Path.GetFileName(dailyCsvPath);
			body.AppendLine($"There were a total of <b>{totalRows}</b> traces done on this day and " +
			                $"out of those <b>{totalSuccess}</b> successful ones, and <b>{totalRows - totalSuccess}</b> failures.\n" +
			                "Site summaries:\n");
			body.AppendLine("<table>\n<thead>\n<tr>\n<th>URL</th><th>Number of Success</th><th>Hop Timeouts</th><th>Slow Hops</th>\n</tr>\n</thead>\n<tbody>");
			foreach (var entry in urlDataSummary)
			{
				var url = entry.Key;
				var (timeouts, slowHops, success) = entry.Value;
				body.AppendLine($"<tr><td>{url}</td><td>{success}</td><td>{timeouts}</td><td>{slowHops}</td></tr>\n");
			}
			body.AppendLine("</tbody>\n</table>");
			body.AppendLine($"The data can be found in {filename}");

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
