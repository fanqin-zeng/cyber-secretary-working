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
    public string CollaboratorText => string.IsNullOrWhiteSpace(Item.Collaborators) ? "—" : Item.Collaborators;
    public string NotesText => string.IsNullOrWhiteSpace(Item.Notes) ? "—" : Item.Notes.ReplaceLineEndings(" ");
    public string DeadlineHint { get; }
    public Brush RowBackground { get; }
    public Brush SelectedRowBackground { get; }
    public Brush HintBrush { get; }
    public Brush ImportanceBrush => Brush(Item.Importance >= 1 ? "#477565" : "#87978B");
    public Brush StatusBackground { get; }
    public Brush StatusForeground { get; }

    public TaskRow(TaskItem item, WorkCalendar calendar, DateTime now)
    {
        Item = item;
        bool completed = item.Status == TaskState.Completed;
        var urgency = TaskRules.Urgency(item, now, calendar);
        var (rowColor, selectedColor) = completed ? ("#F1F2F3", "#E3E7E9") : urgency switch
        {
            TaskUrgency.Critical => ("#FFF0EB", "#F8DCD5"),
            TaskUrgency.Soon => ("#FFF8E3", "#F6EAC6"),
            _ => ("#FFFFFF", "#DBEFE7")
        };
        RowBackground = Brush(rowColor);
        SelectedRowBackground = Brush(selectedColor);
        HintBrush = Brush(completed ? "#929A96" : urgency switch { TaskUrgency.Critical => "#BC6854", TaskUrgency.Soon => "#AC9454", _ => "#99A38F" });
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
            TaskState.InProgress => ("#E1EEE5", "#437964"),
            TaskState.Completed => ("#E5E8E9", "#7C8584"),
            TaskState.Overdue => ("#F8E1D9", "#B26C56"),
            _ => ("#EBEEE7", "#7E8D78")
        };
        StatusBackground = Brush(bg); StatusForeground = Brush(fg);
    }

    internal static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush;
    }
}
