using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetworkTools;

namespace MonitorService.Services
{
	public interface INetworkService
	{
		IList<(string url, int totalHops, int hopTimeouts, int slowHops, IReadOnlyList<long> hopReplyTimes)> RunNetworkTraces(string[] urls, int timeout, long slowThreshold);
		string SaveNetworkTraceReport(
			DateTimeOffset currentTime,
			IList<(string url, int totalHops, int hopTimeouts, int slowHops, IReadOnlyList<long> hopReplyTimes)> traceResults);
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
			this.logger.LogInformation("In NetworkService");
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

		public string SaveNetworkTraceReport(
			DateTimeOffset currentTime,
			IList<(string url, int totalHops, int hopTimeouts, int slowHops, IReadOnlyList<long> hopReplyTimes)> traceResults)
		{
			foreach (var traceResult in traceResults)
			{
				var (url, totalHops, hopTimeouts, slowHops, hopReplyTimes) = traceResult;
				this.logger.LogInformation($"{currentTime} {url} : hops: {totalHops} | timeouts: {hopTimeouts} | slow hops: {slowHops}");
			}

			return string.Empty;
		}
	}
}
