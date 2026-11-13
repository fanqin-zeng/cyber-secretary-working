namespace CyberSecretary.Core;

public enum TaskState { InProgress = 1, Completed = 2, Overdue = 3 }
public enum TaskUrgency { Normal, Soon, Critical, Unknown }
public enum RecurrenceUnit { Minutes, Hours, Days, Weeks, Months, Years }

public sealed record TaskRecurrence(int Interval, RecurrenceUnit Unit)
{
    public DateTime Advance(DateTime date) => Unit switch
    {
        RecurrenceUnit.Minutes => date.AddMinutes(Interval),
        RecurrenceUnit.Hours => date.AddHours(Interval),
        RecurrenceUnit.Days => date.AddDays(Interval),
        RecurrenceUnit.Weeks => date.AddDays(7L * Interval),
        RecurrenceUnit.Months => date.AddMonths(Interval),
        RecurrenceUnit.Years => date.AddYears(Interval),
        _ => throw new ArgumentOutOfRangeException(nameof(Unit))
    };

    public (DateTime Start, DateTime End) AdvanceWindow(DateTime start, DateTime end)
    {
        DateTime nextStart = Advance(start);
        DateTime nextEnd = Advance(end);
        // A short month can map different dates onto the same day in reverse time order.
        return (nextStart, nextEnd < nextStart ? nextStart.Add(end - start) : nextEnd);
    }
}

public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateTime StartAt { get; set; } = DateTime.Today.AddHours(9);
    public DateTime EndAt { get; set; } = DateTime.Today.AddHours(18);
    public int Importance { get; set; }
    public TaskState Status { get; set; } = TaskState.InProgress;
    public string Notes { get; set; } = "";
    public TaskRecurrence? Recurrence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public TaskItem Copy() => (TaskItem)MemberwiseClone();
}

public static class TaskRules
{
    public const int ImportantThreshold = 1;

    public static string? Validate(TaskItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Title)) return "请输入任务名称。";
        if (item.Title.Length > 120) return "任务名称最多 120 个字符。";
        if (item.Importance < -5 || item.Importance > 5) return "重要程度必须是 -5 至 +5 的整数。";
        if (item.EndAt < item.StartAt) return "结束时间不能早于开始时间。";
        if (!Enum.IsDefined(item.Status)) return "任务状态无效。";
        if (item.Notes is null) return "备注应为文字，未填写时请留空。";
        if (item.Recurrence is { } recurrence && (recurrence.Interval <= 0 || !Enum.IsDefined(recurrence.Unit)))
            return "周期须使用正整数和有效的单位。";
        if (item.StartAt == default || item.EndAt == default) return "请填写开始和结束时间。";
        return null;
    }

    public static TaskState EffectiveStatus(TaskItem item, DateTime now) =>
        item.Status == TaskState.Completed ? TaskState.Completed :
        item.EndAt < now ? TaskState.Overdue : TaskState.InProgress;

    public static TaskItem? NextOccurrence(TaskItem item, DateTime now)
    {
        if (item.Recurrence is not { } recurrence) return null;
        var next = item.Copy();
        next.Id = Guid.NewGuid();
        next.CreatedAt = now;
        (next.StartAt, next.EndAt) = recurrence.AdvanceWindow(item.StartAt, item.EndAt);
        next.Status = TaskState.InProgress;
        next.Status = EffectiveStatus(next, now);
        return next;
    }

    public static TaskUrgency Urgency(TaskItem item, DateTime today, WorkCalendar calendar)
    {
        if (item.Status == TaskState.Completed) return TaskUrgency.Normal;
        int? position = calendar.WorkdayPosition(today, item.EndAt);
        if (position == null) return TaskUrgency.Unknown;
        if (item.Importance >= ImportantThreshold && position <= 2) return TaskUrgency.Critical;
        return position < 5 ? TaskUrgency.Soon : TaskUrgency.Normal;
    }

    public static IReadOnlyList<TaskItem> Sort(IEnumerable<TaskItem> tasks, DateTime? completedByDistanceFrom = null) => tasks
        .OrderBy(t => t.Status == TaskState.Completed)
        .ThenBy(t => t.Status == TaskState.Completed && completedByDistanceFrom is { } reference
            ? Math.Abs((t.EndAt - reference).Ticks)
            : 0L)
        .ThenBy(t => t.EndAt)
        .ThenBy(t => t.CreatedAt)
        .ThenBy(t => t.Id)
        .ToList();

    public static string StatusText(TaskState status) => status switch
    {
        TaskState.InProgress => "进行中",
        TaskState.Completed => "已结束",
        TaskState.Overdue => "已超期",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
