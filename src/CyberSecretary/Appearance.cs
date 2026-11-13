using System.IO;
using System.Windows;
using System.Windows.Media;

namespace CyberSecretary;

internal sealed record Skin(string Id, string Name, bool Dark, string Canvas, string Surface,
    string Header, string Line, string Ink, string Muted, string Accent, string AccentInk,
    string Selection, bool Flat = false, int Radius = 12);

internal static class Appearance
{
    public static IReadOnlyList<Skin> Skins { get; } = [
        new("sage", "浅绿卡片", false, "#EEF2EE", "#FFFFFF", "#F2F6F2", "#CCD9CF", "#263934", "#566861", "#317264", "#FFFFFF", "#DBEFE7", Radius: 16),
        new("paper", "暖白无框", false, "#FAF8F4", "#FAF8F4", "#F2EFE9", "#DBD5CB", "#302C29", "#655E56", "#276C62", "#FFFFFF", "#DDEEE7", Flat: true, Radius: 0),
        new("mist", "蓝灰", false, "#E8EFF8", "#F8FBFF", "#E4EDF8", "#B9CAE0", "#223650", "#4C627E", "#285EAA", "#FFFFFF", "#DDEAFE", Radius: 10),
        new("sand", "暖砂", false, "#F5EEE5", "#FFFCF7", "#F4E9DB", "#DCCAB7", "#412F27", "#6D5749", "#8A5034", "#FFFFFF", "#F2DFCB"),
        new("lilac", "浅藤紫", false, "#F0ECF8", "#FDFCFF", "#F1EAF9", "#D9CDE8", "#352B46", "#635572", "#67459A", "#FFFFFF", "#EDE0FB"),
        new("graphite", "石墨", true, "#15171A", "#1E2125", "#24282D", "#3C434C", "#F0F2F5", "#B0B8C2", "#A3C7EE", "#142336", "#304156", Flat: true, Radius: 0),
        new("midnight", "午夜蓝", true, "#0F1727", "#162137", "#1B2942", "#394D70", "#E7EFFC", "#A9BAD5", "#8CB7FF", "#122344", "#283F65", Radius: 10),
        new("forest", "墨绿", true, "#111D19", "#192923", "#20332B", "#3B584B", "#E7F3EC", "#A8C3B4", "#8ED4B0", "#122B22", "#284C3D", Radius: 16),
        new("plum", "暗紫", true, "#1A1523", "#251E31", "#302540", "#524166", "#F1EAF8", "#C3B1D3", "#C7A4F4", "#271639", "#47345F"),
        new("ember", "深栗", true, "#211814", "#2B211B", "#362921", "#584439", "#F8EFE8", "#CDB7A8", "#F1B48C", "#372017", "#52392C")
    ];

    public static Skin Load(string directory)
    {
        try
        {
            string id = File.ReadAllText(Path.Combine(directory, "skin.txt")).Trim();
            return Skins.FirstOrDefault(skin => skin.Id == id) ?? Skins[0];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Skins[0];
        }
    }

    public static void Save(string directory, Skin skin)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "skin.txt");
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, skin.Id);
        File.Move(temporary, path, overwrite: true);
    }

    public static void Apply(ResourceDictionary resources, Skin skin)
    {
        bool dark = skin.Dark;
        var colors = new Dictionary<string, string>
        {
            ["Canvas"] = skin.Canvas, ["Surface"] = skin.Surface, ["Header"] = skin.Header,
            ["Line"] = skin.Line, ["Ink"] = skin.Ink, ["Muted"] = skin.Muted, ["Accent"] = skin.Accent,
            ["AccentInk"] = skin.AccentInk, ["Selection"] = skin.Selection,
            ["Critical"] = dark ? "#39282B" : "#FFF0EB",
            ["CriticalSelected"] = dark ? "#51353B" : "#F8DCD5",
            ["Danger"] = dark ? "#FFB6A7" : "#9B4939",
            ["Soon"] = dark ? "#363022" : "#FFF8E3",
            ["SoonSelected"] = dark ? "#51452B" : "#F6EAC6",
            ["Warning"] = dark ? "#F2CF83" : "#786021",
            ["Completed"] = dark ? "#292E36" : "#F1F2F3",
            ["CompletedSelected"] = dark ? "#39404B" : "#E3E7E9",
            ["CompletedBadge"] = dark ? "#363D47" : "#E5E8E9",
            ["OverdueBadge"] = dark ? "#502F33" : "#F8E1D9",
            ["RestColumn"] = dark ? skin.Header : "#F5F6F1",
            ["UnknownColumn"] = dark ? skin.Canvas : "#FAFAF6"
        };
        foreach (var (name, color) in colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            resources[name] = brush;
        }
        resources["PanelRadius"] = new CornerRadius(skin.Radius);
        resources["ChartBorder"] = new Thickness(skin.Flat ? 0 : 1);
        resources["TableBorder"] = skin.Flat ? new Thickness(0, 1, 0, 0) : new Thickness(1);
    }

    public static Brush Brush(string name) => (Brush)Application.Current.FindResource(name);
}
