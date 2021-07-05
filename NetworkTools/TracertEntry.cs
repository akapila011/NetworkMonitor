using System.Net.NetworkInformation;

namespace NetworkTools
{
	public class TracertEntry
	{
		public int HopID { get; set; }
		public string Address { get; set; }
		public string Hostname { get; set; }
		public long ReplyTime { get; set; }
		public IPStatus ReplyStatus { get; set; }

		public bool IsSlowHop(long threshold) => this.ReplyTime >= threshold;
	}
}
