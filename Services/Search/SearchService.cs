using Dapper;
using PoultryPro.Models.ViewModels;

/// <summary>Global search across the main business entities (top-bar search box).</summary>
public interface ISearchService
{
    Task<SearchResultVm> SearchAsync(string? q, bool includeFinance = true, int perGroup = 8);
}

public class SearchService(IDbConnectionFactory db) : ISearchService
{
    private record GroupDef(string Key, string Label, string Icon, string Tone, string Sql, Func<SearchHit, string> Url, bool Finance = false);

    private static readonly GroupDef[] Defs =
    {
        new("batches", "Flocks / Batches", "bi-collection", "green",
            @"SELECT TOP (@n) Id, BatchCode AS Title,
                     CONCAT(Breed, ' · ', FarmName, ' · ', CurrentCount, ' birds · day ', AgeInDays) AS Subtitle,
                     CASE WHEN IsActive = 1 THEN HealthStatus ELSE 'Closed' END AS Badge
              FROM vw_BatchDetails WHERE BatchCode LIKE @like OR Breed LIKE @like OR HatcheryName LIKE @like
              ORDER BY IsActive DESC, PlacementDate DESC",
            h => $"/Batch/Detail/{h.Id}"),
        new("farms", "Farms", "bi-house-gear", "blue",
            @"SELECT TOP (@n) Id, Name AS Title, CONCAT(Location, ISNULL(' · ' + Zone, ''), ' · ', FarmType) AS Subtitle, CertStatus AS Badge
              FROM Farms WHERE IsActive = 1 AND (Name LIKE @like OR Location LIKE @like OR Zone LIKE @like OR OwnerName LIKE @like)
              ORDER BY Name",
            h => $"/Farm/Detail/{h.Id}"),
        new("customers", "Customers", "bi-people", "violet",
            @"SELECT TOP (@n) Id, Name AS Title, CONCAT(CustomerType, ISNULL(' · ' + Phone, ''), ISNULL(' · ' + Address, '')) AS Subtitle,
                     CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Badge
              FROM Customers WHERE Name LIKE @like OR Phone LIKE @like OR Email LIKE @like OR Address LIKE @like
              ORDER BY Name",
            h => $"/Market/Customers?q={Uri.EscapeDataString(h.Title)}", Finance: true),
        new("suppliers", "Suppliers", "bi-buildings", "amber",
            @"SELECT TOP (@n) Id, Name AS Title, CONCAT(Category, ISNULL(' · ' + ContactPerson, ''), ISNULL(' · ' + Phone, '')) AS Subtitle,
                     CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Badge
              FROM Suppliers WHERE Name LIKE @like OR ContactPerson LIKE @like OR Category LIKE @like OR Phone LIKE @like
              ORDER BY Name",
            h => $"/Supplier/Detail/{h.Id}", Finance: true),
        new("orders", "Sales Orders", "bi-cart3", "green",
            @"SELECT TOP (@n) o.Id, o.OrderCode AS Title,
                     CONCAT(c.Name, ' · ', o.Product, ' · ', FORMAT(o.Quantity, 'N0'), ' ', o.Unit, ' · ', FORMAT(o.OrderDate, 'dd MMM yyyy')) AS Subtitle,
                     o.Status AS Badge
              FROM Orders o INNER JOIN Customers c ON c.Id = o.CustomerId
              WHERE o.OrderCode LIKE @like OR o.Product LIKE @like OR c.Name LIKE @like
              ORDER BY o.OrderDate DESC",
            h => $"/Market/Detail/{h.Id}", Finance: true),
        new("employees", "Employees", "bi-person-vcard", "slate",
            @"SELECT TOP (@n) e.Id, e.FullName AS Title, CONCAT(e.Role, ISNULL(' · ' + f.Name, ''), ISNULL(' · ' + e.Phone, '')) AS Subtitle, e.Status AS Badge
              FROM Employees e LEFT JOIN Farms f ON f.Id = e.FarmId
              WHERE e.IsActive = 1 AND (e.FullName LIKE @like OR e.Role LIKE @like OR e.EmployeeCode LIKE @like OR e.Phone LIKE @like)
              ORDER BY e.FullName",
            h => $"/Employee/Detail/{h.Id}"),
        new("officers", "Field Officers", "bi-person-badge", "blue",
            @"SELECT TOP (@n) Id, FullName AS Title, CONCAT(Designation, ISNULL(' · ' + Zone, ''), ' · @', Username) AS Subtitle,
                     CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS Badge
              FROM FieldOfficers WHERE FullName LIKE @like OR Username LIKE @like OR Zone LIKE @like OR Phone LIKE @like
              ORDER BY FullName",
            h => $"/FieldOfficers/Detail/{h.Id}"),
        new("inventory", "Inventory Items", "bi-box-seam", "amber",
            @"SELECT TOP (@n) Id, Name AS Title, CONCAT(Category, ISNULL(' · SKU ' + SKU, ''), ' · ', FORMAT(Quantity, 'N0'), ' ', Unit, ' in stock') AS Subtitle, StockStatus AS Badge
              FROM vw_InventoryStatus WHERE Name LIKE @like OR SKU LIKE @like OR Category LIKE @like
              ORDER BY Name",
            h => $"/Inventory/Index?q={Uri.EscapeDataString(h.Title)}"),
        new("diseases", "Disease Library", "bi-book", "red",
            @"SELECT TOP (@n) Id, Name AS Title, CONCAT(Category, ISNULL(' · ' + LocalName, ''), ISNULL(' · ' + AgeGroup, '')) AS Subtitle, Severity AS Badge
              FROM DiseaseLibrary WHERE Name LIKE @like OR LocalName LIKE @like OR Category LIKE @like OR Symptoms LIKE @like
              ORDER BY Name",
            h => $"/DiseaseLibrary/Detail/{h.Id}"),
    };

    public async Task<SearchResultVm> SearchAsync(string? q, bool includeFinance = true, int perGroup = 8)
    {
        var vm = new SearchResultVm { Query = (q ?? "").Trim() };
        if (vm.Query.Length < 2) { vm.TooShort = vm.Query.Length > 0; return vm; }

        // Escape LIKE wildcards so user input is matched literally
        var safe = vm.Query.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        var args = new { like = $"%{safe}%", n = perGroup };

        using var conn = db.Create();
        foreach (var d in Defs)
        {
            if (d.Finance && !includeFinance) continue;
            List<SearchHit> hits;
            try { hits = (await conn.QueryAsync<SearchHit>(d.Sql, args)).ToList(); }
            catch (Microsoft.Data.SqlClient.SqlException) { continue; }   // table not present in an older database
            if (hits.Count == 0) continue;
            foreach (var h in hits) { h.Group = d.Key; h.Url = d.Url(h); }
            vm.Groups.Add(new SearchGroup { Key = d.Key, Label = d.Label, Icon = d.Icon, Tone = d.Tone, Hits = hits });
        }
        return vm;
    }
}
