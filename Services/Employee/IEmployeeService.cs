using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

public interface IEmployeeService
{
    /// <summary>Active employees only (used by other modules, dropdowns and reports).</summary>
    Task<IEnumerable<Employee>> GetAllAsync();
    /// <summary>Directory listing including inactive (former) staff when requested.</summary>
    Task<IEnumerable<Employee>> GetDirectoryAsync(bool includeInactive);
    Task<Employee?> GetByIdAsync(int id);
    Task<int> CreateAsync(Employee employee);
    Task UpdateAsync(Employee employee);
    Task DeleteAsync(int id);
    Task<bool> CodeExistsAsync(string code, int exceptId = 0);
    Task<string> NextCodeAsync();
    Task<EmployeeProfileVm?> GetProfileAsync(int id);
    Task<(int Marked, decimal PresentWeight)> GetTodayAttendanceAsync();
}
