using System.Text.Json;
using CyberSecretary.Core;

int assertions = 0;
void Check(bool condition, string name) { assertions++; if (!condition) throw new Exception("FAILED: " + name); Console.WriteLine("PASS " + name); }
var calendar = WorkCalendar.LoadBuiltIn();
DateTime D(string text) => DateTime.ParseExact(text, "yyyy-MM-dd", null);
TaskItem Item(string title, string due, int importance = 0, TaskState state = TaskState.NotStarted) => new()
{ Title = title, StartAt = D("2025-01-01"), EndAt = D(due).AddHours(18), Importance = importance, Status = state };

Check(calendar.CoveredYears.SequenceEqual(new[] { 2025, 2026 }), "embedded calendars load");
Check(calendar.IsWorkday(D("2026-10-01")) == false, "National Day weekday is off");
Check(calendar.IsWorkday(D("2026-10-10")) == true, "Saturday make-up workday");
Check(calendar.IsWorkday(D("2026-09-20")) == true, "Sunday make-up workday");
Check(calendar.IsWorkday(D("2026-11-28")) == true, "last Saturday is workday");
Check(calendar.IsWorkday(D("2026-11-21")) == false, "previous Saturday stays off");
Check(calendar.IsWorkday(D("2026-02-28")) == true, "last Saturday overlaps make-up");
Check(calendar.IsWorkday(D("2026-02-21")) == false, "no replacement Saturday for overlapping month");
Check(calendar.WorkdayPosition(D("2026-02-27"), D("2026-03-02")) == 3, "overlapping last Saturday counted once");
Check(calendar.IsWorkday(D("2026-09-26")) == true, "extra last Saturday overrides holiday");
Check(calendar.IsWorkday(D("2027-01-04")) == null, "unpublished calendar is unknown");
Check(calendar.WorkdayPosition(D("2026-11-02"), D("2026-11-02")) == 1, "today is first workday");
Check(calendar.WorkdayPosition(D("2026-11-02"), D("2026-11-03")) == 2, "tomorrow is second workday");
Check(calendar.WorkdayPosition(D("2026-11-27"), D("2026-11-28")) == 2, "extra Saturday affects countdown");
Check(calendar.WorkdayPosition(D("2026-09-30"), D("2026-10-08")) == 2, "count skips National Day holiday");
Check(calendar.WorkdayPosition(D("2026-12-01"), D("2027-02-01")) == 5, "far deadlines do not need unpublished year");
Check(TaskRules.Urgency(Item("a", "2026-11-02", 1), D("2026-11-02"), calendar) == TaskUrgency.Critical, "important due today red");
Check(TaskRules.Urgency(Item("a", "2026-11-03", 1), D("2026-11-02"), calendar) == TaskUrgency.Critical, "important due tomorrow red");
Check(TaskRules.Urgency(Item("a", "2026-11-04", 5), D("2026-11-02"), calendar) == TaskUrgency.Soon, "third workday yellow");
Check(TaskRules.Urgency(Item("a", "2026-11-02", -5), D("2026-11-02"), calendar) == TaskUrgency.Soon, "unimportant due today yellow");
Check(TaskRules.Urgency(Item("a", "2026-11-05", 0), D("2026-11-02"), calendar) == TaskUrgency.Soon, "fourth workday yellow");
Check(TaskRules.Urgency(Item("a", "2026-11-06", 5), D("2026-11-02"), calendar) == TaskUrgency.Normal, "fifth workday excluded from yellow");
Check(TaskRules.Urgency(Item("a", "2026-11-02", 1, TaskState.Completed), D("2026-11-02"), calendar) == TaskUrgency.Normal, "completed important task is excluded from reminders");
Check(TaskRules.Urgency(Item("a", "2026-11-01", 5, TaskState.Completed), D("2026-11-02"), calendar) == TaskUrgency.Normal, "completed past deadline does not become urgent");
Check(TaskRules.Urgency(Item("a", "2027-01-05", 5, TaskState.Completed), D("2027-01-01"), calendar) == TaskUrgency.Normal, "completion overrides a missing calendar");
Check(TaskRules.Urgency(Item("a", "2026-11-01", 1), D("2026-11-02"), calendar) == TaskUrgency.Critical, "past important deadline remains red");
Check(TaskRules.Urgency(Item("a", "2027-01-05", 1), D("2026-12-31"), calendar) == TaskUrgency.Unknown, "unknown near-year calendar does not guess urgency");
var completed = Item("complete", "2026-11-01", 0, TaskState.Completed);
var late = Item("later", "2026-11-04"); var early = Item("earlier", "2026-11-03");
Check(TaskRules.Sort(new[] { completed, late, early }).SequenceEqual(new[] { early, late, completed }), "completion primary, deadline secondary sorting");
Check(TaskRules.EffectiveStatus(early, D("2026-11-04")) == TaskState.Overdue, "expired task becomes overdue");
Check(TaskRules.EffectiveStatus(completed, D("2026-11-04")) == TaskState.Completed, "completion survives expiry");
Check(TaskRules.Validate(Item(" ", "2026-11-03")) != null, "blank title rejected");
Check(TaskRules.Validate(Item("a", "2026-11-03", 6)) != null, "importance range enforced");
var reverse = Item("a", "2026-11-03"); reverse.StartAt = D("2026-11-04");
Check(TaskRules.Validate(reverse) != null, "end before start rejected");
Check(TaskRules.Validate(Item("a", "2026-11-03", -5)) == null, "minimum importance and optional blanks valid");
Check(TaskRules.Validate(Item("a", "2026-11-03", 5)) == null, "maximum importance valid");
var folder = Path.Combine(Path.GetTempPath(), "CyberSecretaryTests-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new TaskStore(folder);
    Check(store.Load().Count == 0, "first launch has no sample tasks");
    early.Notes = "多行备注\n第二行"; early.Collaborators = "小林、小周";
    store.Save([early]);
    var loaded = store.Load().Single();
    Check(loaded.Id == early.Id && loaded.Notes == early.Notes && loaded.Collaborators == early.Collaborators && loaded.EndAt == early.EndAt, "full JSON roundtrip");
    store.Save([early, late]);
    Check(File.Exists(store.FilePath + ".bak"), "atomic save retains backup");
    File.WriteAllText(store.FilePath, "invalid json");
    Check(store.Load().Count == 1 && store.RecoveredFromBackup, "damaged file recovers previous backup");
    Check(Directory.GetFiles(folder, "*.damaged-*").Length == 1, "damaged original preserved");
    store.Save([early, late]); Check(store.Load().Count == 2, "save succeeds after recovery");
    File.WriteAllText(store.FilePath, "null"); File.Delete(store.FilePath + ".bak");
    bool rejected = false; try { store.Load(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected && File.ReadAllText(store.FilePath) == "null", "corrupt data without backup is not overwritten");
}
finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
bool placeholderRejected = false;
try { calendar.Import("{\"year\":2027,\"papers\":[],\"days\":[]}"); } catch (InvalidDataException) { placeholderRejected = true; }
Check(placeholderRejected && !calendar.CoveredYears.Contains(2027), "unpublished placeholder import rejected");
Console.WriteLine($"\n{assertions} assertions passed.");
