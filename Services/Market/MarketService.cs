using Dapper;
using PoultryPro.Models.Entities;

public class MarketService(IDbConnectionFactory db) : IMarketService
{
    private const string OrderSql = @"
        SELECT o.*, c.Name AS CustomerName, c.Phone AS CustomerPhone, c.Email AS CustomerEmail,
               c.Address AS CustomerAddress, c.CustomerType, f.Name AS FarmName, f.Location AS FarmLocation, b.BatchCode
        FROM Orders o
        INNER JOIN Customers c ON o.CustomerId=c.Id
        INNER JOIN Farms f ON o.FarmId=f.Id
        LEFT JOIN Batches b ON b.Id=o.BatchId";

    private const string AccountSql = @"
        SELECT c.*,
               ISNULL(x.OrderCount,0) AS OrderCount, ISNULL(x.OpenOrders,0) AS OpenOrders,
               ISNULL(x.TotalBilled,0) AS TotalBilled, ISNULL(x.TotalPaid,0) AS TotalPaid, x.LastOrderDate
        FROM Customers c
        LEFT JOIN (SELECT CustomerId, COUNT(*) AS OrderCount,
                          SUM(CASE WHEN Status NOT IN ('Delivered','Cancelled') THEN 1 ELSE 0 END) AS OpenOrders,
                          SUM(CASE WHEN Status <> 'Cancelled' THEN TotalAmount ELSE 0 END) AS TotalBilled,
                          SUM(CASE WHEN Status <> 'Cancelled' THEN PaidAmount ELSE 0 END) AS TotalPaid,
                          MAX(OrderDate) AS LastOrderDate
                   FROM Orders GROUP BY CustomerId) x ON x.CustomerId=c.Id";

