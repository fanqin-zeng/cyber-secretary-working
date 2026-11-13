using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberSecretary;
using CyberSecretary.Core;
using Ellipse = System.Windows.Shapes.Ellipse;

internal static class Program
{
    // Exercise the real resources and controls without running product startup:
    // tests must never open the personal data directory or acquire its mutex.
    private sealed class TestApplication : Application
    {
        public TestApplication() => Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/CyberSecretary;component/Styles.xaml")
        });
    }

    private static int _assertions;
    private static Exception? _failure;
    private static void Check(bool condition, string name)
    {
        _assertions++;
        if (!condition) throw new Exception("FAILED: " + name);
        Console.WriteLine("PASS " + name);
    }

    [STAThread]
    private static int Main()
    {
        var app = new TestApplication(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        string folder = Path.Combine(Path.GetTempPath(), "CyberSecretaryUiTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Keep UI assertions independent of whether the real calendar for
            // the runner's current year has been announced or bundled yet.
            var calendars = Path.Combine(folder, "calendars");
            Directory.CreateDirectory(calendars);
            File.WriteAllText(Path.Combine(calendars, DateTime.Today.Year + ".json"), JsonSerializer.Serialize(new
            {
                year = DateTime.Today.Year,
                papers = new[] { "test fixture, not an official calendar" },
                days = new[] { new { date = DateTime.Today.ToString("yyyy-MM-dd"), name = "test", isOffDay = false } }
            }));
            var dialog = new TaskDialog(new TaskItem { Status = TaskState.Overdue });
            dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var save = (Button)dialog.FindName("SaveButton");
                    var error = (TextBlock)dialog.FindName("ErrorLabel");
                    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(dialog.IsVisible && error.Text.Contains("任务名称"), "empty required name keeps dialog open");
                    ((TextBox)dialog.FindName("TitleInput")).Text = "界面集成测试";
                    ((DatePicker)dialog.FindName("StartDate")).SelectedDate = DateTime.Today.AddDays(1);
                    ((DatePicker)dialog.FindName("EndDate")).SelectedDate = DateTime.Today;
                    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(dialog.IsVisible && error.Text.Contains("结束时间"), "reversed date rejected in dialog");
                    ((DatePicker)dialog.FindName("StartDate")).SelectedDate = DateTime.Today;
                    ((TextBox)dialog.FindName("StartTime")).Text = "25:00";
                    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(dialog.IsVisible && error.Text.Contains("开始时刻"), "invalid 24-hour time rejected");
                    ((TextBox)dialog.FindName("StartTime")).Text = "09:00";
                    ((TextBox)dialog.FindName("EndTime")).Text = "18:00";
                    ((ComboBox)dialog.FindName("ImportanceInput")).SelectedValue = 5;
                    ((TextBox)dialog.FindName("NotesInput")).Text = "多行测试\n第二行";
                    ((DatePicker)dialog.FindName("EndDate")).SelectedDate = DateTime.Today.AddDays(1);
                    Check(((FrameworkElement)dialog.FindName("RecurrenceFields")).Visibility == Visibility.Collapsed, "recurrence fields start hidden");
                    ((CheckBox)dialog.FindName("RecurrenceEnabled")).IsChecked = true;
                    var interval = (TextBox)dialog.FindName("RecurrenceInterval");
                    interval.Text = "0";
                    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(dialog.IsVisible && error.Text.Contains("正整数"), "enabled recurrence rejects zero");
                    interval.Text = "2";
                    ((ComboBox)dialog.FindName("RecurrenceUnitInput")).SelectedValue = RecurrenceUnit.Weeks;
                    save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception ex) { _failure = ex; dialog.Close(); }
            }, DispatcherPriority.ContextIdle);
            Check(dialog.ShowDialog() == true, "valid form saves and closes");
            if (_failure != null) throw _failure;
            Check(dialog.Result is { Importance: 5, Status: TaskState.InProgress, Recurrence: { Interval: 2, Unit: RecurrenceUnit.Weeks } },
                "saving a later deadline restores progress and saves recurrence");
            Check(dialog.Result!.Notes == "多行测试\n第二行", "multiline notes retained");

            var edit = new TaskDialog(dialog.Result);
            Check(((TextBox)edit.FindName("TitleInput")).Text == dialog.Result.Title, "edit prefills title");
            Check(((CheckBox)edit.FindName("RecurrenceEnabled")).IsChecked == true &&
                ((TextBox)edit.FindName("RecurrenceInterval")).Text == "2" &&
                Equals(((ComboBox)edit.FindName("RecurrenceUnitInput")).SelectedValue, RecurrenceUnit.Weeks), "edit prefills recurrence");
            Check(((TextBox)edit.FindName("StartTime")).Text == "09:00", "edit prefills complete time");
            edit.Close();

            var store = new TaskStore(folder);
            dialog.Result.Status = TaskState.Completed;
            dialog.Result.EndAt = DateTime.Today.AddHours(18);
            var window = new MainWindow(store, [dialog.Result]);
            window.Show(); window.UpdateLayout();
            var table = (DataGrid)window.FindName("TaskTable");
            Check(table.Items.Count == 1, "main table shows task");
            window.Dispatcher.BeginInvoke(() =>
            {
                var rules = app.Windows.Cast<Window>().Single(open => open.Title == "工作日规则");
                try
                {
                    var ruleInput = Descendants<ComboBox>(rules).Single(input => input.Name == "RestDayRuleInput");
                    Check(ruleInput.Items.Count == 4 && Equals(ruleInput.SelectedValue, RestDayRule.Huawei),
                        "workday dialog offers four rest day rules and defaults to Huawei");
                    ruleInput.SelectedValue = RestDayRule.TwoDayWeekend;
                    Check(store.LoadRestDayRule() == RestDayRule.TwoDayWeekend, "changing the rule saves it immediately");
                    ruleInput.SelectedValue = RestDayRule.Huawei;
                }
                catch (Exception ex) { _failure = ex; }
                finally { rules.Close(); }
            }, DispatcherPriority.ContextIdle);
            Descendants<Button>(window).Single(button => Equals(button.Content, "工作日规则"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (_failure != null) throw _failure;
            var chart = (TaskChart)window.FindName("Chart");
            var calendar = WorkCalendar.LoadBuiltIn(calendars);
            var today = DateTime.Today;
            chart.Update([], calendar, today);
            Check(chart.FirstVisibleDay == 0 && chart.LastVisibleDay == 30 && chart.MaximumDay == 30, "empty chart retains a 30-day horizon");
            var future = new TaskItem { Title = "future task", StartAt = today, EndAt = today.AddDays(90).AddHours(18), Importance = 2 };
            chart.Update([future], calendar, today);
            Check(chart.MaximumDay == 90, "chart horizon includes the furthest task date");
            chart.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(chart), 0, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent });
            Check(chart.FirstVisibleDay == 1, "right arrow advances one day");
            chart.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120) { RoutedEvent = Mouse.MouseWheelEvent });
            Check(chart.FirstVisibleDay == 4, "vertical wheel down moves chart horizontally forward");
            chart.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = Mouse.MouseWheelEvent });
            Check(chart.FirstVisibleDay == 1, "vertical wheel up moves chart horizontally backward");
            chart.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(chart), 0, Key.Left) { RoutedEvent = Keyboard.KeyDownEvent });
            Check(chart.FirstVisibleDay == 0, "left arrow returns toward today");
            chart.ScrollToDay(-100);
            Check(chart.FirstVisibleDay == 0, "chart cannot scroll before today");
            chart.ScrollToDay(int.MaxValue);
            Check(chart.FirstVisibleDay == 60 && chart.LastVisibleDay == 90, "chart stops exactly at the furthest task");
            chart.Update([future], calendar, today);
            Check(chart.FirstVisibleDay == 60, "refresh preserves chart position");
            chart.Update([future], calendar, today.AddDays(1));
            Check(chart.FirstVisibleDay == 59 && chart.MaximumDay == 89, "midnight refresh preserves visible calendar dates");
            chart.Update([], calendar, today);
            Check(chart.FirstVisibleDay == 0 && chart.MaximumDay == 30, "removing the furthest task clamps the viewport");
            chart.Update([future], calendar, today);
            chart.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -20) { RoutedEvent = Mouse.MouseWheelEvent });
            Check(chart.FirstVisibleDay == 0, "high-resolution wheel retains fractional motion");
            chart.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -20) { RoutedEvent = Mouse.MouseWheelEvent });
            Check(chart.FirstVisibleDay == 1, "high-resolution wheel accumulates into a day");
            chart.Update([future.Copy()], calendar, today);
            chart.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(chart), 0, Key.End) { RoutedEvent = Keyboard.KeyDownEvent });
            Check(chart.LastVisibleDay == 90, "End reaches the last task date");
            chart.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(chart), 0, Key.Home) { RoutedEvent = Keyboard.KeyDownEvent });
            Check(chart.FirstVisibleDay == 0, "Home returns to today");
            chart.Update([dialog.Result], calendar, today);
            var row = (TaskRow)table.Items[0];
            var completedColor = (Color)ColorConverter.ConvertFromString("#F1F2F3");
            Check(((SolidColorBrush)row.RowBackground).Color == completedColor, "completed important task uses light gray background");
            Check(row.DeadlineHint == "已结束", "completed task no longer displays a deadline warning");
            var pastCompleted = dialog.Result.Copy(); pastCompleted.EndAt = today.AddDays(-1); pastCompleted.StartAt = today.AddDays(-2);
            Check(((SolidColorBrush)new TaskRow(pastCompleted, calendar, today).RowBackground).Color == completedColor, "completed past task also uses light gray");
            var active = dialog.Result.Copy(); active.Status = TaskState.InProgress;
            Check(((SolidColorBrush)new TaskRow(active, calendar, today).RowBackground).Color == (Color)ColorConverter.ConvertFromString("#FFF0EB"), "unfinished important task retains red reminder");
            var search = (TextBox)window.FindName("SearchBox");
            search.Text = "无匹配关键词";
            Check(table.Items.Count == 0 && ((FrameworkElement)window.FindName("EmptyState")).Visibility == Visibility.Visible, "search displays empty state");
            search.Text = "第二行"; Check(table.Items.Count == 1, "search matches notes");
            search.Clear();
            window.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var newDialog = app.Windows.OfType<TaskDialog>().Single();
                    ((TextBox)newDialog.FindName("TitleInput")).Text = "从主界面创建";
                    ((DatePicker)newDialog.FindName("StartDate")).SelectedDate = DateTime.Today;
                    ((DatePicker)newDialog.FindName("EndDate")).SelectedDate = DateTime.Today.AddDays(14);
                    ((TextBox)newDialog.FindName("StartTime")).Text = "09:00";
                    ((TextBox)newDialog.FindName("EndTime")).Text = "18:00";
                    ((CheckBox)newDialog.FindName("RecurrenceEnabled")).IsChecked = true;
                    ((ComboBox)newDialog.FindName("RecurrenceUnitInput")).SelectedValue = RecurrenceUnit.Weeks;
                    ((Button)newDialog.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception ex) { _failure = ex; foreach (var open in app.Windows.OfType<TaskDialog>().ToList()) open.Close(); }
            }, DispatcherPriority.ContextIdle);
            // A routed click follows the same creation handler used by the real button.
            var newButton = (Button)window.FindName("NewTaskButton");
            newButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (_failure != null) throw _failure;
            Check(table.Items.Count == 2 && store.Load().Count == 2, "new task persists through main interface");
            Check(((TaskRow)table.Items[0]).Item.Title == "从主界面创建", "unfinished task precedes earlier completed task");
            table.SelectedIndex = 0; window.UpdateLayout();
            var cell = Descendants<DataGridCell>(table).FirstOrDefault(c => c.IsSelected);
            Check(cell != null && cell.Background is SolidColorBrush { Color.A: 0 }, "selection preserves task warning background");
            var unfinishedGridRow = (DataGridRow)table.ItemContainerGenerator.ContainerFromIndex(0);
            var selectionDot = Descendants<Ellipse>(unfinishedGridRow).Single();
            Check(unfinishedGridRow.Opacity == 1 && unfinishedGridRow.BorderBrush is SolidColorBrush { Color.A: 0 } &&
                ((SolidColorBrush)unfinishedGridRow.Background).Color == (Color)ColorConverter.ConvertFromString("#DBEFE7") &&
                selectionDot.Visibility == Visibility.Visible,
                "selecting a white row uses mint background and a dot without fading text or showing a bottom border");
            table.SelectedIndex = 1; window.UpdateLayout();
            Check(((SolidColorBrush)unfinishedGridRow.Background).Color == Colors.White &&
                unfinishedGridRow.BorderBrush is SolidColorBrush { Color.A: > 0 } && selectionDot.Visibility == Visibility.Hidden,
                "deselected row restores its original background and separator and hides its dot");
            var completedGridRow = (DataGridRow)table.ItemContainerGenerator.ContainerFromIndex(1);
            Check(((SolidColorBrush)completedGridRow.Background).Color == (Color)ColorConverter.ConvertFromString("#E3E7E9"),
                "selected completed row uses the gray selection tint");
            Check(!Descendants<Button>(completedGridRow).Single(button => Equals(button.Content, "完成")).IsEnabled,
                "completed row disables the complete action");
            var completeButton = Descendants<Button>(unfinishedGridRow).Single(button => Equals(button.Content, "完成"));
            var unfinishedTask = ((TaskRow)unfinishedGridRow.Item).Item.Copy();
            Check(completeButton.IsEnabled, "unfinished row offers the complete action");
            completeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            completeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var updatedRow = table.Items.Cast<TaskRow>().Single(task => task.Item.Id == unfinishedTask.Id);
            Check(updatedRow.StatusText == "已结束" && ((SolidColorBrush)updatedRow.RowBackground).Color == completedColor,
                "completing a task immediately updates its status and background");
            window.Close();
            var savedTasks = store.Load();
            var savedTask = savedTasks.Single(task => task.Id == unfinishedTask.Id);
            Check(savedTask.Status == TaskState.Completed && savedTask.EndAt == unfinishedTask.EndAt,
                "complete action persists the new status and retains the deadline");
            var reopened = new MainWindow(store, savedTasks); reopened.Show(); reopened.UpdateLayout();
            var nextTask = savedTasks.Single(task => task.Id != unfinishedTask.Id && task.Title == unfinishedTask.Title);
            Check(nextTask.StartAt == unfinishedTask.StartAt.AddDays(7) && nextTask.EndAt == unfinishedTask.EndAt.AddDays(7) &&
                nextTask.Status == TaskState.InProgress && nextTask.Recurrence == unfinishedTask.Recurrence &&
                nextTask.Importance == unfinishedTask.Importance && nextTask.Notes == unfinishedTask.Notes,
                "completing a recurring task persists exactly one next occurrence with shifted dates");
            Check(((DataGrid)reopened.FindName("TaskTable")).Items.Count == 3, "completed history and next occurrence reload");
            reopened.Close();
            Console.WriteLine($"\n{_assertions} UI integration assertions passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            foreach (Window window in app.Windows.Cast<Window>().ToList()) window.Close();
            app.Shutdown();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T item) yield return item;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
