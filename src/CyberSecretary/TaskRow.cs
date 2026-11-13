using System.Windows.Media;
using CyberSecretary.Core;

namespace CyberSecretary;

public sealed class TaskRow
{
    public TaskItem Item { get; }
    public string StartText => Item.StartAt.ToString("yyyy/MM/dd HH:mm");
    public string EndText => Item.EndAt.ToString("yyyy/MM/dd HH:mm");
    public string ImportanceText => Item.Importance > 0 ? "+" + Item.Importance : Item.Importance.ToString();
    public string StatusText => TaskRules.StatusText(Item.Status);
    public bool CanComplete => Item.Status != TaskState.Completed;
    public string NotesText => string.IsNullOrWhiteSpace(Item.Notes) ? "—" : Item.Notes.ReplaceLineEndings(" ");
    public string DeadlineHint { get; }
    public Brush RowBackground { get; }
    public Brush SelectedRowBackground { get; }
    public Brush HintBrush { get; }
    public Brush ImportanceBrush => Appearance.Brush(Item.Importance >= 1 ? "Accent" : "Muted");
    public Brush StatusBackground { get; }
    public Brush StatusForeground { get; }

    public TaskRow(TaskItem item, WorkCalendar calendar, DateTime now)
    {
        Item = item;
        bool completed = item.Status == TaskState.Completed;
        var urgency = TaskRules.Urgency(item, now, calendar);
        var (rowColor, selectedColor) = completed ? ("Completed", "CompletedSelected") : urgency switch
        {
            TaskUrgency.Critical => ("Critical", "CriticalSelected"),
            TaskUrgency.Soon => ("Soon", "SoonSelected"),
            _ => ("Surface", "Selection")
        };
        RowBackground = Appearance.Brush(rowColor);
        SelectedRowBackground = Appearance.Brush(selectedColor);
        HintBrush = Appearance.Brush(urgency switch { TaskUrgency.Critical => "Danger", TaskUrgency.Soon => "Warning", _ => "Muted" });
        int difference = (item.EndAt.Date - now.Date).Days;
        DeadlineHint = completed ? "已结束" : urgency == TaskUrgency.Unknown ? "法定日历待补全" : difference switch
        {
            < 0 => $"已过截止日 {Math.Abs(difference)} 天",
            0 => "今天截止",
            1 => "明天截止",
            _ => $"{difference} 天后截止"
        };
        (string bg, string fg) = item.Status switch
        {
            TaskState.Completed => ("CompletedBadge", "Muted"),
            TaskState.Overdue => ("OverdueBadge", "Danger"),
            _ => ("Selection", "Ink")
        };
        StatusBackground = Appearance.Brush(bg); StatusForeground = Appearance.Brush(fg);
    }
}
