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
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11, MinHeight = 34, Margin = new Thickness(0, 16, 0, 6) };
    private readonly Button _update = new() { Content = "更新年度日历" };
    private readonly Button _import = new() { Content = "导入日历" };

    public CalendarDialog(WorkCalendar calendar, string directory)
    {
        _calendar = calendar; _directory = directory;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "工作日规则"; Width = 565; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(30, 26, 30, 26) };
        panel.Children.Add(new TextBlock { Text = "工作日规则", FontSize = 23, FontWeight = FontWeights.SemiBold });
        AddText(panel, "中国法定节假日与调休", 14, 22);
        AddText(panel, "正常周一至周五上班，法定假期休息；国务院安排的调休工作日计入。", 12, 8);
        AddText(panel, "每月最后一个周六", 14, 20);
        AddText(panel, "额外计入工作日。若这一天本身已是调休工作日，只计一次，不再追加其他周六；法定假日中的月末周六也按此额外规则计入。", 12, 8);
        AddText(panel, "提醒从今天开始计数", 14, 20);
        AddText(panel, "今天是工作日时算第 1 个工作日。重要程度 ≥ +1，且 ≤ 2 工作日标红；< 5 工作日的所有任务标黄，红色优先。已结束任务同样按日期着色。已过截止日的任务延续提醒。", 12, 8);
        panel.Children.Add(new Border { Height = 1, Background = TaskRow.Brush("#DFE5D9"), Margin = new Thickness(0, 22, 0, 17) });
        _coverage.FontSize = 12; panel.Children.Add(_coverage); UpdateCoverage();
        AddText(panel, "日历内置于应用，离线可用。新年度发布后可更新或导入 JSON 日历；未收录的日期会显示“待补全”，不会猜测调休。", 11, 9);
        _message.Foreground = TaskRow.Brush("#7D917C"); panel.Children.Add(_message);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        _update.Style = (Style)FindResource("PrimaryButton"); _update.Margin = new Thickness(0, 0, 10, 0); _update.Click += async (_, _) => await UpdateOnline();
        _import.Click += (_, _) => Import(); buttons.Children.Add(_update); buttons.Children.Add(_import); panel.Children.Add(buttons);
        var license = new Button { Content = "日历来源与许可", Style = (Style)FindResource("QuietButton"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0, 8, 0, 0), Margin = new Thickness(0, 10, 0, 0) };
        license.Click += (_, _) =>
        {
            var assembly = typeof(WorkCalendar).Assembly;
            using var reader = new StreamReader(assembly.GetManifestResourceStream("CyberSecretary.Core.Calendars.LICENSE")!);
            MessageBox.Show(this, "日历数据：NateScarlet/holiday-cn\nhttps://github.com/NateScarlet/holiday-cn\n依据国务院年度节假日公告，2026 年公告：\nhttps://www.gov.cn/zhengce/content/202511/content_7047090.htm\n\n" + reader.ReadToEnd(), "数据来源与许可", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        panel.Children.Add(license); Content = panel;
    }

    private static void AddText(Panel panel, string text, double size, double top) => panel.Children.Add(new TextBlock
    {
        Text = text, TextWrapping = TextWrapping.Wrap, FontSize = size, Margin = new Thickness(0, top, 0, 0),
        Foreground = TaskRow.Brush(size >= 14 ? "#3D5F49" : "#8A9783"), LineHeight = size * 1.7
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
