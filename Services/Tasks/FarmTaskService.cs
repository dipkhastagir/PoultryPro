using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

/// <summary>Operational task board (dbo.FarmTasks).</summary>
public interface IFarmTaskService
{
    Task<IEnumerable<FarmTask>> GetAllAsync(int? farmId = null, string? priority = null, int? assigneeId = null, string? category = null);
    Task<FarmTask?> GetByIdAsync(int id);
    Task<int> CreateAsync(TaskFormVm vm, string? createdBy);
    Task UpdateAsync(TaskFormVm vm);
    Task MoveAsync(int id, string status);
    Task DeleteAsync(int id);
    Task<IEnumerable<TaskBatchOption>> GetActiveBatchesAsync();
}

public class FarmTaskService(IDbConnectionFactory db) : IFarmTaskService
{
    private const string Select = @"SELECT t.*, f.Name AS FarmName, b.BatchCode, e.FullName AS AssigneeName
                                    FROM FarmTasks t
                                    LEFT JOIN Farms f ON f.Id = t.FarmId
                                    LEFT JOIN Batches b ON b.Id = t.BatchId
                                    LEFT JOIN Employees e ON e.Id = t.AssignedToEmployeeId";

    public async Task<IEnumerable<FarmTask>> GetAllAsync(int? farmId = null, string? priority = null, int? assigneeId = null, string? category = null)
    {
        using var conn = db.Create();
        var sql = Select + " WHERE (t.Status <> 'Done' OR t.CompletedAt IS NULL OR t.CompletedAt >= DATEADD(DAY, -60, GETDATE()))";
        if (farmId.HasValue) sql += " AND t.FarmId = @farmId";
        if (!string.IsNullOrWhiteSpace(priority)) sql += " AND t.Priority = @priority";
        if (assigneeId.HasValue) sql += " AND t.AssignedToEmployeeId = @assigneeId";
        if (!string.IsNullOrWhiteSpace(category)) sql += " AND t.Category = @category";
        sql += " ORDER BY CASE WHEN t.DueDate IS NULL THEN 1 ELSE 0 END, t.DueDate, t.CreatedAt DESC";
        return await conn.QueryAsync<FarmTask>(sql, new { farmId, priority, assigneeId, category });
    }

    public async Task<FarmTask?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<FarmTask>(Select + " WHERE t.Id = @id", new { id });
    }

    private static object Params(TaskFormVm vm, string? createdBy = null) => new
    {
        vm.Id,
        Title = vm.Title.Trim(),
        Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
        vm.FarmId,
        vm.BatchId,
        vm.AssignedToEmployeeId,
        Category = FarmTask.Categories.Contains(vm.Category) ? vm.Category : "General",
        Priority = FarmTask.Priorities.Contains(vm.Priority) ? vm.Priority : "Medium",
        Status = FarmTask.Statuses.Contains(vm.Status) ? vm.Status : "Open",
        DueDate = vm.DueDate?.Date,
        CreatedBy = createdBy
    };

    public async Task<int> CreateAsync(TaskFormVm vm, string? createdBy)
    {
        using var conn = db.Create();
        await NormaliseBatchAsync(conn, vm);
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO FarmTasks(Title, Description, FarmId, BatchId, AssignedToEmployeeId, Category, Priority, Status, DueDate, CompletedAt, CreatedBy)
              VALUES(@Title, @Description, @FarmId, @BatchId, @AssignedToEmployeeId, @Category, @Priority, @Status, @DueDate,
                     CASE WHEN @Status = 'Done' THEN GETDATE() ELSE NULL END, @CreatedBy);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", Params(vm, createdBy));
    }

    public async Task UpdateAsync(TaskFormVm vm)
    {
        using var conn = db.Create();
        await NormaliseBatchAsync(conn, vm);
        await conn.ExecuteAsync(
            @"UPDATE FarmTasks SET Title=@Title, Description=@Description, FarmId=@FarmId, BatchId=@BatchId,
                     AssignedToEmployeeId=@AssignedToEmployeeId, Category=@Category, Priority=@Priority, DueDate=@DueDate,
                     CompletedAt = CASE WHEN @Status='Done' THEN ISNULL(CompletedAt, GETDATE()) ELSE NULL END,
                     Status=@Status
              WHERE Id=@Id", Params(vm));
    }

    /// <summary>A batch always belongs to one farm: fill the farm from the batch and drop a batch from another farm.</summary>
    private static async Task NormaliseBatchAsync(System.Data.IDbConnection conn, TaskFormVm vm)
    {
        if (!vm.BatchId.HasValue) return;
        var batchFarm = await conn.ExecuteScalarAsync<int?>("SELECT FarmId FROM Batches WHERE Id=@id", new { id = vm.BatchId });
        if (batchFarm == null) { vm.BatchId = null; return; }
        if (!vm.FarmId.HasValue) vm.FarmId = batchFarm;
        else if (vm.FarmId != batchFarm) vm.BatchId = null;
    }

    public async Task MoveAsync(int id, string status)
    {
        if (!FarmTask.Statuses.Contains(status)) return;
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE FarmTasks SET Status=@status,
                     CompletedAt = CASE WHEN @status='Done' THEN ISNULL(CompletedAt, GETDATE()) ELSE NULL END
              WHERE Id=@id", new { id, status });
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("DELETE FROM FarmTasks WHERE Id=@id", new { id });
    }

    public async Task<IEnumerable<TaskBatchOption>> GetActiveBatchesAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<TaskBatchOption>(
            "SELECT Id, BatchCode, FarmId, Breed FROM Batches WHERE IsActive = 1 ORDER BY BatchCode");
    }
}
