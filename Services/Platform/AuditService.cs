using Dapper;
using PoultryPro.Models.Entities;

public interface IAuditService
{
    Task LogAsync(string? user, string action, string entity, string? entityId, string? details = null, string? ip = null);
    Task<IEnumerable<AuditLog>> SearchAsync(string? user = null, string? entity = null, DateTime? from = null, DateTime? to = null, int top = 500);
    Task<IEnumerable<string>> GetEntitiesAsync();
}

public class AuditService(IDbConnectionFactory db) : IAuditService
{
    public async Task LogAsync(string? user, string action, string entity, string? entityId, string? details = null, string? ip = null)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"INSERT INTO AuditLogs(UserName,Action,Entity,EntityId,Details,IpAddress)
              VALUES(@user,@action,@entity,@entityId,@details,@ip)",
            new { user, action, entity, entityId, details = details?.Length > 1000 ? details[..1000] : details, ip });
    }

    public async Task<IEnumerable<AuditLog>> SearchAsync(string? user = null, string? entity = null, DateTime? from = null, DateTime? to = null, int top = 500)
    {
        using var conn = db.Create();
        var sql = "SELECT TOP (@top) * FROM AuditLogs WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(user))   sql += " AND UserName=@user";
        if (!string.IsNullOrWhiteSpace(entity)) sql += " AND Entity=@entity";
        if (from.HasValue) sql += " AND CreatedAt>=@from";
        if (to.HasValue)   sql += " AND CreatedAt<DATEADD(DAY,1,@to)";
        sql += " ORDER BY CreatedAt DESC";
        return await conn.QueryAsync<AuditLog>(sql, new { top, user, entity, from, to });
    }

    public async Task<IEnumerable<string>> GetEntitiesAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<string>("SELECT DISTINCT Entity FROM AuditLogs ORDER BY Entity");
    }
}
