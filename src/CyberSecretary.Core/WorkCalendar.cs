using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CyberSecretary.Core;

public sealed class CalendarDocument
{
    [JsonPropertyName("year")] public int Year { get; set; }
    [JsonPropertyName("papers")] public List<string> Papers { get; set; } = [];
    [JsonPropertyName("days")] public List<CalendarDay> Days { get; set; } = [];
}

public sealed class CalendarDay
{
    [JsonPropertyName("date")] public string Date { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("isOffDay")] public bool IsOffDay { get; set; }
}

public enum RestDayRule { Huawei, TwoDayWeekend, OneDayWeekend, NoRestDays }

public sealed class WorkCalendar
{
    private readonly SortedDictionary<int, CalendarDocument> _documents = [];
    private readonly Dictionary<DateTime, CalendarDay> _exceptions = [];
    public IReadOnlyCollection<int> CoveredYears => _documents.Keys.ToList();
    public RestDayRule RestDays { get; set; }

    public static WorkCalendar LoadBuiltIn(string? userDirectory = null)
    {
        var calendar = new WorkCalendar();
        var assembly = typeof(WorkCalendar).Assembly;
        foreach (string name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".json")))
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            calendar.Import(reader.ReadToEnd());
        }
        if (userDirectory != null && Directory.Exists(userDirectory))
            foreach (string file in Directory.EnumerateFiles(userDirectory, "*.json").Order())
            {
                // A bad optional calendar must not prevent access to personal tasks.
                try { calendar.Import(File.ReadAllText(file)); }
                catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException) { }
            }
        return calendar;
    }

    public static CalendarDocument Parse(string json)
    {
        var doc = JsonSerializer.Deserialize<CalendarDocument>(json) ?? throw new InvalidDataException("日历内容为空。");
        if (doc.Year < 2000 || doc.Year > 2100 || doc.Papers is not { Count: > 0 } || doc.Days is not { Count: > 0 })
            throw new InvalidDataException("该年度的法定节假日尚未公布，或日历文件不完整。");
        foreach (var day in doc.Days)
            if (day is null || !DateTime.TryParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || Math.Abs(date.Year - doc.Year) > 1)
                throw new InvalidDataException("日历日期格式无效。");
        if (doc.Days.Select(d => d.Date).Distinct().Count() != doc.Days.Count) throw new InvalidDataException("日历有重复日期。");
        return doc;
    }

    public void Import(string json)
    {
        var doc = Parse(json);
        _documents[doc.Year] = doc;
        _exceptions.Clear();
        // Next year's announcement may also change dates in December.
        foreach (var document in _documents.Values)
            foreach (var day in document.Days)
                _exceptions[DateTime.ParseExact(day.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture)] = day;
    }

    public static bool IsLastSaturday(DateTime date) => date.DayOfWeek == DayOfWeek.Saturday && date.Day + 7 > DateTime.DaysInMonth(date.Year, date.Month);

    public bool? IsWorkday(DateTime date)
    {
        if (RestDays == RestDayRule.TwoDayWeekend)
            return date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        if (RestDays == RestDayRule.OneDayWeekend) return date.DayOfWeek != DayOfWeek.Sunday;
        if (RestDays == RestDayRule.NoRestDays) return true;

        date = date.Date;
        // The extra last Saturday is a union: an existing make-up workday
        // counts once, with no substitute Saturday elsewhere in the month.
        if (IsLastSaturday(date)) return true;
        if (_exceptions.TryGetValue(date, out var day)) return !day.IsOffDay;
        if (!_documents.ContainsKey(date.Year)) return null;
        return date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }

    public string Describe(DateTime date)
    {
        if (RestDays != RestDayRule.Huawei) return IsWorkday(date) == true ? "工作日" : "休息日";

        bool last = IsLastSaturday(date);
        if (_exceptions.TryGetValue(date.Date, out var day))
        {
            if (!day.IsOffDay) return last ? "调休工作日（与月末周六重合，仅计一次）" : "调休工作日";
            if (last) return "月末周六工作日 · " + day.Name;
            return day.Name + "假期";
        }
        if (last) return "月末周六工作日";
        return IsWorkday(date) switch { true => "工作日", false => "休息日", null => "该年法定日历待补全" };
    }

    // Only the first five workdays matter to the reminder rules; stopping
    // there avoids depending on an unpublished distant year's calendar.
    public int? WorkdayPosition(DateTime today, DateTime dueDate)
    {
        today = today.Date; dueDate = dueDate.Date;
        if (dueDate < today) return 0;
        int count = 0;
        for (var date = today; date <= dueDate; date = date.AddDays(1))
        {
            var workday = IsWorkday(date);
            if (workday == null) return null;
            if (workday == true && ++count >= 5) return 5;
        }
        return Math.Max(1, count);
    }
}
