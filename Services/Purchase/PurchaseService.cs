using System.Data;
using Dapper;
using PoultryPro.Models.Entities;
using PoultryPro.Models.ViewModels;

public interface IPurchaseService
{
    Task<IEnumerable<PurchaseOrder>> GetAllAsync(string? status = null, int? supplierId = null);
    Task<PurchaseOrder?> GetByIdAsync(int id);
    Task<IEnumerable<PurchaseOrderLine>> GetLinesAsync(int purchaseOrderId);
    Task<string> PreviewPoNumberAsync();
    Task<(int Id, string PoNumber)> CreateAsync(PurchaseOrderCreateVm vm, string user);
    Task<bool> MarkOrderedAsync(int id);
    Task<bool> CancelAsync(int id);
    Task<bool> MarkPaidAsync(int id);
    /// <summary>Receives the PO: stock IN for every line, moving-average unit cost, and finance purchase transactions.</summary>
    Task<(bool Ok, string Message)> ReceiveAsync(int id, DateTime receivedDate, string user);
}

public class PurchaseService(IDbConnectionFactory db) : IPurchaseService
{
    private const string HeaderSql = @"
        SELECT po.*, s.Name AS SupplierName, s.ContactPerson AS SupplierContact, s.Phone AS SupplierPhone,
               s.Email AS SupplierEmail, s.Address AS SupplierAddress, s.PaymentTerms AS SupplierPaymentTerms,
               f.Name AS FarmName, f.Location AS FarmLocation,
               (SELECT COUNT(*) FROM PurchaseOrderItems i WHERE i.PurchaseOrderId = po.Id) AS LineCount
        FROM PurchaseOrders po
        INNER JOIN Suppliers s ON s.Id = po.SupplierId
        LEFT JOIN Farms f ON f.Id = po.FarmId";

    public async Task<IEnumerable<PurchaseOrder>> GetAllAsync(string? status = null, int? supplierId = null)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<PurchaseOrder>(HeaderSql + @"
            WHERE (@status IS NULL OR po.Status=@status) AND (@supplierId IS NULL OR po.SupplierId=@supplierId)
            ORDER BY po.OrderDate DESC, po.Id DESC", new { status, supplierId });
    }

    public async Task<PurchaseOrder?> GetByIdAsync(int id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<PurchaseOrder>(HeaderSql + " WHERE po.Id=@id", new { id });
    }

