namespace PoultryPro.Infrastructure;

/// <summary>Application roles. Use the combined constants with [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string Admin        = "Admin";
    public const string Manager      = "Manager";
    public const string Veterinarian = "Veterinarian";
    public const string Accountant   = "Accountant";
    public const string FieldOfficer = "FieldOfficer";
    public const string Viewer       = "Viewer";

    public const string AdminOrManager = Admin + "," + Manager;
    public const string Finance        = Admin + "," + Manager + "," + Accountant;
    public const string Health         = Admin + "," + Manager + "," + Veterinarian;
    public const string Operations     = Admin + "," + Manager + "," + Veterinarian + "," + FieldOfficer;

    public static readonly string[] All = { Admin, Manager, Veterinarian, Accountant, FieldOfficer, Viewer };

    public static string Describe(string role) => role switch
    {
        Admin        => "Full access including users, settings and audit log",
        Manager      => "All farm, health, supply and finance operations",
        Veterinarian => "Flock health, vaccination, biosecurity and analytics",
        Accountant   => "Finance, sales, purchasing, inventory and payroll",
        FieldOfficer => "Farm visits and daily flock records",
        Viewer       => "Read-only access to dashboards, analytics and research data",
        _            => role
    };
}