    public async Task<IEnumerable<Order>> GetAllAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Order>(OrderSql + " ORDER BY o.OrderDate DESC, o.Id DESC");
    }
    public async Task<Order?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Order>(OrderSql + " WHERE o.Id=@id", new { id });
    }
    public async Task<IEnumerable<Order>> GetByCustomerAsync(int customerId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Order>(OrderSql + " WHERE o.CustomerId=@customerId ORDER BY o.OrderDate DESC, o.Id DESC", new { customerId });
    }

    public Task<int> CreateAsync(Order order) => CreateAsync(order, 0, null, "system");

    public async Task<int> CreateAsync(Order order, decimal advance, string? paymentMethod, string user)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var baseCode = $"SO-{DateTime.Now:yyMMdd-HHmmss}";
        var code = baseCode;
        for (var n = 2; await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Orders WHERE OrderCode=@code", new { code }, tx) > 0; n++)
            code = $"{baseCode}-{n}";
        order.OrderCode = code;
        var total = Math.Round(order.Quantity * order.UnitPrice, 2);
        order.PaidAmount = Math.Min(Math.Max(0, advance), total);
        order.PaymentStatus = PaymentStatusFor(order.PaidAmount, total);
        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Orders(OrderCode,CustomerId,FarmId,Product,Quantity,Unit,Grade,UnitPrice,OrderDate,DeliveryDate,Status,PaymentStatus,Notes,BatchId,BirdCount,PaidAmount)
              VALUES(@OrderCode,@CustomerId,@FarmId,@Product,@Quantity,@Unit,@Grade,@UnitPrice,@OrderDate,@DeliveryDate,@Status,@PaymentStatus,@Notes,@BatchId,@BirdCount,@PaidAmount);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", order, tx);
        if (order.PaidAmount > 0)
            await InsertPaymentTxnAsync(conn, tx, order, order.PaidAmount, order.OrderDate, paymentMethod, "Advance received");
        tx.Commit();
        return id;
    }

    private static string PaymentStatusFor(decimal paid, decimal total)
        => paid <= 0 ? "Unpaid" : paid >= total ? "Paid" : "Partial";

    private static async Task InsertPaymentTxnAsync(System.Data.IDbConnection conn, System.Data.IDbTransaction tx, Order o,
                                                    decimal amount, DateTime date, string? method, string? note)
    {
        var customer = string.IsNullOrEmpty(o.CustomerName)
            ? await conn.ExecuteScalarAsync<string>("SELECT Name FROM Customers WHERE Id=@CustomerId", new { o.CustomerId }, tx)
            : o.CustomerName;
        var desc = $"Payment {o.OrderCode} — {customer}" + (string.IsNullOrWhiteSpace(note) ? "" : $" ({note})");
        if (desc.Length > 300) desc = desc[..300];
        await conn.ExecuteAsync(
            @"INSERT INTO Transactions(TxnDate,TxnType,Category,Description,Amount,PaymentStatus,FarmId,Reference,BatchId,PaymentMethod)
              VALUES(@date,'Sale','Customer Payment',@desc,@amount,'Paid',@FarmId,@OrderCode,@BatchId,@method)",
            new { date, desc, amount, o.FarmId, o.OrderCode, o.BatchId, method }, tx);
    }

    public async Task UpdateAsync(Order order)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE Orders SET CustomerId=@CustomerId,FarmId=@FarmId,BatchId=@BatchId,BirdCount=@BirdCount,Product=@Product,
                  Quantity=@Quantity,Unit=@Unit,Grade=@Grade,UnitPrice=@UnitPrice,OrderDate=@OrderDate,DeliveryDate=@DeliveryDate,
                  Status=@Status,Notes=@Notes
              WHERE Id=@Id;
              UPDATE Orders SET PaymentStatus = CASE WHEN PaidAmount <= 0 THEN 'Unpaid' WHEN PaidAmount >= TotalAmount THEN 'Paid' ELSE 'Partial' END
              WHERE Id=@Id;", order);
    }

    public async Task UpdateStatusAsync(int id, string status, string paymentStatus)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            "UPDATE Orders SET Status=@status,PaymentStatus=@paymentStatus WHERE Id=@id",
            new { id, status, paymentStatus });
    }

    public async Task SetStatusAsync(int id, string status)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("UPDATE Orders SET Status=@status WHERE Id=@id", new { id, status });
    }

    public async Task<(bool Ok, string Message)> RecordPaymentAsync(int orderId, decimal amount, DateTime date, string? method, string? note, string user)
    {
        if (amount <= 0) return (false, "Payment amount must be greater than zero.");
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var o = await conn.QuerySingleOrDefaultAsync<Order>(
            "SELECT o.*, c.Name AS CustomerName FROM Orders o WITH (UPDLOCK) INNER JOIN Customers c ON c.Id=o.CustomerId WHERE o.Id=@orderId",
            new { orderId }, tx);
        if (o == null) { tx.Rollback(); return (false, "Order not found."); }
        if (o.Status == "Cancelled") { tx.Rollback(); return (false, "Cannot record a payment against a cancelled order."); }
        var due = o.TotalAmount - o.PaidAmount;
        if (due <= 0) { tx.Rollback(); return (false, $"{o.OrderCode} is already fully paid."); }
        if (amount > due) { tx.Rollback(); return (false, $"Amount exceeds the balance due ({PoultryPro.Infrastructure.Fmt.Money(due, 2)})."); }
        var newPaid = o.PaidAmount + amount;
        await conn.ExecuteAsync("UPDATE Orders SET PaidAmount=@newPaid, PaymentStatus=@ps WHERE Id=@orderId",
            new { newPaid, ps = PaymentStatusFor(newPaid, o.TotalAmount), orderId }, tx);
        await InsertPaymentTxnAsync(conn, tx, o, amount, date, method, note);
        tx.Commit();
        return (true, $"Payment of {PoultryPro.Infrastructure.Fmt.Money(amount)} recorded for {o.OrderCode}.");
    }

    public async Task<IEnumerable<Transaction>> GetPaymentsAsync(string orderCode)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Transaction>(
            @"SELECT * FROM Transactions WHERE Reference=@orderCode AND Category='Customer Payment' ORDER BY TxnDate, Id", new { orderCode });
    }

    public async Task<IEnumerable<Transaction>> GetCustomerPaymentsAsync(int customerId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Transaction>(
            @"SELECT t.* FROM Transactions t INNER JOIN Orders o ON o.OrderCode=t.Reference
              WHERE o.CustomerId=@customerId AND t.Category='Customer Payment' ORDER BY t.TxnDate DESC, t.Id DESC", new { customerId });
    }

    public async Task<IEnumerable<Customer>> GetCustomersAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Customer>("SELECT * FROM Customers WHERE IsActive=1 ORDER BY Name");
    }
    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Customer>("SELECT * FROM Customers WHERE Id=@id", new { id });
    }
    public async Task<int> CreateCustomerAsync(Customer customer)
    {
        using var conn = db.Create();
        return await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Customers(Name,Phone,Email,Address,CustomerType,CreditLimit)
              VALUES(@Name,@Phone,@Email,@Address,@CustomerType,@CreditLimit);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", customer);
    }
    public async Task UpdateCustomerAsync(Customer customer)
    {
        using var conn = db.Create();
        await conn.ExecuteAsync(
            @"UPDATE Customers SET Name=@Name,Phone=@Phone,Email=@Email,Address=@Address,CustomerType=@CustomerType,
              CreditLimit=@CreditLimit,IsActive=@IsActive WHERE Id=@Id", customer);
    }
    public async Task<IEnumerable<CustomerAccount>> GetCustomerAccountsAsync(bool includeInactive = false)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<CustomerAccount>(AccountSql + " WHERE (@includeInactive=1 OR c.IsActive=1) ORDER BY c.Name", new { includeInactive });
    }
    public async Task<CustomerAccount?> GetCustomerAccountAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<CustomerAccount>(AccountSql + " WHERE c.Id=@id", new { id });
    }

    private const string BatchSql = @"SELECT Id, BatchCode, FarmId, Breed, BatchType, CurrentCount, AverageWeightKg,
                                             DATEDIFF(DAY, PlacementDate, CAST(GETDATE() AS DATE)) AS AgeInDays FROM Batches";
    public async Task<IEnumerable<SalesBatchOption>> GetActiveBatchesAsync(int? farmId = null)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<SalesBatchOption>(BatchSql + " WHERE IsActive=1 AND (@farmId IS NULL OR FarmId=@farmId) ORDER BY BatchCode", new { farmId });
    }
    public async Task<SalesBatchOption?> GetBatchAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<SalesBatchOption>(BatchSql + " WHERE Id=@id", new { id });
    }
}
