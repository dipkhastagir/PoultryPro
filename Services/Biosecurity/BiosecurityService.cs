using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

public interface IBiosecurityService
{
    /// <summary>The standard 14-point weighted checklist (Category, Question, Weight).</summary>
    IReadOnlyList<(string Category, string Question, int Weight)> Checklist { get; }
    Task<List<BiosecurityAudit>> GetAuditsAsync(int? farmId = null);
    Task<BiosecurityAudit?> GetAuditAsync(int id);
    Task<List<BiosecurityAuditItem>> GetItemsAsync(int auditId);
    Task<BiosecurityAudit?> GetPreviousAuditAsync(int farmId, DateTime auditDate, int excludeId);
    /// <summary>Saves the audit + answers and computes Score / MaxScore / Grade in one transaction.</summary>
    Task<int> CreateAuditAsync(BiosecurityChecklistVm vm);
}

public class BiosecurityService(IDbConnectionFactory db) : IBiosecurityService
{
    private static readonly List<(string Category, string Question, int Weight)> Questions = new()
    {
        ("Perimeter & Access",  "Farm has a fence and a single controlled entry gate", 2),
        ("Perimeter & Access",  "Visitor log book is maintained and signed", 1),
        ("Perimeter & Access",  "Vehicle disinfection at entry (spray / wheel dip)", 2),
        ("Personnel Hygiene",   "Footbath with active disinfectant at every shed door", 2),
        ("Personnel Hygiene",   "Dedicated boots and clothing used inside sheds", 2),
        ("Personnel Hygiene",   "Hand-wash station with soap available", 1),
        ("Flock Management",    "All-in all-out placement with minimum 14 days downtime", 3),
        ("Flock Management",    "Dead birds removed at least twice daily and disposed in pit/incinerator", 3),
        ("Flock Management",    "No other poultry, ducks or pet birds on the premises", 2),
        ("Feed & Water",        "Feed stored off the floor in a rodent-proof store", 2),
        ("Feed & Water",        "Drinking water tested or chlorinated", 2),
        ("Pest Control",        "Rodent bait stations maintained and logged", 2),
        ("Pest Control",        "Wild-bird proof netting on all openings", 2),
        ("Records",             "Vaccination and medication records up to date", 2),
    };

    public IReadOnlyList<(string Category, string Question, int Weight)> Checklist => Questions;

    public static string GradeFor(decimal pct) => pct >= 90 ? "A" : pct >= 75 ? "B" : pct >= 60 ? "C" : "D";

    private const string AuditSelect =
        @"SELECT a.Id, a.FarmId, f.Name AS FarmName, a.AuditDate, a.AuditorName, a.Score, a.MaxScore, a.ScorePct,
                 a.Grade, a.Notes, a.CreatedAt,
                 (SELECT COUNT(*) FROM BiosecurityAuditItems i WHERE i.AuditId = a.Id AND i.Passed = 0) AS FailedCount
          FROM BiosecurityAudits a INNER JOIN Farms f ON a.FarmId = f.Id";

    public async Task<List<BiosecurityAudit>> GetAuditsAsync(int? farmId = null)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<BiosecurityAudit>(
            AuditSelect + " WHERE (@farmId IS NULL OR a.FarmId = @farmId) ORDER BY a.AuditDate DESC, a.Id DESC", new { farmId });
        return rows.ToList();
    }

    public async Task<BiosecurityAudit?> GetAuditAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<BiosecurityAudit>(AuditSelect + " WHERE a.Id = @id", new { id });
    }

    public async Task<List<BiosecurityAuditItem>> GetItemsAsync(int auditId)
    {
        using var conn = db.Create();
        var rows = await conn.QueryAsync<BiosecurityAuditItem>(
            "SELECT * FROM BiosecurityAuditItems WHERE AuditId = @auditId ORDER BY Id", new { auditId });
        return rows.ToList();
    }

    public async Task<BiosecurityAudit?> GetPreviousAuditAsync(int farmId, DateTime auditDate, int excludeId)
    {
        using var conn = db.Create();
        return await conn.QueryFirstOrDefaultAsync<BiosecurityAudit>(
            AuditSelect + @" WHERE a.FarmId = @farmId AND a.Id <> @excludeId
                               AND (a.AuditDate < @auditDate OR (a.AuditDate = @auditDate AND a.Id < @excludeId))
                             ORDER BY a.AuditDate DESC, a.Id DESC", new { farmId, auditDate, excludeId });
    }

    public async Task<int> CreateAuditAsync(BiosecurityChecklistVm vm)
    {
        // Only the standard questions are accepted; weights come from the server-side checklist, not the form.
        var answers = Questions.Select((q, i) =>
        {
            var posted = i < vm.Items.Count ? vm.Items[i] : null;
            return new BiosecurityAuditItem
            {
                Category = q.Category, Question = q.Question, Weight = q.Weight,
                Passed = posted?.Passed ?? false,
                Remark = string.IsNullOrWhiteSpace(posted?.Remark) ? null : posted!.Remark!.Trim()
            };
        }).ToList();

        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var auditId = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO BiosecurityAudits (FarmId, AuditDate, AuditorName, Score, MaxScore, Grade, Notes)
              VALUES (@FarmId, @AuditDate, @AuditorName, 0, 0, 'D', @Notes);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", vm, tx);

        foreach (var a in answers) a.AuditId = auditId;
        await conn.ExecuteAsync(
            @"INSERT INTO BiosecurityAuditItems (AuditId, Category, Question, Weight, Passed, Remark)
              VALUES (@AuditId, @Category, @Question, @Weight, @Passed, @Remark)", answers, tx);

        decimal score = answers.Where(a => a.Passed).Sum(a => a.Weight);
        decimal max = answers.Sum(a => a.Weight);
        var grade = GradeFor(max > 0 ? score / max * 100 : 0);
        await conn.ExecuteAsync(
            "UPDATE BiosecurityAudits SET Score=@score, MaxScore=@max, Grade=@grade WHERE Id=@auditId",
            new { score, max, grade, auditId }, tx);

        tx.Commit();
        return auditId;
    }
}
