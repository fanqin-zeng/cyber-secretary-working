using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CyberSecretary.Core;

namespace CyberSecretary;

public partial class TaskDialog : Window
{
    private readonly TaskItem _draft;
    public TaskItem? Result { get; private set; }

    public TaskDialog(TaskItem? existing)
    {
        InitializeComponent();
        var now = DateTime.Now;
        _draft = existing?.Copy() ?? new TaskItem
        {
            StartAt = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0),
            EndAt = now.Hour < 18 ? now.Date.AddHours(18) : now.Date.AddDays(1).AddHours(18)
        };
        if (existing != null) { Title = Heading.Text = "编辑任务"; SaveButton.Content = "保存修改"; }
        TitleInput.Text = _draft.Title; StartDate.SelectedDate = _draft.StartAt.Date; EndDate.SelectedDate = _draft.EndAt.Date;
        StartTime.Text = _draft.StartAt.ToString("HH:mm"); EndTime.Text = _draft.EndAt.ToString("HH:mm");
        ImportanceInput.ItemsSource = Enumerable.Range(-5, 11).Reverse().Select(n => new { Text = n > 0 ? "+" + n : n.ToString(), Value = n });
        ImportanceInput.SelectedValue = _draft.Importance;
        StatusInput.ItemsSource = Enum.GetValues<TaskState>().Select(s => new { Text = TaskRules.StatusText(s), Value = s });
        StatusInput.SelectedValue = _draft.Status;
        NotesInput.Text = _draft.Notes; CollaboratorInput.Text = _draft.Collaborators;
        StartDate.DateValidationError += DateInvalid; EndDate.DateValidationError += DateInvalid;
        Loaded += (_, _) => { TitleInput.Focus(); TitleInput.CaretIndex = TitleInput.Text.Length; };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; SaveClick(this, new RoutedEventArgs()); } };
    }

    private void DateInvalid(object? sender, DatePickerDateValidationErrorEventArgs e) { e.ThrowException = false; ErrorLabel.Text = "请输入有效日期，例如 2026/11/02。"; }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        ErrorLabel.Text = "";
        if (!TryDate(StartDate, out var startDate)) { ErrorLabel.Text = "请填写有效的开始日期。"; StartDate.Focus(); return; }
        if (!TryDate(EndDate, out var endDate)) { ErrorLabel.Text = "请填写有效的结束日期。"; EndDate.Focus(); return; }
        if (!TimeOnly.TryParseExact(StartTime.Text.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
        { ErrorLabel.Text = "开始时刻请使用 24 小时制，例如 09:00。"; StartTime.Focus(); return; }
        if (!TimeOnly.TryParseExact(EndTime.Text.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var endTime))
        { ErrorLabel.Text = "结束时刻请使用 24 小时制，例如 18:00。"; EndTime.Focus(); return; }
        if (ImportanceInput.SelectedValue is not int importance || StatusInput.SelectedValue is not TaskState status)
        { ErrorLabel.Text = "请选择重要程度和任务状态。"; return; }
        _draft.Title = TitleInput.Text.Trim(); _draft.StartAt = startDate.Date.Add(startTime.ToTimeSpan()); _draft.EndAt = endDate.Date.Add(endTime.ToTimeSpan());
        _draft.Importance = importance; _draft.Status = status; _draft.Notes = NotesInput.Text.Trim(); _draft.Collaborators = CollaboratorInput.Text.Trim();
        if (TaskRules.Validate(_draft) is { } error) { ErrorLabel.Text = error; if (_draft.Title.Length == 0) TitleInput.Focus(); return; }
        _draft.Status = TaskRules.EffectiveStatus(_draft, DateTime.Now);
        Result = _draft; DialogResult = true;
    }

    private static bool TryDate(DatePicker picker, out DateTime date) =>
        DateTime.TryParse(picker.Text, CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out date);
}
