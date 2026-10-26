using Dapper;
using Libro.Desktop.Data;
using Libro.Desktop.Util;
using Microsoft.Data.Sqlite;

namespace Libro.Desktop.Services;

public sealed class ProductInput
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "pieza";
    public long PriceCents { get; set; }
    public long CostCents { get; set; }
    public double IvaRate { get; set; } = 0.16;
    public double MinStock { get; set; }
    public bool TrackStock { get; set; } = true;
    public double InitialStock { get; set; }
}

public sealed class InventoryFilters
{
    public string? Query { get; set; }
    public bool OnlyLow { get; set; }
    public bool IncludeInactive { get; set; }
}

public sealed class InventorySummary
{
    public long ValueCents { get; set; }
    public long ProductCount { get; set; }
    public double UnitCount { get; set; }
    public long LowStockCount { get; set; }
    public long OutOfStockCount { get; set; }
}

public sealed class InventoryMovementInput
{
    public long ProductId { get; set; }
    public string Date { get; set; } = "";
    public string Type { get; set; } = "entrada";
    public double Qty { get; set; }
    public long UnitCostCents { get; set; }
    public double IvaRate { get; set; } = 0.16;
    public string Reason { get; set; } = "";
    public string Reference { get; set; } = "";
    public long? AccountId { get; set; }
}

internal sealed class InventorySummaryRow
{
    public long Value { get; set; }
    public long Products { get; set; }
    public double Units { get; set; }
    public long Low { get; set; }
    public long Out { get; set; }
}

