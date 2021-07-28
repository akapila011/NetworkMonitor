using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetworkTools
{
    public class TraceRoute
    {
        /// <summary>
        /// Traces the route which data have to travel through in order to reach an IP address.
        /// e.g. Tracert("8.8.8.8", 10, 10000);
        /// </summary>
        /// <param name="host">The IP address or hostname of the destination.</param>
        /// <param name="maxHops">Max hops to be returned.</param>
        /// <param name="timeout">Max hops to be returned.</param>
        /// <returns>A list of hops with details of the request made</returns>
        public IEnumerable<TracertEntry> Tracert(string host, int maxHops = 30, int timeout = 10000)
        {
	        var hops = new List<TracertEntry>(maxHops);
            // Max hops should be at least one or else there won't be any data to return.
            if (maxHops < 1)
                throw new ArgumentException("Max hops can't be lower than 1.");
            // Ensure that the timeout is not set to 0 or a negative number.
            if (timeout < 1)
				throw new ArgumentException("Timeout value must be higher than 0.");

            var ping = new Ping();
            var pingOptions = new PingOptions(1, true);
            var pingReplyTime = new Stopwatch();
            PingReply reply;
            do {
	            pingReplyTime.Start();
	            reply = ping.Send(host, timeout, new byte[] { 0 }, pingOptions);
	            pingReplyTime.Stop();
	            var hostname = string.Empty;
	            if (reply.Address != null) {
            		try {
            			var ipHostInfo = Dns.GetHostEntry(reply.Address);
            			hostname = ipHostInfo.HostName;
	                } catch (SocketException) { /* No host available for that address. */ }
				}

	            // Return out TracertEntry object with all the information about the hop.
	            hops.Add(new TracertEntry()
	            {
		            HopID = pingOptions.Ttl,
		            Address = reply.Address == null ? "N/A" : reply.Address.ToString(),
		            Hostname = hostname,
		            ReplyTime = pingReplyTime.ElapsedMilliseconds,
		            ReplyStatus = reply.Status
	            });

                pingOptions.Ttl++;
                pingReplyTime.Reset();
            }
            while (reply.Status != IPStatus.Success && pingOptions.Ttl <= maxHops);

            return hops;
        }
    }
}
