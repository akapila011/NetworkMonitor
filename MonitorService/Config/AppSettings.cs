using System.Collections.Generic;
using System.IO;

namespace MonitorService.Config
{
	public class AppSettings
	{
		/// <summary>
		/// Run the network checks after x seconds
		/// </summary>
		public int Interval { get; set; }

		/// <summary>
		/// The urls to ping/trace
		/// </summary>
		public string[] Urls { get; set; }

		/// <summary>
		/// After how many milliseconds to we consider a timeout
		/// This will indicate an error
		/// </summary>
		public uint HopTimeoutMs { get; set; } = 10000;

		/// <summary>
		/// After how many millseconds is a hop response time
		/// considered slow but not a timeout
		/// </summary>
		public uint HopSlowThresholdMs { get; set; } = 3000;

		/// <summary>
		/// Where we will store data about the network data and email reports
		/// </summary>
		public string DataFolder { get; set; }

		public EmailReport EmailReport { get; set; }

		public bool ShouldSendEmail { get; private set; }

		public bool Validate(out IList<string> warnings, out IList<string> errors)
		{
			warnings = new List<string>();
			errors = new List<string>();
			if (this.Interval < 1)
			{
				errors.Add("Internal value must be greater than 1 second");
			}

			if (this.Urls.Length < 1)
			{
				errors.Add("Must have at least 1 url to run network checks against");
			}

			if (!Directory.Exists(DataFolder))
			{
				errors.Add("Must provide a valid path to a folder to store data");
			}

			if (EmailReport != null)
			{
				if (EmailReport.Frequency.Length < 1 ||
				    string.IsNullOrEmpty(EmailReport.SenderSmtp) ||
				    EmailReport.SenderSmtp.Length < 1 ||
				    string.IsNullOrEmpty(EmailReport.To) ||
				    string.IsNullOrEmpty(EmailReport.SmtpHost))
				{
					warnings.Add(
						"Email frequency/sender fields/to/smtp host/port are invalid so will not send reports");
				}
				else
				{
					// some fields may still be invalid like wrong frequency or password but will only know at execution time
					this.ShouldSendEmail = true;
				}
			}
			else
			{
				warnings.Add("No Email Report configs, no reports will be sent");
			}

			return errors.Count == 0;
		}
	}
}