public static class InventoryService
{
    public static List<Product> GetProducts(InventoryFilters? filters = null)
    {
        filters ??= new InventoryFilters();
        var all = AppState.Db.All<Product>(
            $"SELECT * FROM products {(filters.IncludeInactive ? "" : "WHERE active = 1")} ORDER BY name;");
        var query = filters.Query?.Trim() ?? "";
        return all
            .Where(product => !filters.OnlyLow ||
                              product.TrackStock == 1 && product.Stock <= product.MinStock)
            .Where(product => string.IsNullOrWhiteSpace(query) ||
                              product.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                              product.Sku.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static Product? GetProduct(long id) =>
        AppState.Db.One<Product>("SELECT * FROM products WHERE id = $id;", new { id });

    public static InventorySummary GetSummary()
    {
        var row = AppState.Db.One<InventorySummaryRow>(
            """
            SELECT
              COALESCE(SUM(CASE WHEN track_stock = 1 THEN CAST(ROUND(stock * cost_cents) AS INTEGER) ELSE 0 END), 0) AS value,
              COUNT(*) AS products,
              COALESCE(SUM(CASE WHEN track_stock = 1 THEN stock ELSE 0 END), 0) AS units,
              COALESCE(SUM(CASE WHEN track_stock = 1 AND stock <= min_stock AND stock > 0 THEN 1 ELSE 0 END), 0) AS low,
              COALESCE(SUM(CASE WHEN track_stock = 1 AND stock <= 0 THEN 1 ELSE 0 END), 0) AS out
            FROM products WHERE active = 1;
            """) ?? new InventorySummaryRow();

        return new InventorySummary
        {
            ValueCents = row.Value,
            ProductCount = row.Products,
            UnitCount = row.Units,
            LowStockCount = row.Low,
            OutOfStockCount = row.Out,
        };
    }

    public static long SaveProduct(ProductInput input, long? userId)
    {
        EnsureWriter();
        ValidateProduct(input);

        try
        {
            return AppState.Db.InTransaction((connection, transaction) =>
            {
                if (input.Id == 0)
                {
                    var stock = input.TrackStock ? Math.Max(0, input.InitialStock) : 0;
                    connection.Execute(
                        """
                        INSERT INTO products
                          (sku, name, description, unit, price_cents, cost_cents, iva_rate,
                           stock, min_stock, track_stock, active)
                        VALUES ($sku, $name, $description, $unit, $price, $cost, $iva,
                                $stock, $minStock, $trackStock, 1);
                        """,
                        new
                        {
                            sku = input.Sku.Trim(),
                            name = input.Name.Trim(),
                            description = input.Description.Trim(),
                            unit = input.Unit.Trim(),
                            price = input.PriceCents,
                            cost = input.CostCents,
                            iva = input.IvaRate,
                            stock,
                            minStock = Math.Max(0, input.MinStock),
                            trackStock = input.TrackStock ? 1 : 0,
                        }, transaction);
                    var id = connection.ExecuteScalar<long>("SELECT last_insert_rowid();", transaction: transaction);
                    if (input.TrackStock && stock > 0)
                    {
                        connection.Execute(
                            """
                            INSERT INTO inventory_movements
                              (product_id, date, type, qty, unit_cost_cents, reason, created_by)
                            VALUES ($productId, $date, 'ajuste', $qty, $cost, 'Saldo inicial', $userId);
                            """,
                            new { productId = id, date = Dates.TodayIso(), qty = stock, cost = input.CostCents, userId },
                            transaction);
                    }
                    Audit(connection, transaction, userId, "create", "product", id, input.Name.Trim());
                    return id;
                }

                var current = connection.QuerySingleOrDefault<Product>(
                    "SELECT * FROM products WHERE id = $id;", new { id = input.Id }, transaction);
                if (current is null) throw new InvalidOperationException("El producto no existe.");

                connection.Execute(
                    """
                    UPDATE products SET sku = $sku, name = $name, description = $description, unit = $unit,
                      price_cents = $price, cost_cents = $cost, iva_rate = $iva,
                      min_stock = $minStock, track_stock = $trackStock
                    WHERE id = $id;
                    """,
                    new
                    {
                        id = input.Id,
                        sku = input.Sku.Trim(),
                        name = input.Name.Trim(),
                        description = input.Description.Trim(),
                        unit = input.Unit.Trim(),
                        price = input.PriceCents,
                        cost = input.CostCents,
                        iva = input.IvaRate,
                        minStock = Math.Max(0, input.MinStock),
                        trackStock = input.TrackStock ? 1 : 0,
                    }, transaction);
                Audit(connection, transaction, userId, "update", "product", input.Id, input.Name.Trim());
                return input.Id;
            });
        }
        catch (SqliteException ex) when (ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ya existe un producto con ese SKU.");
        }
    }

    public static void ToggleActive(long productId, bool active, long? userId)
    {
        EnsureWriter();
        AppState.Db.InTransaction((connection, transaction) =>
        {
            var exists = connection.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM products WHERE id = $id;", new { id = productId }, transaction);
            if (exists == 0) throw new InvalidOperationException("El producto no existe.");
            connection.Execute("UPDATE products SET active = $active WHERE id = $id;",
                new { active = active ? 1 : 0, id = productId }, transaction);
            Audit(connection, transaction, userId, active ? "restore" : "archive", "product", productId, "");
        });
    }

    public static List<InventoryMovement> GetMovements(long productId) =>
        AppState.Db.All<InventoryMovement>(
            """
            SELECT m.*, a.name AS account_name, u.name AS user_name
            FROM inventory_movements m
            LEFT JOIN accounts a ON a.id = m.account_id
            LEFT JOIN users u ON u.id = m.created_by
            WHERE m.product_id = $productId
            ORDER BY m.date DESC, m.id DESC;
            """,
            new { productId });

    public static long AddMovement(InventoryMovementInput input, long? userId)
    {
        EnsureWriter();
        if (!Dates.IsValidIsoDate(input.Date))
            throw new ArgumentException("La fecha del movimiento no es válida.");
        if (input.Type is not ("entrada" or "salida" or "ajuste"))
            throw new ArgumentException("El tipo de movimiento no es válido.");
        if (input.Type != "ajuste" && input.Qty <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a cero.");
        if (input.AccountId is > 0 && input.Type != "entrada")
            throw new ArgumentException("Solo las entradas pueden registrar una compra en efectivo.");

        return AppState.Db.InTransaction((connection, transaction) =>
        {
            var product = connection.QuerySingleOrDefault<Product>(
                "SELECT * FROM products WHERE id = $id;", new { id = input.ProductId }, transaction);
            if (product is null) throw new InvalidOperationException("El producto no existe.");
            if (product.TrackStock != 1)
                throw new InvalidOperationException("Este producto no controla inventario.");

            var delta = 0.0;
            var newStock = product.Stock;
            var newCost = product.CostCents;
            var unitCost = product.CostCents;

            if (input.Type == "entrada")
            {
                delta = input.Qty;
                unitCost = input.UnitCostCents > 0 ? input.UnitCostCents : product.CostCents;
                newStock = product.Stock + delta;
                var baseStock = Math.Max(0, product.Stock);
                newCost = newStock > 0
                    ? (long)Math.Round((baseStock * product.CostCents + delta * unitCost) / newStock,
                        MidpointRounding.AwayFromZero)
                    : unitCost;
            }
            else if (input.Type == "salida")
            {
                if (input.Qty > product.Stock)
                    throw new InvalidOperationException(
                        $"Stock insuficiente: disponible {FormatQty(product.Stock)}, solicitado {FormatQty(input.Qty)}.");
                delta = -input.Qty;
                newStock = product.Stock + delta;
            }
            else
            {
                newStock = input.Qty;
                delta = newStock - product.Stock;
                if (Math.Abs(delta) < 0.000001)
                    throw new InvalidOperationException("El stock real es igual al actual; no hay ajuste.");
            }

            if (newStock < 0)
                throw new InvalidOperationException("El movimiento dejaría el stock en negativo.");

            var reason = input.Reason.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = input.Type switch
                {
                    "entrada" => "Entrada de inventario",
                    "salida" => "Salida manual",
                    _ => "Ajuste de inventario",
                };
            }

            connection.Execute(
                """
                INSERT INTO inventory_movements
                  (product_id, date, type, qty, unit_cost_cents, reason, reference, account_id, created_by)
                VALUES ($productId, $date, $type, $qty, $cost, $reason, $reference, $accountId, $userId);
                """,
                new
                {
                    productId = input.ProductId,
                    date = input.Date,
                    type = input.Type,
                    qty = delta,
                    cost = unitCost,
                    reason,
                    reference = input.Reference.Trim(),
                    accountId = input.AccountId,
                    userId,
                }, transaction);
            var movementId = connection.ExecuteScalar<long>(
                "SELECT last_insert_rowid();", transaction: transaction);

            connection.Execute(
                "UPDATE products SET stock = $stock, cost_cents = $cost WHERE id = $id;",
                new { stock = newStock, cost = newCost, id = input.ProductId }, transaction);

            if (input.Type == "entrada" && input.AccountId is > 0)
            {
                var subtotal = (long)Math.Round(input.Qty * unitCost, MidpointRounding.AwayFromZero);
                if (subtotal <= 0)
                    throw new InvalidOperationException(
                        "Escribe el costo unitario para registrar la compra en efectivo.");
                var accountExists = connection.ExecuteScalar<long>(
                    "SELECT COUNT(*) FROM accounts WHERE id = $id AND active = 1;",
                    new { id = input.AccountId }, transaction);
                if (accountExists == 0)
                    throw new InvalidOperationException("La cuenta de pago no existe o está inactiva.");

                var categoryId = connection.ExecuteScalar<long?>(
                    "SELECT id FROM categories WHERE name = 'Compras de inventario' AND kind = 'gasto' LIMIT 1;",
                    transaction: transaction);
                if (categoryId is null)
                {
                    connection.Execute(
                        "INSERT INTO categories (name, kind) VALUES ('Compras de inventario', 'gasto');",
                        transaction: transaction);
                    categoryId = connection.ExecuteScalar<long>(
                        "SELECT last_insert_rowid();", transaction: transaction);
                }

                var iva = (long)Math.Round(subtotal * input.IvaRate, MidpointRounding.AwayFromZero);
                connection.Execute(
                    """
                    INSERT INTO transactions
                      (date, type, category_id, account_id, description, subtotal_cents, iva_cents,
                       total_cents, iva_rate, method, reference, source, source_id, created_by)
                    VALUES ($date, 'gasto', $categoryId, $accountId, $description, $subtotal, $iva,
                            $total, $ivaRate, 'Compra', $reference, 'inventario', $sourceId, $userId);
                    """,
                    new
                    {
                        date = input.Date,
                        categoryId,
                        accountId = input.AccountId,
                        description = $"Compra de inventario: {product.Name}",
                        subtotal,
                        iva,
                        total = subtotal + iva,
                        ivaRate = input.IvaRate,
                        reference = input.Reference.Trim(),
                        sourceId = movementId,
                        userId,
                    }, transaction);
                var transactionId = connection.ExecuteScalar<long>(
                    "SELECT last_insert_rowid();", transaction: transaction);
                connection.Execute(
                    "UPDATE inventory_movements SET transaction_id = $transactionId WHERE id = $id;",
                    new { transactionId, id = movementId }, transaction);
            }

            Audit(connection, transaction, userId, input.Type, "inventory", movementId,
                $"{product.Name}: {delta:+#,0.###;-#,0.###;0}");
            return movementId;
        });
    }

    private static void ValidateProduct(ProductInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("Escribe el nombre del producto.");
        if (input.Name.Trim().Length > 160)
            throw new ArgumentException("El nombre es demasiado largo.");
        if (input.Sku.Trim().Length > 80)
            throw new ArgumentException("El SKU es demasiado largo.");
        if (input.Description.Trim().Length > 1000)
            throw new ArgumentException("La descripción es demasiado larga.");
        if (string.IsNullOrWhiteSpace(input.Unit))
            throw new ArgumentException("Escribe la unidad.");
        if (input.Unit.Trim().Length > 40)
            throw new ArgumentException("La unidad es demasiado larga.");
        if (input.PriceCents < 0 || input.CostCents < 0)
            throw new ArgumentException("El precio y el costo no pueden ser negativos.");
        if (input.IvaRate is < 0 or > 1)
            throw new ArgumentException("La tasa de IVA no es válida.");
        if (input.MinStock < 0 || input.InitialStock < 0)
            throw new ArgumentException("Las existencias no pueden ser negativas.");
        if (input.Id == 0 && input.TrackStock && input.InitialStock > 1_000_000)
            throw new ArgumentException("El stock inicial es demasiado grande.");
    }

    private static void EnsureWriter()
    {
        if (!AppState.Can(Permissions.ProductsWrite) && !AppState.Can(Permissions.InventoryWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar inventario.");
    }

    private static void Audit(SqliteConnection connection, SqliteTransaction transaction, long? userId,
        string action, string entity, long entityId, string detail)
    {
        connection.Execute(
            """
            INSERT INTO audit_log (user_id, action, entity, entity_id, detail)
            VALUES ($userId, $action, $entity, $entityId, $detail);
            """,
            new { userId, action, entity, entityId = entityId.ToString(), detail }, transaction);
    }

    private static string FormatQty(double qty) =>
        qty.ToString("#,0.###", System.Globalization.CultureInfo.GetCultureInfo("es-MX"));
}
