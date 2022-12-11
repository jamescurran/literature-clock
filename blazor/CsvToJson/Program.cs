using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using LiteraryClock.Common;

namespace CsvToJson;

class Program
{
	List<Regex> profanity;

	public static async Task Main(string[] args)
	{
		var sol = new Program();
		await sol.Build();
	}
	public async Task Build()
	{
		var sw = new Stopwatch();
		sw.Start();
		profanity = (await File.ReadAllLinesAsync("profanity_patterns.R"))
			.Select(pattern => new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase))
			.ToList();


		using var reader = new StreamReader("litclock_annotated.csv");
		using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			Delimiter = "|", 
			BadDataFound = null, //badDataFound, 
			HasHeaderRecord = false,

		});
		csv.Context.RegisterClassMap<QuoteMap>();
		Console.WriteLine($"Setup complete (Elapsed: {sw.Elapsed}");
		Console.WriteLine("Reading...");
		var list = csv.GetRecords<Quote>()
						.Select(Transform)
						.GroupBy(q=>q.Time)
						.ToList();

		Console.WriteLine($"Reading complete (Elapsed: {sw.Elapsed}");
		Console.WriteLine("Writing...");
		Directory.CreateDirectory("times");
		foreach (var blk in list)
		{
			var filename = blk.Key.Replace(":", "_");
			string pathName = $"times/{filename}.json";
			string jsonString = JsonSerializer.Serialize(blk as IEnumerable<Quote>, new JsonSerializerOptions
			{
				WriteIndented = true,
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			});
			await File.WriteAllTextAsync(pathName, jsonString,Encoding.UTF8);
		}
		Console.WriteLine($"Writing complete (Elapsed: {sw.Elapsed}");
	}
	public Quote Transform(Quote q)
	{
		q.Sfw = (q.Sfw == "nsfw" || profanity.Any(regex => regex.IsMatch(q.Quote_first))) ? "no" : "yes";
		var inx = q.Quote_first?.IndexOf(q.Quote_time_case, StringComparison.InvariantCultureIgnoreCase) ?? -1;
		if (inx != -1)
		{
			q.Quote_last = q.Quote_first.Substring(inx + q.Quote_time_case.Length);
			q.Quote_time_case = q.Quote_first.Substring(inx, q.Quote_time_case.Length);   // Quote_time_case field is always lower case.
			q.Quote_first = q.Quote_first.Substring(0, inx);
		}
		return q;
	}

}






public sealed class QuoteMap : ClassMap<Quote>
{
	public QuoteMap()
	{
		Map(m => m.Time).Index(0);
		Map(m => m.Quote_time_case).Index(1);
		Map(m => m.Quote_first).Index(2);
		Map(m => m.Title).Index(3);
		Map(m => m.Author).Index(4);
		Map(m => m.Sfw).Index(5);
	}
}