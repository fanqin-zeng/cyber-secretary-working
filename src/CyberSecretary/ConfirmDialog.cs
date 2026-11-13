using System.Windows;
using System.Windows.Controls;

namespace CyberSecretary;

internal sealed class ConfirmDialog : Window
{
    public ConfirmDialog(string taskTitle)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "删除任务"; Width = 460; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(26) };
        panel.Children.Add(new TextBlock { Text = "删除这个任务？", FontSize = 23, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = taskTitle, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 15, 0, 7) });
        panel.Children.Add(new TextBlock { Text = "删除后将从表格和栅格中移除。", FontSize = 13, Foreground = Appearance.Brush("Muted") });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 25, 0, 0) };
        buttons.Children.Add(new Button { Content = "取消", IsCancel = true, Margin = new Thickness(0, 0, 10, 0) });
        var delete = new Button { Content = "删除任务", Background = Appearance.Brush("OverdueBadge"), Foreground = Appearance.Brush("Danger"), BorderThickness = new Thickness(0) };
        delete.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(delete); panel.Children.Add(buttons); Content = panel;
    }
}
