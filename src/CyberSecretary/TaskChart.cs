using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CyberSecretary.Core;

namespace CyberSecretary;

public sealed class TaskChart : FrameworkElement
{
    public const int VisibleDayCount = 31;
    public int FirstVisibleDay { get; private set; }
    public int LastVisibleDay => FirstVisibleDay + VisibleDayCount - 1;
    public int MaximumDay { get; private set; } = VisibleDayCount - 1;
    private WorkCalendar _calendar = WorkCalendar.LoadBuiltIn();
    private DateTime _today = DateTime.Today;
    private readonly Dictionary<(int Day, int Importance), List<TaskItem>> _cells = [];
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly ToolTip _tooltip = new() { Placement = PlacementMode.Custom, StaysOpen = true, MaxWidth = 480, Padding = new Thickness(17) };
    private (int Day, int Importance)? _hoverCell;
    private HwndSource? _source;
    private double _wheelRemainder;
    private Rect Plot => new(40, 13, Math.Max(1, ActualWidth - 48), Math.Max(1, ActualHeight - 62));
    private static readonly Typeface Face = new(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    public event Action<TaskItem>? TaskChosen;
    public event Action<TaskItem>? TaskEditRequested;

    public TaskChart()
    {
        ClipToBounds = true;
        Focusable = true;
        KeyboardNavigation.SetIsTabStop(this, true);
        _tooltip.PlacementTarget = this;
        _tooltip.CustomPopupPlacementCallback = (popup, target, offset) =>
        {
            if (_hoverCell is not { } cell) return [new CustomPopupPlacement(new Point(0, 0), PopupPrimaryAxis.None)];
            var plot = Plot;
            double x = plot.X + (cell.Day - FirstVisibleDay + .5) * plot.Width / VisibleDayCount;
            double y = plot.Y + (5 - cell.Importance + .5) * plot.Height / 11;
            x = Math.Clamp(x + 14, 0, Math.Max(0, ActualWidth - popup.Width));
            y = y - popup.Height - 12 >= 0 ? y - popup.Height - 12 : y + 12;
            return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.None)];
        };
        _hoverTimer.Tick += (_, _) => { _hoverTimer.Stop(); ShowCellTooltip(); };
        MouseMove += Hover;
        MouseLeave += (_, _) => { CloseTooltip(); InvalidateVisual(); };
        MouseLeftButtonDown += Click;
        Loaded += (_, _) =>
        {
            _source = PresentationSource.FromVisual(this) as HwndSource;
            _source?.AddHook(HandleWindowMessage);
        };
        Unloaded += (_, _) =>
        {
            CloseTooltip();
            _source?.RemoveHook(HandleWindowMessage);
            _source = null;
        };
        SizeChanged += (_, _) => { CloseTooltip(); InvalidateVisual(); };
        System.Windows.Automation.AutomationProperties.SetName(this, "任务时间栅格，支持滚轮横向浏览及左右方向键，重要程度 -5 至 +5");
    }

    public void Update(List<TaskItem> tasks, WorkCalendar calendar, DateTime now)
    {
        DateTime previousStart = _today.AddDays(FirstVisibleDay);
        _calendar = calendar;
        _today = now.Date;
        _cells.Clear();
        MaximumDay = VisibleDayCount - 1;
        foreach (var task in tasks)
        {
            int day = (task.EndAt.Date - _today).Days;
            if (day < 0) continue;
            MaximumDay = Math.Max(MaximumDay, day);
            var key = (day, task.Importance);
            if (!_cells.TryGetValue(key, out var items)) _cells[key] = items = [];
            items.Add(task);
        }
        FirstVisibleDay = Math.Clamp((previousStart - _today).Days, 0, MaximumDay - VisibleDayCount + 1);
        CloseTooltip();
        InvalidateVisual();
    }

    public void ScrollToDay(int firstDay)
    {
        int target = Math.Clamp(firstDay, 0, MaximumDay - VisibleDayCount + 1);
        if (target == FirstVisibleDay) return;
        FirstVisibleDay = target;
        CloseTooltip();
        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        Focus();
        MoveWheel(-e.Delta);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int? target = e.Key switch
        {
            Key.Left => FirstVisibleDay - 1,
            Key.Right => FirstVisibleDay + 1,
            Key.Home => 0,
            Key.End => MaximumDay - VisibleDayCount + 1,
            _ => null
        };
        if (target is not { } day) return;
        ScrollToDay(day);
        e.Handled = true;
    }

    private void MoveWheel(int delta)
    {
        _wheelRemainder += delta / 120.0 * 3;
        int days = (int)_wheelRemainder;
        _wheelRemainder -= days;
        if (days != 0) ScrollToDay(FirstVisibleDay + days);
    }

    private nint HandleWindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        const int MouseHorizontalWheel = 0x020E;
        if (message != MouseHorizontalWheel || !IsVisible) return 0;
        long position = lParam.ToInt64();
        var point = PointFromScreen(new Point((short)(position & 0xffff), (short)((position >> 16) & 0xffff)));
        if (!new Rect(RenderSize).Contains(point)) return 0;
        Focus();
        MoveWheel((short)((wParam.ToInt64() >> 16) & 0xffff));
        handled = true;
        return 0;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var plot = Plot;
        double cellW = plot.Width / VisibleDayCount, cellH = plot.Height / 11;
        for (int column = 0; column < VisibleDayCount; column++)
        {
            int day = FirstVisibleDay + column;
            bool? isWorkday = _calendar.IsWorkday(_today.AddDays(day));
            if (isWorkday != true) dc.DrawRectangle(TaskRow.Brush(isWorkday == null ? "#FAFAF6" : "#F5F6F1"), null, new Rect(plot.X + column * cellW, plot.Y, cellW, plot.Height));
        }
        if (FirstVisibleDay == 0) dc.DrawRectangle(TaskRow.Brush("#EAF2EA"), null, new Rect(plot.X, plot.Y, cellW, plot.Height));
        if (_hoverCell is { } hover)
            dc.DrawRectangle(TaskRow.Brush("#E6EEE3"), null, new Rect(plot.X + (hover.Day - FirstVisibleDay) * cellW, plot.Y + (5 - hover.Importance) * cellH, cellW, cellH));
        var gridPen = new Pen(TaskRow.Brush("#E4E9DF"), .7);
        for (int i = 0; i <= VisibleDayCount; i++)
        {
            double x = plot.X + i * cellW;
            dc.DrawLine(gridPen, new Point(x, plot.Y), new Point(x, plot.Bottom));
        }
        for (int i = 0; i <= 11; i++)
        {
            double y = plot.Y + i * cellH;
            dc.DrawLine(gridPen, new Point(plot.X, y), new Point(plot.Right, y));
        }
        for (int importance = 5; importance >= -5; importance--)
        {
            double y = plot.Y + (5 - importance + .5) * cellH;
            Text(dc, importance > 0 ? "+" + importance : importance.ToString(), new Point(plot.X - 18, y - 9), 12, importance == 0 ? "#597866" : "#8D9C86", true);
        }
        for (int column = 0; column < VisibleDayCount; column++)
        {
            int day = FirstVisibleDay + column;
            double x = plot.X + (column + .5) * cellW;
            Text(dc, day == 0 ? "0d" : day.ToString(), new Point(x, plot.Bottom + 8), 11, day == 0 ? "#467A61" : "#87947E", true);
            if (column % 5 == 0) Text(dc, _today.AddDays(day).ToString("MM/dd"), new Point(x, plot.Bottom + 27), 11, "#98A28E", true);
            if (WorkCalendar.IsLastSaturday(_today.AddDays(day)))
                dc.DrawEllipse(TaskRow.Brush("#C6A76F"), null, new Point(x, plot.Y - 5), 1.7, 1.7);
        }
        foreach (var (key, items) in _cells)
        {
            if (key.Day < FirstVisibleDay || key.Day > LastVisibleDay) continue;
            var item = items.FirstOrDefault(t => t.Status != TaskState.Completed) ?? items[0];
            var center = new Point(plot.X + (key.Day - FirstVisibleDay + .5) * cellW, plot.Y + (5 - key.Importance + .5) * cellH);
            var color = TaskRow.Brush(item.Status switch { TaskState.InProgress => "#3E7D6E", TaskState.Completed => "#A1AEA5", TaskState.Overdue => "#CB6D59", _ => "#97A99F" });
            double radius = Math.Min(cellH * .4, items.Count > 1 ? 10.5 : 6.2);
            if (items.Count == 1 && item.Status == TaskState.Completed) dc.DrawEllipse(Brushes.White, new Pen(color, 1.8), center, radius, radius);
            else
            {
                dc.DrawEllipse(color, new Pen(Brushes.White, 1.5), center, radius, radius);
                if (items.Count > 1) Text(dc, items.Count > 99 ? "+" : items.Count.ToString(), new Point(center.X, center.Y - 8), 11, "#FFFFFF", true);
            }
        }
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
        return (FirstVisibleDay + (int)((position.X - plot.X) / (plot.Width / VisibleDayCount)), 5 - (int)((position.Y - plot.Y) / (plot.Height / 11)));
    }

    private void CloseTooltip()
    {
        _hoverTimer.Stop();
        _tooltip.IsOpen = false;
        _hoverCell = null;
        Cursor = Cursors.Arrow;
    }

    private void Hover(object sender, MouseEventArgs e)
    {
        var cell = CellAt(e.GetPosition(this));
        if (cell == _hoverCell) return;
        CloseTooltip();
        _hoverCell = cell;
        Cursor = cell.HasValue && _cells.ContainsKey(cell.Value) ? Cursors.Hand : Cursors.Arrow;
        if (cell.HasValue) _hoverTimer.Start();
        InvalidateVisual();
    }

    private void ShowCellTooltip()
    {
        if (_hoverCell is not { } key) return;
        var date = _today.AddDays(key.Day);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = $"{date:yyyy/MM/dd}   ·   重要程度 {(key.Importance > 0 ? "+" : "")}{key.Importance}", FontSize = 13, Foreground = TaskRow.Brush("#637C68") });
        panel.Children.Add(new TextBlock { Text = _calendar.Describe(date), FontSize = 12, Foreground = TaskRow.Brush("#9BA58E"), Margin = new Thickness(0, 4, 0, 5) });
        if (_cells.TryGetValue(key, out var items))
        {
            foreach (var item in items)
            {
                panel.Children.Add(new Border { Height = 1, Background = TaskRow.Brush("#E8ECE2"), Margin = new Thickness(0, 8, 0, 10) });
                panel.Children.Add(new TextBlock { Text = item.Title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(new TextBlock { Text = TaskRules.StatusText(item.Status) + $"   ·   {item.StartAt:MM/dd HH:mm} — {item.EndAt:MM/dd HH:mm}", FontSize = 12, Foreground = TaskRow.Brush("#7F907A"), Margin = new Thickness(0, 6, 0, 0) });
                if (item.Collaborators.Length > 0) panel.Children.Add(new TextBlock { Text = "协作人：" + item.Collaborators, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
                if (item.Notes.Length > 0) panel.Children.Add(new TextBlock { Text = item.Notes, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            }
        }
        else panel.Children.Add(new TextBlock { Text = "这个栅格暂无任务", FontSize = 13, Margin = new Thickness(0, 7, 0, 2), Foreground = TaskRow.Brush("#95A08C") });
        _tooltip.Content = new ScrollViewer { Content = panel, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _tooltip.IsOpen = true;
    }

    private void Click(object sender, MouseButtonEventArgs e)
    {
        Focus();
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
                entry.Click += (_, _) => TaskChosen?.Invoke(item);
                menu.Items.Add(entry);
            }
            menu.PlacementTarget = this;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
        }
    }
}
