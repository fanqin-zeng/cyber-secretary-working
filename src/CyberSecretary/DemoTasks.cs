using CyberSecretary.Core;

namespace CyberSecretary;

internal static class DemoTasks
{
    public static List<TaskItem> Create()
    {
        var today = DateTime.Today;
        TaskItem Make(string title, int days, int importance, TaskState status, string notes = "") => new()
        {
            Title = title, StartAt = today.AddDays(-2).AddHours(9), EndAt = today.AddDays(days).AddHours(18),
            Importance = importance, Status = status, Notes = notes
        };
        return [
            Make("整理本周计划", 0, 4, TaskState.InProgress, "梳理本周重点，预留专注时间。"),
            Make("完成项目方案", 1, 5, TaskState.InProgress, "补充里程碑与交付清单。"),
            Make("阅读与笔记", 2, 0, TaskState.InProgress, "每天留出半小时阅读。"),
            Make("更新个人作品集", 4, 3, TaskState.InProgress),
            Make("安排周末运动", 5, -2, TaskState.InProgress),
            Make("月度复盘", 12, 4, TaskState.InProgress),
            Make("规划下阶段学习", 19, 2, TaskState.InProgress),
            Make("备份资料", 26, -1, TaskState.InProgress),
            Make("确认下月行程", 30, 1, TaskState.InProgress),
            Make("下阶段项目计划", 45, 5, TaskState.InProgress, "用于演示超过 30 天的横向时间栅格。"),
            Make("季度复盘", 90, 3, TaskState.InProgress),
            Make("整理本周计划 · 资料收集", 0, 4, TaskState.Completed, "与同一坐标任务合并显示数量。"),
            Make("归档上月文件", -1, -1, TaskState.Completed)
        ];
    }
}
