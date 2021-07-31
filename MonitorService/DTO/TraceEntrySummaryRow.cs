using System.Collections.Generic;

namespace MonitorService.DTO
{
	public class TraceEntrySummaryRow
	{
		public string URL { get; set; }
		public int TotalHops { get; set; }
		public int HopTimeouts { get; set; }
		public int SlowHops { get; set; }
		public IReadOnlyList<long> HopReplyTimes { get; set; }
	}
}
