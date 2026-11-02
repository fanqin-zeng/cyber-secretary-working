using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberSecretary.Core;

namespace CyberSecretary;

public sealed class TaskChart : FrameworkElement
{
    private List<TaskItem> _tasks = [];
    private WorkCalendar _calendar = WorkCalendar.LoadBuiltIn();
    private DateTime _today = DateTime.Today;
    private readonly Dictionary<(int Day, int Importance), List<TaskItem>> _cells = [];
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly ToolTip _tooltip = new() { Placement = PlacementMode.Custom, StaysOpen = true, MaxWidth = 430, Padding = new Thickness(17) };
    private (int Day, int Importance)? _hoverCell;
    private Rect Plot => new(40, 13, Math.Max(1, ActualWidth - 48), Math.Max(1, ActualHeight - 56));
    private static readonly Typeface Face = new(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    public event Action<TaskItem>? TaskChosen;
    public event Action<TaskItem>? TaskEditRequested;

    public TaskChart()
    {
        ClipToBounds = false;
        _tooltip.PlacementTarget = this;
        _tooltip.CustomPopupPlacementCallback = (popup, target, offset) =>
        {
            if (_hoverCell is not { } cell) return [new CustomPopupPlacement(new Point(0, 0), PopupPrimaryAxis.None)];
            var plot = Plot;
            double x = plot.X + (cell.Day + .5) * plot.Width / 31;
            double y = plot.Y + (5 - cell.Importance + .5) * plot.Height / 11;
            x = Math.Clamp(x + 14, 0, Math.Max(0, ActualWidth - popup.Width));
            y = y - popup.Height - 12 >= 0 ? y - popup.Height - 12 : y + 12;
            return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.None)];
        };
        _hoverTimer.Tick += (_, _) => { _hoverTimer.Stop(); ShowCellTooltip(); };
        MouseMove += Hover;
        MouseLeave += (_, _) => { _hoverTimer.Stop(); _tooltip.IsOpen = false; _hoverCell = null; InvalidateVisual(); };
        MouseLeftButtonDown += Click;
        Unloaded += (_, _) => { _hoverTimer.Stop(); _tooltip.IsOpen = false; };
        SizeChanged += (_, _) => InvalidateVisual();
        System.Windows.Automation.AutomationProperties.SetName(this, "未来 0 至 30 天任务栅格，纵轴重要程度 -5 至 +5；任务详情同时可在下方表格查看");
    }

    public void Update(List<TaskItem> tasks, WorkCalendar calendar, DateTime now)
    {
        _tasks = tasks; _calendar = calendar; _today = now.Date;
        _cells.Clear();
        foreach (var task in tasks)
        {
            int day = (task.EndAt.Date - _today).Days;
            if (day is < 0 or > 30) continue;
            var key = (day, task.Importance);
            if (!_cells.TryGetValue(key, out var items)) _cells[key] = items = [];
            items.Add(task);
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var plot = Plot; double cellW = plot.Width / 31, cellH = plot.Height / 11;
        for (int day = 0; day <= 30; day++)
        {
            bool? isWorkday = _calendar.IsWorkday(_today.AddDays(day));
            if (isWorkday != true) dc.DrawRectangle(TaskRow.Brush(isWorkday == null ? "#FAFAF6" : "#F5F6F1"), null, new Rect(plot.X + day * cellW, plot.Y, cellW, plot.Height));
        }
        dc.DrawRectangle(TaskRow.Brush("#EAF2EA"), null, new Rect(plot.X, plot.Y, cellW, plot.Height));
        if (_hoverCell is { } hover)
            dc.DrawRectangle(TaskRow.Brush("#E6EEE3"), null, new Rect(plot.X + hover.Day * cellW, plot.Y + (5 - hover.Importance) * cellH, cellW, cellH));
        var gridPen = new Pen(TaskRow.Brush("#E4E9DF"), .7);
        for (int i = 0; i <= 31; i++)
        {
            double x = plot.X + i * cellW; dc.DrawLine(gridPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
        }
        for (int i = 0; i <= 11; i++)
        {
            double y = plot.Y + i * cellH; dc.DrawLine(gridPen, new Point(plot.X, y), new Point(plot.Right, y));
        }
        for (int importance = 5; importance >= -5; importance--)
        {
            double y = plot.Y + (5 - importance + .5) * cellH;
            Text(dc, importance > 0 ? "+" + importance : importance.ToString(), new Point(plot.X - 18, y - 7), 10, importance == 0 ? "#597866" : "#9BA894", true);
        }
        for (int day = 0; day <= 30; day++)
        {
            double x = plot.X + (day + .5) * cellW;
            Text(dc, day == 0 ? "0d" : day.ToString(), new Point(x, plot.Bottom + 8), 9, day == 0 ? "#467A61" : "#97A28E", true);
            if (day % 5 == 0) Text(dc, _today.AddDays(day).ToString("MM/dd"), new Point(x, plot.Bottom + 24), 9, "#ACB4A3", true);
            if (WorkCalendar.IsLastSaturday(_today.AddDays(day)))
                dc.DrawEllipse(TaskRow.Brush("#C6A76F"), null, new Point(x, plot.Y - 5), 1.7, 1.7);
        }
        foreach (var (key, items) in _cells)
        {
            var item = items.FirstOrDefault(t => t.Status != TaskState.Completed) ?? items[0];
            var center = new Point(plot.X + (key.Day + .5) * cellW, plot.Y + (5 - key.Importance + .5) * cellH);
            var color = TaskRow.Brush(item.Status switch { TaskState.InProgress => "#3E7D6E", TaskState.Completed => "#A1AEA5", TaskState.Overdue => "#CB6D59", _ => "#97A99F" });
            double radius = Math.Min(cellH * .37, items.Count > 1 ? 9.0 : 5.4);
            if (items.Count == 1 && item.Status == TaskState.Completed) dc.DrawEllipse(Brushes.White, new Pen(color, 1.8), center, radius, radius);
            else
            {
                dc.DrawEllipse(color, new Pen(Brushes.White, 1.5), center, radius, radius);
                if (items.Count > 1) Text(dc, items.Count > 99 ? "+" : items.Count.ToString(), new Point(center.X, center.Y - 6), 9, "#FFFFFF", true);
            }
        }
        if (_tasks.Count == 0) Text(dc, "每一个点，都是一件值得安排的事", new Point(plot.X + plot.Width / 2, plot.Y + plot.Height / 2 - 8), 12, "#A1B09A", true);
    }

    private void Text(DrawingContext dc, string text, Point point, double size, string color, bool centered = false)
    {
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, Face, size, TaskRow.Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, centered ? new Point(point.X - formatted.Width / 2, point.Y) : point);
    }

    private (int Day, int Importance)? CellAt(Point position)
    {
        var plot = Plot;
        if (!plot.Contains(position) || position.X >= plot.Right || position.Y >= plot.Bottom) return null;
        return ((int)((position.X - plot.X) / (plot.Width / 31)), 5 - (int)((position.Y - plot.Y) / (plot.Height / 11)));
    }

    private void Hover(object sender, MouseEventArgs e)
    {
        var cell = CellAt(e.GetPosition(this));
        if (cell == _hoverCell) return;
        _hoverTimer.Stop(); _tooltip.IsOpen = false; _hoverCell = cell;
        Cursor = cell.HasValue && _cells.ContainsKey(cell.Value) ? Cursors.Hand : Cursors.Arrow;
        if (cell.HasValue) _hoverTimer.Start();
        InvalidateVisual();
    }

    private void ShowCellTooltip()
    {
        if (_hoverCell is not { } key) return;
        var date = _today.AddDays(key.Day);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = $"{date:yyyy/MM/dd}   ·   重要程度 {(key.Importance > 0 ? "+" : "")}{key.Importance}", FontSize = 11, Foreground = TaskRow.Brush("#637C68") });
        panel.Children.Add(new TextBlock { Text = _calendar.Describe(date), FontSize = 10, Foreground = TaskRow.Brush("#9BA58E"), Margin = new Thickness(0, 4, 0, 5) });
        if (_cells.TryGetValue(key, out var items))
        {
            foreach (var item in items)
            {
                panel.Children.Add(new Border { Height = 1, Background = TaskRow.Brush("#E8ECE2"), Margin = new Thickness(0, 8, 0, 10) });
                panel.Children.Add(new TextBlock { Text = item.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(new TextBlock { Text = TaskRules.StatusText(item.Status) + $"   ·   {item.StartAt:MM/dd HH:mm} — {item.EndAt:MM/dd HH:mm}", FontSize = 10, Foreground = TaskRow.Brush("#7F907A"), Margin = new Thickness(0, 6, 0, 0) });
                if (item.Collaborators.Length > 0) panel.Children.Add(new TextBlock { Text = "协作人：" + item.Collaborators, FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
                if (item.Notes.Length > 0) panel.Children.Add(new TextBlock { Text = item.Notes, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            }
        }
        else panel.Children.Add(new TextBlock { Text = "这个栅格暂无任务", FontSize = 11, Margin = new Thickness(0, 7, 0, 2), Foreground = TaskRow.Brush("#95A08C") });
        _tooltip.Content = new ScrollViewer { Content = panel, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _tooltip.IsOpen = true;
    }

    private void Click(object sender, MouseButtonEventArgs e)
    {
        _tooltip.IsOpen = false;
        if (CellAt(e.GetPosition(this)) is not { } key || !_cells.TryGetValue(key, out var items)) return;
        if (items.Count == 1)
        {
            if (e.ClickCount == 2) TaskEditRequested?.Invoke(items[0]); else TaskChosen?.Invoke(items[0]);
        }
        else
        {
            var menu = new ContextMenu();
            foreach (var item in items)
            {
                var entry = new MenuItem { Header = item.Title + "  ·  " + TaskRules.StatusText(item.Status) };
                entry.Click += (_, _) => TaskChosen?.Invoke(item); menu.Items.Add(entry);
            }
            menu.PlacementTarget = this; menu.Placement = PlacementMode.MousePoint; menu.IsOpen = true;
        }
    }
}
