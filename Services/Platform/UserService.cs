using Dapper;
using PoultryPro.Infrastructure;
using PoultryPro.Models.Entities;

/// <summary>Login accounts for the web application (dbo.AppUsers).</summary>
public interface IUserService
{
    Task<AppUser?> AuthenticateAsync(string username, string password);
    Task<IEnumerable<AppUser>> GetAllAsync();
    Task<AppUser?> GetByIdAsync(int id);
    Task<bool> UsernameExistsAsync(string username, int exceptId = 0);
    Task<int> CreateAsync(AppUser user, string password);
    Task UpdateAsync(AppUser user);
    Task SetPasswordAsync(int id, string newPassword);
    Task ToggleActiveAsync(int id);
    Task UpdateProfileAsync(int id, string fullName, string? email, string? phone);
}

public class UserService(IDbConnectionFactory db) : IUserService
{
    private const string Select = @"SELECT u.*, fo.FullName AS FieldOfficerName FROM AppUsers u
                                    LEFT JOIN FieldOfficers fo ON fo.Id = u.FieldOfficerId";

    public async Task<AppUser?> AuthenticateAsync(string username, string password)
    {
        using var conn = db.Create();
        var user = await conn.QuerySingleOrDefaultAsync<AppUser>(Select + " WHERE u.Username=@username AND u.IsActive=1", new { username });
        if (user == null || !PasswordHasher.Verify(password, user.PasswordHash)) return null;

        // transparently upgrade legacy SHA-256 hashes to PBKDF2
        if (!user.PasswordHash.StartsWith("pbkdf2$"))
            await conn.ExecuteAsync("UPDATE AppUsers SET PasswordHash=@h WHERE Id=@id", new { h = PasswordHasher.Hash(password), id = user.Id });
        await conn.ExecuteAsync("UPDATE AppUsers SET LastLoginAt=GETDATE() WHERE Id=@id", new { id = user.Id });
        return user;
    }

    public async Task<IEnumerable<AppUser>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<AppUser>(Select + " ORDER BY u.IsActive DESC, u.Role, u.FullName");
    }

    public async Task<AppUser?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<AppUser>(Select + " WHERE u.Id=@id", new { id });
    }

    public async Task<bool> UsernameExistsAsync(string username, int exceptId = 0)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppUsers WHERE Username=@username AND Id<>@exceptId",
            new { username, exceptId }) > 0;
    }

    public async Task<int> CreateAsync(AppUser user, string password)
    {
        using var conn = db.Create();
        user.PasswordHash = PasswordHasher.Hash(password);
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO AppUsers(FullName,Username,Email,Phone,PasswordHash,Role,FieldOfficerId,IsActive)
              VALUES(@FullName,@Username,@Email,@Phone,@PasswordHash,@Role,@FieldOfficerId,@IsActive);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", user);
    }

    public async Task UpdateAsync(AppUser user)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE AppUsers SET FullName=@FullName,Username=@Username,Email=@Email,Phone=@Phone,
              Role=@Role,FieldOfficerId=@FieldOfficerId,IsActive=@IsActive WHERE Id=@Id", user);
    }

    public async Task SetPasswordAsync(int id, string newPassword)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE AppUsers SET PasswordHash=@h WHERE Id=@id", new { h = PasswordHasher.Hash(newPassword), id });
    }

    public async Task ToggleActiveAsync(int id)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE AppUsers SET IsActive = CASE WHEN IsActive=1 THEN 0 ELSE 1 END WHERE Id=@id", new { id });
    }

    public async Task UpdateProfileAsync(int id, string fullName, string? email, string? phone)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE AppUsers SET FullName=@fullName,Email=@email,Phone=@phone WHERE Id=@id", new { id, fullName, email, phone });
    }
}
