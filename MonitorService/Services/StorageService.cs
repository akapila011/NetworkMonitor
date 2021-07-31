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

		public async Task SaveNetworkTraceEntriesCsv(
			string csvPath,
			IList<(string url, int totalHops, int hopTimeouts, int slowHops, bool success)> entries)
		{
			string headers = null;
			if (!File.Exists(csvPath)) // need to write header if doesn't exist
			{
				headers = "time,url,totalHops,hopTimeouts,slowHops,success";
			}

			var rows = new string[entries.Count];
			var count = 0;
			foreach (var entry in entries)
			{
				var (url, totalHops, hopTimeouts, slowHops, success) = entry;
				var row = $"{this.dateTimeOffset},{url},{totalHops},{hopTimeouts},{slowHops},{success}";
				rows[count] = row;
			}

			await SaveCsvFile(csvPath, rows, headers: headers);
		}

		private async Task SaveCsvFile(string csvPath, string[] dataRows, string headers = null)
		{
			if (dataRows?.Length == 0) throw new ArgumentException("Cannot write a csv file with no lines");
			if (!string.IsNullOrEmpty(headers))
			{
				await File.WriteAllTextAsync(csvPath, $"{headers}\n");
			}

			foreach (var row in dataRows)
			{
				await File.AppendAllTextAsync(csvPath, $"{row}\n");
			}
		}

		public (string weekPath, string dayPath, int year, int weekNo, string dayOfWeek) GetWeekFilePaths()
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

			return (weekPath.FullName, dayPath.FullName, year, weekNo, dayOfWeek.ToString());
		}

		/// <summary>
		/// Wrapper around C# Path.Combine to avoid unnecesarry Path imports in calling files
		/// </summary>
		/// <param name="basePath">e.g. C:\Users</param>
		/// <param name="suffixPath">example.csv</param>
		/// <returns>C:\Users\example.csv (also works to combine directory paths)</returns>
		public static string Combine(string basePath, string suffixPath)
		{
			return Path.Combine(basePath, suffixPath);
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
