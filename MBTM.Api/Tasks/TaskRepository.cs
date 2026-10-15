using Azure;
using Azure.Data.Tables;

namespace MBTM.Api.Tasks;

/// <summary>Table Storage の Tasks テーブルに対するタスク操作。すべてユーザーID(PartitionKey)で分離する。</summary>
public sealed class TaskRepository
{
    private const string TableName = "Tasks";

    private readonly TableClient _table;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public TaskRepository()
    {
        var connectionString = Environment.GetEnvironmentVariable("TableStorageConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("設定 TableStorageConnectionString が未設定です。");
        }

        _table = new TableClient(connectionString, TableName);
    }

    public async Task<IReadOnlyList<TaskEntity>> ListAsync(string userId, CancellationToken ct)
    {
        await EnsureTableAsync(ct);
        var items = new List<TaskEntity>();
        await foreach (var e in _table.QueryAsync<TaskEntity>(t => t.PartitionKey == userId, cancellationToken: ct))
        {
            items.Add(e);
        }
        return items.OrderBy(t => t.CreatedAt).ToList();
    }

    public async Task<TaskEntity?> GetAsync(string userId, string id, CancellationToken ct)
    {
        await EnsureTableAsync(ct);
        var result = await _table.GetEntityIfExistsAsync<TaskEntity>(userId, id, cancellationToken: ct);
        return result.HasValue ? result.Value : null;
    }

    public async Task<TaskEntity> CreateAsync(string userId, TaskInput input, CancellationToken ct)
    {
        await EnsureTableAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var entity = new TaskEntity
        {
            PartitionKey = userId,
            RowKey = Guid.NewGuid().ToString("N"),
            Title = input.Title!.Trim(),
            Status = input.Status ?? TaskStatuses.Todo,
            DueDate = input.DueDate ?? string.Empty,
            Memo = input.Memo ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _table.AddEntityAsync(entity, ct);
        return entity;
    }

    /// <summary>更新する。対象が存在しない場合は null。</summary>
    public async Task<TaskEntity?> UpdateAsync(string userId, string id, TaskInput input, CancellationToken ct)
    {
        var entity = await GetAsync(userId, id, ct);
        if (entity is null) return null;

        entity.Title = input.Title!.Trim();
        entity.Status = input.Status ?? entity.Status;
        entity.DueDate = input.DueDate ?? string.Empty;
        entity.Memo = input.Memo ?? string.Empty;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            // 取得時の ETag で更新し、同時更新があれば失敗させる
            await _table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        return entity;
    }

    /// <summary>削除する。対象が存在しない場合は false。</summary>
    public async Task<bool> DeleteAsync(string userId, string id, CancellationToken ct)
    {
        await EnsureTableAsync(ct);
        try
        {
            var response = await _table.DeleteEntityAsync(userId, id, ETag.All, ct);
            // SDKは存在しない行の削除で例外を投げず404を返す場合があるため、ステータスも確認する
            return response.Status != 404;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (!_initialized)
            {
                await _table.CreateIfNotExistsAsync(ct);
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }
}
