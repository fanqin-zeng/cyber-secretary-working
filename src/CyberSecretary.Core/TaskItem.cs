namespace CyberSecretary.Core;

public enum TaskState { NotStarted, InProgress, Completed, Overdue }
public enum TaskUrgency { Normal, Soon, Critical, Unknown }

public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateTime StartAt { get; set; } = DateTime.Today.AddHours(9);
    public DateTime EndAt { get; set; } = DateTime.Today.AddHours(18);
    public int Importance { get; set; }
    public TaskState Status { get; set; }
    public string Notes { get; set; } = "";
    public string Collaborators { get; set; } = "";
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
        if (!Enum.IsDefined(item.Status)) return "请选择有效的任务状态。";
        if (item.Notes is null || item.Collaborators is null) return "备注和协作人应为文字，未填写时请留空。";
        if (item.StartAt == default || item.EndAt == default) return "请填写开始和结束时间。";
        return null;
    }

    public static TaskState EffectiveStatus(TaskItem item, DateTime now) =>
        item.Status != TaskState.Completed && item.EndAt < now ? TaskState.Overdue : item.Status;

    public static TaskUrgency Urgency(TaskItem item, DateTime today, WorkCalendar calendar)
    {
        if (item.Status == TaskState.Completed) return TaskUrgency.Normal;
        int? position = calendar.WorkdayPosition(today, item.EndAt);
        if (position == null) return TaskUrgency.Unknown;
        if (item.Importance >= ImportantThreshold && position <= 2) return TaskUrgency.Critical;
        return position < 5 ? TaskUrgency.Soon : TaskUrgency.Normal;
    }

    public static IReadOnlyList<TaskItem> Sort(IEnumerable<TaskItem> tasks) => tasks
        .OrderBy(t => t.Status == TaskState.Completed)
        .ThenBy(t => t.EndAt)
        .ThenBy(t => t.CreatedAt)
        .ThenBy(t => t.Id)
        .ToList();

    public static string StatusText(TaskState status) => status switch
    {
        TaskState.NotStarted => "未开始",
        TaskState.InProgress => "进行中",
        TaskState.Completed => "已结束",
        TaskState.Overdue => "已超期",
        _ => "未开始"
    };
}
