using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CyberSecretary.Core;

namespace CyberSecretary;

public partial class MainWindow : Window
{
    private readonly TaskStore _store;
    private readonly List<TaskItem> _tasks;
    private readonly WorkCalendar _calendar;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool _ready;
    private DateTime _lastRefreshDate;

    public MainWindow(TaskStore store, List<TaskItem> tasks)
    {
        _store = store; _tasks = tasks;
        _calendar = WorkCalendar.LoadBuiltIn(Path.Combine(store.DirectoryPath, "calendars"));
        InitializeComponent();
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 34);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 34);
        _ready = true;
        Chart.TaskChosen += item => SelectTask(item.Id);
        Chart.TaskEditRequested += EditTask;
        Loaded += (_, _) => Chart.Focus();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; NewTask(); }
        };
        _clock.Tick += (_, _) =>
        {
            var now = DateTime.Now;
            if (now.Date != _lastRefreshDate || _tasks.Any(t => TaskRules.EffectiveStatus(t, now) != t.Status)) Refresh();
            else
            {
                var visible = TaskTable.Items.Cast<TaskRow>().Select(row => row.Item).ToList();
                if (!visible.SequenceEqual(TaskRules.Sort(visible, now))) Refresh();
            }
        };
        Closed += (_, _) => _clock.Stop();
        _clock.Start(); Refresh();
    }

    private void Refresh()
    {
        if (!_ready) return;
        var now = DateTime.Now;
        _lastRefreshDate = now.Date;
        Guid? selected = (TaskTable.SelectedItem as TaskRow)?.Item.Id;
        bool changed = false;
        foreach (var item in _tasks)
        {
            var status = TaskRules.EffectiveStatus(item, now);
            if (status != item.Status) { item.Status = status; changed = true; }
        }
        if (changed) Save();
        var query = SearchBox.Text.Trim();
        var visible = TaskRules.Sort(_tasks, now).Where(t => query.Length == 0 ||
            t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || t.Notes.Contains(query, StringComparison.OrdinalIgnoreCase) || t.Collaborators.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(t => new TaskRow(t, _calendar, now)).ToList();
        TaskTable.ItemsSource = visible;
        if (selected.HasValue) TaskTable.SelectedItem = visible.FirstOrDefault(t => t.Item.Id == selected);
        EmptyState.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = query.Length > 0 ? "没有找到相关任务" : "从一个小计划开始";
        EmptyDescription.Text = query.Length > 0 ? "试试其他任务名、备注或协作人。" : "点击右上角「新建任务」，安排接下来重要的事。";
        TaskCount.Text = query.Length > 0 ? $"{visible.Count} / {_tasks.Count} 项" : $"{_tasks.Count} 项";
        int completed = _tasks.Count(t => t.Status == TaskState.Completed);
        int overdue = _tasks.Count(t => t.Status == TaskState.Overdue);
        SummaryLabel.Text = $"{_tasks.Count - completed} 项待办  ·  {completed} 项已结束" + (overdue > 0 ? $"  ·  {overdue} 项已超期" : "");
        TodayLabel.Text = now.ToString("yyyy 年 M 月 d 日  dddd", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
        var chartItems = TaskRules.Sort(_tasks).ToList();
        Chart.Update(chartItems, _calendar, now);
    }

    private bool Save()
    {
        try
        {
            _store.Save(_tasks);
            SaveLabel.Text = "所有更改已保存至本地";
            SaveLabel.Foreground = TaskRow.Brush("#94A08F");
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            SaveLabel.Text = "保存失败，请重试";
            SaveLabel.Foreground = TaskRow.Brush("#BC6854");
            MessageBox.Show(this, "无法保存任务，请检查数据目录是否可写。\n\n" + ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void NewTask()
    {
        var dialog = new TaskDialog(null) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _tasks.Add(dialog.Result!);
        if (!Save()) _tasks.Remove(dialog.Result!);
        else { SearchBox.Clear(); ShowNotice("任务已创建"); }
        Refresh(); SelectTask(dialog.Result!.Id);
    }

    private void EditTask(TaskItem item)
    {
        var dialog = new TaskDialog(item) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        int index = _tasks.FindIndex(t => t.Id == item.Id);
        if (index < 0) return;
        _tasks[index] = dialog.Result!;
        if (!Save()) _tasks[index] = item;
        else ShowNotice("任务已更新");
        Refresh(); SelectTask(item.Id);
    }

    private void SelectTask(Guid id)
    {
        var row = TaskTable.Items.Cast<TaskRow>().FirstOrDefault(t => t.Item.Id == id);
        if (row == null && !string.IsNullOrEmpty(SearchBox.Text)) { SearchBox.Clear(); row = TaskTable.Items.Cast<TaskRow>().FirstOrDefault(t => t.Item.Id == id); }
        if (row == null) return;
        TaskTable.SelectedItem = row; TaskTable.ScrollIntoView(row);
    }

    public void ShowNotice(string message, bool warning = false)
    {
        NoticeLabel.Text = message; NoticeLabel.Foreground = TaskRow.Brush(warning ? "#B08E42" : "#728E7A");
    }

    private void NewTaskClick(object sender, RoutedEventArgs e) => NewTask();
    private void EditTaskClick(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is TaskRow row) EditTask(row.Item); }
    private void CompleteTaskClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TaskRow { CanComplete: true } row) return;
        var previousStatus = row.Item.Status;
        row.Item.Status = TaskState.Completed;
        if (!Save()) row.Item.Status = previousStatus;
        else ShowNotice("任务已结束");
        Refresh();
    }

    private void DeleteTaskClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TaskRow row) return;
        var dialog = new ConfirmDialog(row.Item.Title) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        int index = _tasks.IndexOf(row.Item);
        _tasks.Remove(row.Item);
        if (!Save()) _tasks.Insert(index, row.Item); else ShowNotice("任务已删除");
        Refresh();
    }
    private void SearchChanged(object sender, TextChangedEventArgs e) => Refresh();
    private void TableDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        while (source != null && source is not DataGridRow && source is not Button)
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        if (source is DataGridRow gridRow && gridRow.Item is TaskRow row) EditTask(row.Item);
    }
    private void CalendarClick(object sender, RoutedEventArgs e)
    {
        new CalendarDialog(_calendar, Path.Combine(_store.DirectoryPath, "calendars")) { Owner = this }.ShowDialog();
        Refresh();
    }
}
