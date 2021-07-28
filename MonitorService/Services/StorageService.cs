using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace MonitorService.Services
{
	public class StorageService
	{
		private readonly string saveDirectoryPath;
		private readonly DateTimeOffset dateTimeOffset;

		public StorageService(string saveDirectoryPath, DateTimeOffset dateTimeOffset)
		{
			this.saveDirectoryPath = saveDirectoryPath;
			this.dateTimeOffset = dateTimeOffset;
		}

		public async Task SaveNetworkEntriesCsv(
			string csvPath,
			IList<(string url, int totalHops, int hopTimeouts, int slowHops, bool success)> entries)
		{

			if (!File.Exists(csvPath)) // need to write header if doesn't exist
			{
				var headers = "time,url,totalHops,hopTimeouts,slowHops,success";
				await File.WriteAllTextAsync(csvPath, $"{headers}\n");
			}

			foreach (var entry in entries)
			{
				var (url, totalHops, hopTimeouts, slowHops, success) = entry;
				var row = $"{this.dateTimeOffset},{url},{totalHops},{hopTimeouts},{slowHops},{success}";
				await File.AppendAllTextAsync(csvPath, $"{row}\n");
			}
		}

		public (string weekPath, string dayPath, string recordFilePath) GetWeekFilePaths()
		{
			// First check year folder exists
			var year = this.dateTimeOffset.Year;
			var yearPath = Directory.CreateDirectory(Path.Combine(this.saveDirectoryPath, year.ToString()));// won't create again if exists

			// Get week folder
			var weekNo = this.GetIso8601WeekOfYear();
			var weekPath = Directory.CreateDirectory(Path.Combine(yearPath.FullName, weekNo.ToString()));

			// Get the Day of Week folder
			var dayOfWeek = this.dateTimeOffset.DayOfWeek;
			var dayPath = Directory.CreateDirectory(Path.Combine(weekPath.FullName, dayOfWeek.ToString()));

			// File Path
			var recordFilePath = Path.Combine(dayPath.FullName, $"{dayOfWeek.ToString()}.csv");

			return (weekPath.FullName, dayPath.FullName, recordFilePath);
		}

		private int GetIso8601WeekOfYear()
		{
			// Seriously cheat.  If its Monday, Tuesday or Wednesday, then it'll
			// be the same week# as whatever Thursday, Friday or Saturday are,
			// and we always get those right
			var time = this.dateTimeOffset.DateTime;
			DayOfWeek day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
			if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
			{
				time = time.AddDays(3);
			}

			// Return the week of our adjusted day
			return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
		}


	}
}
