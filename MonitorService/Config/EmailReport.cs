using System.Security;

namespace MonitorService.Config
{
	public class EmailReport
	{
		/// <summary>
		/// When to send reports: daily, weekly
		/// </summary>
		public string[] Frequency { get; set; }

		/// <summary>
		/// Account to send the report
		/// </summary>
		public string SenderSmtp { get; set; }

		/// <summary>
		/// Sender account's password
		/// </summary>
		public SecureString SenderPassword { get; set; }

		/// <summary>
		/// The host used for sending emails
		/// </summary>
		public string SmtpHost { get; set; }

		/// <summary>
		/// Smtp port for the host
		/// </summary>
		public uint SmtpPort { get; set; }

		/// <summary>
		/// The email address to be sent to
		/// TODO: need to find out how to send to multiple + cc
		/// </summary>
		public string To { get; set; }
	}
}
