using PoultryPro.Models.Entities;


public interface IMarketService
{
    Task<IEnumerable<Order>> GetAllAsync();
    Task<Order?> GetByIdAsync(int id);
    Task<int> CreateAsync(Order order);
    Task UpdateStatusAsync(int id, string status, string paymentStatus);
    Task<IEnumerable<Customer>> GetCustomersAsync();
    Task<int> CreateCustomerAsync(Customer customer);

    // ── extended sales ledger ──
    Task<int> CreateAsync(Order order, decimal advance, string? paymentMethod, string user);
    Task UpdateAsync(Order order);
    Task SetStatusAsync(int id, string status);
    Task<(bool Ok, string Message)> RecordPaymentAsync(int orderId, decimal amount, DateTime date, string? method, string? note, string user);
    Task<IEnumerable<Transaction>> GetPaymentsAsync(string orderCode);
    Task<IEnumerable<Transaction>> GetCustomerPaymentsAsync(int customerId);
    Task<IEnumerable<Order>> GetByCustomerAsync(int customerId);
    Task<Customer?> GetCustomerByIdAsync(int id);
    Task UpdateCustomerAsync(Customer customer);
    Task<IEnumerable<CustomerAccount>> GetCustomerAccountsAsync(bool includeInactive = false);
    Task<CustomerAccount?> GetCustomerAccountAsync(int id);
    Task<IEnumerable<SalesBatchOption>> GetActiveBatchesAsync(int? farmId = null);
    Task<SalesBatchOption?> GetBatchAsync(int id);
}
