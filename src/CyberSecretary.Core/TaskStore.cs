using System.Text.Json;
using System.Text.Json.Serialization;

namespace CyberSecretary.Core;

public sealed class TaskStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "tasks.json");
    public bool RecoveredFromBackup { get; private set; }

    public TaskStore(string directoryPath) => DirectoryPath = directoryPath;

    public List<TaskItem> Load()
    {
        RecoveredFromBackup = false;
        if (!File.Exists(FilePath)) return [];
        try { return Read(FilePath); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            if (!File.Exists(FilePath + ".bak")) throw new InvalidDataException("任务文件损坏，请先保留原文件后恢复备份。", ex);
            var items = Read(FilePath + ".bak");
            // Keep the damaged document before restoring the last good snapshot.
            File.Copy(FilePath, FilePath + ".damaged-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"), false);
            File.Copy(FilePath + ".bak", FilePath, true);
            RecoveredFromBackup = true;
            return items;
        }
    }

    private static List<TaskItem> Read(string path)
    {
        var items = JsonSerializer.Deserialize<List<TaskItem>>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("任务文件为空。 ");
        if (items.Any(t => t is null || TaskRules.Validate(t) != null) || items.Select(t => t.Id).Distinct().Count() != items.Count)
            throw new InvalidDataException("任务文件包含无效或重复的记录。");
        return items;
    }

    public void Save(IEnumerable<TaskItem> tasks)
    {
        var items = tasks.ToList();
        foreach (var item in items)
            if (TaskRules.Validate(item) is { } error) throw new InvalidDataException(error);
        Directory.CreateDirectory(DirectoryPath);
        string temporary = FilePath + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, items, JsonOptions);
                stream.Flush(true);
            }
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak", true);
            else File.Move(temporary, FilePath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
