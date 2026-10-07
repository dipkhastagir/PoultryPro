using Dapper;
using PoultryPro.Models.Entities;

/// <summary>CRUD for breed performance standards (reference curves used by the analytics engine).</summary>
public interface IBreedStandardService
{
    Task<List<(string Breed, int Points, int MaxAge, int Batches)>> GetBreedsAsync();
    Task<List<BreedStandard>> GetByBreedAsync(string breed);
    Task<BreedStandard?> GetByIdAsync(int id);
    Task<bool> ExistsAsync(string breed, int ageDay, int exceptId = 0);
    Task<int> CreateAsync(BreedStandard s);
    Task UpdateAsync(BreedStandard s);
    Task<string?> DeleteAsync(int id);
}

public class BreedStandardService(IDbConnectionFactory db) : IBreedStandardService
{
    public async Task<List<(string Breed, int Points, int MaxAge, int Batches)>> GetBreedsAsync()
    {
        using var conn = db.Create();
        return (await conn.QueryAsync<(string Breed, int Points, int MaxAge, int Batches)>(
            @"SELECT s.Breed, COUNT(*) AS Points, MAX(s.AgeDay) AS MaxAge,
                     (SELECT COUNT(*) FROM Batches b WHERE b.Breed = s.Breed) AS Batches
              FROM BreedStandards s GROUP BY s.Breed ORDER BY s.Breed")).ToList();
    }

    public async Task<List<BreedStandard>> GetByBreedAsync(string breed)
    {
        using var conn = db.Create();
        return (await conn.QueryAsync<BreedStandard>("SELECT * FROM BreedStandards WHERE Breed = @breed ORDER BY AgeDay", new { breed })).ToList();
    }

    public async Task<BreedStandard?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<BreedStandard>("SELECT * FROM BreedStandards WHERE Id = @id", new { id });
    }

    public async Task<bool> ExistsAsync(string breed, int ageDay, int exceptId = 0)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM BreedStandards WHERE Breed = @breed AND AgeDay = @ageDay AND Id <> @exceptId",
            new { breed, ageDay, exceptId }) > 0;
    }

    private static void Normalise(BreedStandard s)
    {
        s.Breed = s.Breed.Trim();
        if (s.CumFeedGm <= 0 && s.TargetFCR > 0) s.CumFeedGm = Math.Round(s.TargetWeightGm * s.TargetFCR, 0);
    }

    public async Task<int> CreateAsync(BreedStandard s)
    {
        Normalise(s);
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO BreedStandards (Breed, AgeDay, TargetWeightGm, DailyFeedGm, CumFeedGm, TargetFCR, TargetCumMortPct)
              VALUES (@Breed, @AgeDay, @TargetWeightGm, @DailyFeedGm, @CumFeedGm, @TargetFCR, @TargetCumMortPct);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", s);
    }

    public async Task UpdateAsync(BreedStandard s)
    {
        Normalise(s);
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE BreedStandards SET Breed=@Breed, AgeDay=@AgeDay, TargetWeightGm=@TargetWeightGm, DailyFeedGm=@DailyFeedGm,
                     CumFeedGm=@CumFeedGm, TargetFCR=@TargetFCR, TargetCumMortPct=@TargetCumMortPct WHERE Id=@Id", s);
    }

    /// <summary>Deletes one point and returns its breed (for redirecting back to the breed tab).</summary>
    public async Task<string?> DeleteAsync(int id)
    {
        using var conn = db.Create();
        var breed = await conn.ExecuteScalarAsync<string?>("SELECT Breed FROM BreedStandards WHERE Id = @id", new { id });
        await conn.ExecuteAsync("DELETE FROM BreedStandards WHERE Id = @id", new { id });
        return breed;
    }
}
