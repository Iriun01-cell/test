using Azure;
using Azure.Data.Tables;

namespace MBTM.Api.Tasks;

/// <summary>Table Storage のタスク行。PartitionKey = ユーザーID(oid)、RowKey = タスクID。</summary>
public sealed class TaskEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = TaskStatuses.Todo;
    /// <summary>期限(yyyy-MM-dd)。未設定は空文字。</summary>
    public string DueDate { get; set; } = string.Empty;
    public string Memo { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class TaskStatuses
{
    public const string Todo = "todo";
    public const string Doing = "doing";
    public const string Done = "done";

    public static readonly string[] All = [Todo, Doing, Done];
}

/// <summary>作成・更新リクエスト。</summary>
public sealed class TaskInput
{
    public string? Title { get; set; }
    public string? Status { get; set; }
    public string? DueDate { get; set; }
    public string? Memo { get; set; }
}

/// <summary>レスポンス用。</summary>
public sealed record TaskDto(
    string Id,
    string Title,
    string Status,
    string? DueDate,
    string Memo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static TaskDto From(TaskEntity e) => new(
        e.RowKey,
        e.Title,
        e.Status,
        string.IsNullOrEmpty(e.DueDate) ? null : e.DueDate,
        e.Memo,
        e.CreatedAt,
        e.UpdatedAt);
}

public static class TaskValidation
{
    public const int MaxTitleLength = 200;
    public const int MaxMemoLength = 2000;

    /// <summary>入力を検証する。問題なければ null、あればエラーメッセージを返す。</summary>
    public static string? Validate(TaskInput? input)
    {
        if (input is null) return "リクエスト本文が不正です。";
        if (string.IsNullOrWhiteSpace(input.Title)) return "title は必須です。";
        if (input.Title.Length > MaxTitleLength) return $"title は{MaxTitleLength}文字以内にしてください。";
        if (input.Status is not null && !TaskStatuses.All.Contains(input.Status))
            return $"status は {string.Join(" / ", TaskStatuses.All)} のいずれかにしてください。";
        if (!string.IsNullOrEmpty(input.DueDate) &&
            !DateOnly.TryParseExact(input.DueDate, "yyyy-MM-dd", out _))
            return "dueDate は yyyy-MM-dd 形式にしてください。";
        if (input.Memo is { Length: > MaxMemoLength }) return $"memo は{MaxMemoLength}文字以内にしてください。";
        return null;
    }
}