    public async Task<IEnumerable<PurchaseOrderLine>> GetLinesAsync(int purchaseOrderId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<PurchaseOrderLine>(
            @"SELECT l.Id, l.PurchaseOrderId, l.InventoryItemId, l.Quantity, l.UnitCost, l.LineTotal,
                     i.Name AS ItemName, i.SKU, i.Category, i.Unit
              FROM PurchaseOrderItems l INNER JOIN InventoryItems i ON i.Id = l.InventoryItemId
              WHERE l.PurchaseOrderId=@purchaseOrderId ORDER BY l.Id", new { purchaseOrderId });
    }

    private static async Task<string> NextPoNumberAsync(IDbConnection conn, IDbTransaction? tx)
    {
        var prefix = $"PO-{DateTime.Today:yyMM}-";
        var last = await conn.ExecuteScalarAsync<string?>(
            "SELECT MAX(PoNumber) FROM PurchaseOrders WITH (UPDLOCK, HOLDLOCK) WHERE PoNumber LIKE @p",
            new { p = prefix + "%" }, tx);
        var seq = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n)) seq = n + 1;
        return prefix + seq.ToString("000");
    }

    public async Task<string> PreviewPoNumberAsync()
    {
        using var conn = db.Create();
        return await NextPoNumberAsync(conn, null);
    }

    public async Task<(int Id, string PoNumber)> CreateAsync(PurchaseOrderCreateVm vm, string user)
    {
        var lines = vm.Lines.Where(l => l.InventoryItemId > 0 && l.Quantity > 0).ToList();
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var poNumber = await NextPoNumberAsync(conn, tx);
        var status = vm.SaveAs == "Ordered" ? "Ordered" : "Draft";
        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO PurchaseOrders(PoNumber,SupplierId,FarmId,OrderDate,ExpectedDate,Status,PaymentStatus,TotalAmount,Notes,CreatedBy)
              VALUES(@poNumber,@SupplierId,@FarmId,@OrderDate,@ExpectedDate,@status,'Unpaid',@total,@Notes,@user);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { poNumber, vm.SupplierId, vm.FarmId, vm.OrderDate, vm.ExpectedDate, status,
                  total = lines.Sum(l => Math.Round(l.Quantity * l.UnitCost, 2)), vm.Notes, user }, tx);
        foreach (var l in lines)
            await conn.ExecuteAsync(
                "INSERT INTO PurchaseOrderItems(PurchaseOrderId,InventoryItemId,Quantity,UnitCost) VALUES(@id,@InventoryItemId,@Quantity,@UnitCost)",
                new { id, l.InventoryItemId, l.Quantity, l.UnitCost }, tx);
        await conn.ExecuteAsync(
            "UPDATE PurchaseOrders SET TotalAmount=ISNULL((SELECT SUM(LineTotal) FROM PurchaseOrderItems WHERE PurchaseOrderId=@id),0) WHERE Id=@id",
            new { id }, tx);
        tx.Commit();
        return (id, poNumber);
    }

    public async Task<bool> MarkOrderedAsync(int id)
    {
        using var conn = db.Create();
        return await conn.ExecuteAsync("UPDATE PurchaseOrders SET Status='Ordered' WHERE Id=@id AND Status='Draft'", new { id }) > 0;
    }

    public async Task<bool> CancelAsync(int id)
    {
        using var conn = db.Create();
        return await conn.ExecuteAsync(
            "UPDATE PurchaseOrders SET Status='Cancelled' WHERE Id=@id AND Status IN ('Draft','Ordered')", new { id }) > 0;
    }

    public async Task<bool> MarkPaidAsync(int id)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var po = await conn.QuerySingleOrDefaultAsync<PurchaseOrder>(
            "SELECT * FROM PurchaseOrders WHERE Id=@id", new { id }, tx);
        if (po == null || po.Status == "Cancelled" || po.PaymentStatus == "Paid") { tx.Rollback(); return false; }
        await conn.ExecuteAsync("UPDATE PurchaseOrders SET PaymentStatus='Paid' WHERE Id=@id", new { id }, tx);
        await conn.ExecuteAsync(
            "UPDATE Transactions SET PaymentStatus='Paid' WHERE Reference=@PoNumber AND TxnType='Purchase'",
            new { po.PoNumber }, tx);
        tx.Commit();
        return true;
    }

    public async Task<(bool Ok, string Message)> ReceiveAsync(int id, DateTime receivedDate, string user)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        var po = await conn.QuerySingleOrDefaultAsync<PurchaseOrder>(
            @"SELECT po.*, s.Name AS SupplierName FROM PurchaseOrders po WITH (UPDLOCK)
              INNER JOIN Suppliers s ON s.Id=po.SupplierId WHERE po.Id=@id", new { id }, tx);
        if (po == null) { tx.Rollback(); return (false, "Purchase order not found."); }
        if (po.Status is not ("Draft" or "Ordered")) { tx.Rollback(); return (false, $"{po.PoNumber} is {po.Status} and cannot be received."); }

        var lines = (await conn.QueryAsync<PurchaseOrderLine>(
            @"SELECT l.Id, l.PurchaseOrderId, l.InventoryItemId, l.Quantity, l.UnitCost, l.LineTotal, i.Name AS ItemName, i.Category, i.Unit
              FROM PurchaseOrderItems l INNER JOIN InventoryItems i ON i.Id=l.InventoryItemId WHERE l.PurchaseOrderId=@id",
            new { id }, tx)).ToList();
        if (lines.Count == 0) { tx.Rollback(); return (false, $"{po.PoNumber} has no line items."); }

        await conn.ExecuteAsync(
            "UPDATE PurchaseOrders SET Status='Received', ReceivedDate=@receivedDate WHERE Id=@id", new { id, receivedDate }, tx);

        foreach (var l in lines)
        {
            await conn.ExecuteAsync(
                @"INSERT INTO InventoryTransactions(ItemId,TxnType,Quantity,Reason,TxnDate,UnitCost,FarmId,Reference,CreatedBy)
                  VALUES(@InventoryItemId,'IN',@Quantity,@reason,@receivedDate,@UnitCost,@FarmId,@PoNumber,@user);
                  UPDATE InventoryItems SET
                      UnitCost = CASE WHEN Quantity > 0 THEN CAST((Quantity*UnitCost + @Quantity*@UnitCost)/(Quantity+@Quantity) AS DECIMAL(12,2))
                                      ELSE @UnitCost END,
                      Quantity = Quantity + @Quantity,
                      SupplierId = ISNULL(SupplierId, @SupplierId),
                      UpdatedAt = GETDATE()
                  WHERE Id=@InventoryItemId;",
                new { l.InventoryItemId, l.Quantity, l.UnitCost, reason = $"PO receipt from {po.SupplierName}",
                      receivedDate, po.FarmId, po.PoNumber, user, po.SupplierId }, tx);
        }

        var paymentStatus = po.PaymentStatus == "Paid" ? "Paid" : "Pending";
        foreach (var g in lines.GroupBy(l => l.Category))
        {
            var amount = g.Sum(l => l.LineTotal);
            if (amount == 0) continue;
            var desc = $"{po.PoNumber} — {po.SupplierName}: " + string.Join(", ", g.Select(l => l.ItemName).Take(3)) + (g.Count() > 3 ? $" +{g.Count() - 3} more" : "");
            if (desc.Length > 300) desc = desc[..300];
            await conn.ExecuteAsync(
                @"INSERT INTO Transactions(TxnDate,TxnType,Category,Description,Amount,PaymentStatus,FarmId,Reference)
                  VALUES(@receivedDate,'Purchase',@cat,@desc,@amount,@paymentStatus,@FarmId,@PoNumber)",
                new { receivedDate, cat = g.Key, desc, amount = -amount, paymentStatus, po.FarmId, po.PoNumber }, tx);
        }
        tx.Commit();
        return (true, $"{po.PoNumber} received — {lines.Count} line(s) added to stock and posted to finance.");
    }
}
