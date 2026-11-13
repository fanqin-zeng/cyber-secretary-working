using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using CyberSecretary.Core;
using Microsoft.Win32;

namespace CyberSecretary;

internal sealed class CalendarDialog : Window
{
    private readonly WorkCalendar _calendar;
    private readonly string _directory;
    private readonly TextBlock _coverage = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, MinHeight = 34, Margin = new Thickness(0, 16, 0, 6) };
    private readonly Button _update = new() { Content = "更新年度日历" };
    private readonly Button _import = new() { Content = "导入日历" };

    public CalendarDialog(WorkCalendar calendar, TaskStore store, Action refresh)
    {
        _calendar = calendar; _directory = Path.Combine(store.DirectoryPath, "calendars");
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "工作日规则"; Width = 615; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(30, 26, 30, 26) };
        panel.Children.Add(new TextBlock { Text = "工作日规则", FontSize = 25, FontWeight = FontWeights.SemiBold });
        AddText(panel, "休息日", 14, 16);
        var choices = new[]
        {
            new { Rule = RestDayRule.Huawei, Name = "华为风格", Detail = "按中国法定节假日和调休计算，每月最后一个周六额外计为工作日。若与调休重合，只计一次。" },
            new { Rule = RestDayRule.TwoDayWeekend, Name = "双休", Detail = "每周六、周日休息。" },
            new { Rule = RestDayRule.OneDayWeekend, Name = "单休", Detail = "每周日休息。" },
            new { Rule = RestDayRule.NoRestDays, Name = "无休", Detail = "每天都是工作日。" }
        };
        var ruleInput = new ComboBox
        {
            Name = "RestDayRuleInput", ItemsSource = choices, DisplayMemberPath = "Name", SelectedValuePath = "Rule",
            SelectedValue = calendar.RestDays, Margin = new Thickness(0, 8, 0, 0)
        };
        System.Windows.Automation.AutomationProperties.SetName(ruleInput, "休息日规则");
        panel.Children.Add(ruleInput);
        var ruleDetail = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(ruleDetail);
        var ruleError = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Appearance.Brush("Danger"), Visibility = Visibility.Collapsed };
        panel.Children.Add(ruleError);
        AddText(panel, "提醒从今天开始计数", 14, 20);
        AddText(panel, "已结束任务统一浅灰色，不参与截止提醒。对未结束任务，今天是工作日时算第 1 个工作日：重要程度 ≥ +1 且 ≤ 2 工作日标红，< 5 工作日标黄，红色优先。已过截止日的未结束任务延续提醒。", 12, 8);
        var holidayPanel = new StackPanel();
        panel.Children.Add(holidayPanel);
        holidayPanel.Children.Add(new Border { Height = 1, Background = Appearance.Brush("Line"), Margin = new Thickness(0, 22, 0, 17) });
        _coverage.FontSize = 14; holidayPanel.Children.Add(_coverage); UpdateCoverage();
        AddText(holidayPanel, "日历内置于应用，离线可用。新年度发布后可更新或导入 JSON 日历；未收录的日期会显示“待补全”。", 11, 9);
        _message.Foreground = Appearance.Brush("Muted"); holidayPanel.Children.Add(_message);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        _update.Style = (Style)FindResource("PrimaryButton"); _update.Margin = new Thickness(0, 0, 10, 0); _update.Click += async (_, _) => await UpdateOnline();
        _import.Click += (_, _) => Import(); buttons.Children.Add(_update); buttons.Children.Add(_import); holidayPanel.Children.Add(buttons);
        var license = new Button { Content = "日历来源与许可", Style = (Style)FindResource("QuietButton"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0, 8, 0, 0), Margin = new Thickness(0, 10, 0, 0) };
        license.Click += (_, _) =>
        {
            var assembly = typeof(WorkCalendar).Assembly;
            using var reader = new StreamReader(assembly.GetManifestResourceStream("CyberSecretary.Core.Calendars.LICENSE")!);
            MessageBox.Show(this, "日历数据：NateScarlet/holiday-cn\nhttps://github.com/NateScarlet/holiday-cn\n依据国务院年度节假日公告，2026 年公告：\nhttps://www.gov.cn/zhengce/content/202511/content_7047090.htm\n\n" + reader.ReadToEnd(), "数据来源与许可", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        holidayPanel.Children.Add(license);
        void UpdateRuleDisplay()
        {
            ruleDetail.Text = choices.Single(choice => choice.Rule == calendar.RestDays).Detail;
            holidayPanel.Visibility = calendar.RestDays == RestDayRule.Huawei ? Visibility.Visible : Visibility.Collapsed;
        }
        ruleInput.SelectionChanged += (_, _) =>
        {
            if (ruleInput.SelectedValue is not RestDayRule rule || rule == calendar.RestDays) return;
            try { store.SaveRestDayRule(rule); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ruleInput.SelectedValue = calendar.RestDays;
                ruleError.Text = "休息日规则保存失败，请检查数据目录是否可写。";
                ruleError.Visibility = Visibility.Visible;
                return;
            }
            calendar.RestDays = rule;
            ruleError.Visibility = Visibility.Collapsed;
            UpdateRuleDisplay();
            refresh();
        };
        UpdateRuleDisplay();
        Content = panel;
    }

    private static void AddText(Panel panel, string text, double size, double top) => panel.Children.Add(new TextBlock
    {
        Text = text, TextWrapping = TextWrapping.Wrap, FontSize = size + 2, Margin = new Thickness(0, top, 0, 0),
        Foreground = Appearance.Brush(size >= 14 ? "Ink" : "Muted"), LineHeight = (size + 2) * 1.7
    });

    private void UpdateCoverage() => _coverage.Text = "已收录法定日历：" + string.Join("、", _calendar.CoveredYears) + " 年";

    private void SaveCalendar(string json)
    {
        var doc = WorkCalendar.Parse(json);
        Directory.CreateDirectory(_directory);
        string target = Path.Combine(_directory, doc.Year + ".json");
        string temporary = target + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, target, true);
        _calendar.Import(json); UpdateCoverage();
    }

    private void Import()
    {
        var picker = new OpenFileDialog { Title = "导入中国法定节假日日历", Filter = "年度日历 (*.json)|*.json", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        try { SaveCalendar(File.ReadAllText(picker.FileName)); _message.Text = "日历已导入，提醒将立即重新计算。"; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { _message.Text = "导入失败：" + ex.Message; }
    }

    private async Task UpdateOnline()
    {
        _update.IsEnabled = _import.IsEnabled = false; _message.Text = "正在获取最新年度日历…";
        int count = 0; var unavailable = new List<int>();
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            for (int year = DateTime.Now.Year; year <= DateTime.Now.Year + 1; year++)
            {
                try
                {
                    string json = await client.GetStringAsync($"https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/{year}.json");
                    var doc = WorkCalendar.Parse(json);
                    if (doc.Year != year) throw new InvalidDataException("年度不匹配。");
                    SaveCalendar(json); count++;
                }
                catch (InvalidDataException) { unavailable.Add(year); }
                catch (HttpRequestException) { unavailable.Add(year); }
            }
            _message.Text = count > 0 ? $"已更新 {count} 个年度。" : "暂时无法获取新日历，请稍后重试或导入文件。";
            if (unavailable.Count > 0) _message.Text += string.Join("、", unavailable) + " 年尚未公布或暂时无法访问。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or TaskCanceledException)
        { _message.Text = "更新未完成，已有日历仍可使用。"; }
        finally { _update.IsEnabled = _import.IsEnabled = true; }
    }
}
