using Dapper;
using Libro.Desktop.Data;
using Libro.Desktop.Util;

namespace Libro.Desktop.Services;

public sealed class TransactionFilters
{
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Type { get; set; }
    public long? CategoryId { get; set; }
    public string? Query { get; set; }
}

public sealed class TransactionTotals
{
    public long IncomeSubtotal { get; set; }
    public long IncomeIva { get; set; }
    public long ExpenseSubtotal { get; set; }
    public long ExpenseIva { get; set; }
    public long Count { get; set; }

    public long Net => IncomeSubtotal - ExpenseSubtotal;
    public long NetIva => IncomeIva - ExpenseIva;
}

public sealed class TransactionPage
{
    public List<TransactionRow> Rows { get; set; } = new();
    public long Total { get; set; }
}

internal sealed class TransactionTotalsRow
{
    public long IncomeSubtotal { get; set; }
    public long IncomeIva { get; set; }
    public long ExpenseSubtotal { get; set; }
    public long ExpenseIva { get; set; }
    public long Count { get; set; }
}

internal sealed class TransactionSourceRow
{
    public string Source { get; set; } = "";
    public long TotalCents { get; set; }
}

public static class TransactionService
{
    private static (string Where, DynamicParameters Parameters) BuildFilter(TransactionFilters filters)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filters.From))
        {
            where.Add("t.date >= $from");
            parameters.Add("from", filters.From);
        }
        if (!string.IsNullOrWhiteSpace(filters.To))
        {
            where.Add("t.date <= $to");
            parameters.Add("to", filters.To);
        }
        if (filters.Type is "ingreso" or "gasto")
        {
            where.Add("t.type = $type");
            parameters.Add("type", filters.Type);
        }
        if (filters.CategoryId is > 0)
        {
            where.Add("t.category_id = $categoryId");
            parameters.Add("categoryId", filters.CategoryId.Value);
        }
        if (!string.IsNullOrWhiteSpace(filters.Query))
        {
            where.Add("(t.description LIKE $q OR t.reference LIKE $q OR c.name LIKE $q)");
            parameters.Add("q", $"%{filters.Query.Trim()}%");
        }

        return (where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where), parameters);
    }

    public static TransactionPage GetTransactions(TransactionFilters filters, int limit = 100, int offset = 0)
    {
        var (where, parameters) = BuildFilter(filters);
        var total = AppState.Db.ScalarLong(
            $"""
             SELECT COUNT(*)
             FROM transactions t
             LEFT JOIN contacts c ON c.id = t.contact_id
             {where};
             """,
            parameters);

        parameters.Add("limit", limit);
        parameters.Add("offset", offset);

        var rows = AppState.Db.All<TransactionRow>(
            $"""
             SELECT t.*, cat.name AS category_name, cat.kind AS category_kind,
                    c.name AS contact_name, a.name AS account_name, u.name AS created_by_name
             FROM transactions t
             LEFT JOIN categories cat ON cat.id = t.category_id
             LEFT JOIN contacts c ON c.id = t.contact_id
             LEFT JOIN accounts a ON a.id = t.account_id
             LEFT JOIN users u ON u.id = t.created_by
             {where}
             ORDER BY t.date DESC, t.id DESC
             LIMIT $limit OFFSET $offset;
             """,
            parameters);

        return new TransactionPage { Rows = rows, Total = total };
    }

    public static TransactionTotals GetTotals(TransactionFilters filters)
    {
        var (where, parameters) = BuildFilter(filters);
        var row = AppState.Db.One<TransactionTotalsRow>(
            $"""
             SELECT
               COALESCE(SUM(CASE WHEN t.type = 'ingreso' THEN t.subtotal_cents ELSE 0 END), 0) AS income_subtotal,
               COALESCE(SUM(CASE WHEN t.type = 'ingreso' THEN t.iva_cents ELSE 0 END), 0) AS income_iva,
               COALESCE(SUM(CASE WHEN t.type = 'gasto' THEN t.subtotal_cents ELSE 0 END), 0) AS expense_subtotal,
               COALESCE(SUM(CASE WHEN t.type = 'gasto' THEN t.iva_cents ELSE 0 END), 0) AS expense_iva,
               COUNT(*) AS count
             FROM transactions t
             LEFT JOIN contacts c ON c.id = t.contact_id
             {where};
             """,
            parameters) ?? new TransactionTotalsRow();

        return new TransactionTotals
        {
            IncomeSubtotal = row.IncomeSubtotal,
            IncomeIva = row.IncomeIva,
            ExpenseSubtotal = row.ExpenseSubtotal,
            ExpenseIva = row.ExpenseIva,
            Count = row.Count,
        };
    }

    public static TransactionRow? GetTransaction(long id) =>
        AppState.Db.One<TransactionRow>(
            """
            SELECT t.*, cat.name AS category_name, cat.kind AS category_kind,
                   c.name AS contact_name, a.name AS account_name, u.name AS created_by_name
            FROM transactions t
            LEFT JOIN categories cat ON cat.id = t.category_id
            LEFT JOIN contacts c ON c.id = t.contact_id
            LEFT JOIN accounts a ON a.id = t.account_id
            LEFT JOIN users u ON u.id = t.created_by
            WHERE t.id = $id;
            """,
            new { id });

    public static long Save(TransactionRow input, long? userId)
    {
        if (!AppState.Can(Permissions.TxWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar movimientos.");
        if (!DateTime.TryParseExact(input.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _))
            throw new ArgumentException("La fecha no es válida.");
        if (input.Type is not ("ingreso" or "gasto"))
            throw new ArgumentException("Selecciona si es un ingreso o un gasto.");
        if (input.SubtotalCents <= 0)
            throw new ArgumentException("Escribe un monto mayor a cero.");
        if (string.IsNullOrWhiteSpace(input.Description))
            throw new ArgumentException("Escribe una descripción.");
        if (input.CategoryId is null or <= 0)
            throw new ArgumentException("Selecciona una categoría.");
        if (input.AccountId is null or <= 0)
            throw new ArgumentException("Selecciona una cuenta de efectivo.");
        if (input.IvaRate is < 0 or > 1)
            throw new ArgumentException("La tasa de IVA no es válida.");

        var categoryKind = AppState.Db.ScalarString(
            "SELECT kind FROM categories WHERE id = $id AND archived = 0;",
            new { id = input.CategoryId.Value });
        if (categoryKind is null)
            throw new ArgumentException("La categoría seleccionada no existe.");
        if (input.Type == "ingreso" && categoryKind != "ingreso")
            throw new ArgumentException("Un ingreso debe usar una categoría de ingreso.");
        if (input.Type == "gasto" && categoryKind == "ingreso")
            throw new ArgumentException("Un gasto debe usar una categoría de gasto o costo.");

        var accountExists = AppState.Db.ScalarLong(
            "SELECT COUNT(*) FROM accounts WHERE id = $id AND active = 1;",
            new { id = input.AccountId.Value });
        if (accountExists == 0)
            throw new ArgumentException("La cuenta de efectivo no existe o está inactiva.");

        if (input.ContactId is > 0)
        {
            var contactExists = AppState.Db.ScalarLong(
                "SELECT COUNT(*) FROM contacts WHERE id = $id;",
                new { id = input.ContactId.Value });
            if (contactExists == 0)
                throw new ArgumentException("El contacto seleccionado no existe.");
        }

        var iva = (long)Math.Round(input.SubtotalCents * input.IvaRate, MidpointRounding.AwayFromZero);
        var total = input.SubtotalCents + iva;

        return AppState.Db.InTransaction((connection, transaction) =>
        {
            long id;
            if (input.Id == 0)
            {
                connection.Execute(
                    """
                    INSERT INTO transactions
                      (date, type, category_id, contact_id, account_id, description, subtotal_cents,
                       iva_cents, total_cents, iva_rate, method, reference, source, created_by)
                    VALUES ($date, $type, $categoryId, $contactId, $accountId, $description,
                            $subtotal, $iva, $total, $ivaRate, $method, $reference, 'manual', $userId);
                    """,
                    new
                    {
                        date = input.Date,
                        type = input.Type,
                        categoryId = input.CategoryId,
                        contactId = input.ContactId,
                        accountId = input.AccountId,
                        description = input.Description.Trim(),
                        subtotal = input.SubtotalCents,
                        iva,
                        total,
                        ivaRate = input.IvaRate,
                        method = input.Method?.Trim() ?? "",
                        reference = input.Reference?.Trim() ?? "",
                        userId,
                    }, transaction);
                id = connection.ExecuteScalar<long>("SELECT last_insert_rowid();", transaction: transaction);
                connection.Execute(
                    "INSERT INTO audit_log (user_id, action, entity, entity_id, detail) VALUES ($userId, 'create', 'transaction', $id, $detail);",
                    new { userId, id = id.ToString(), detail = total.ToString() }, transaction);
            }
            else
            {
                var source = connection.ExecuteScalar<string>(
                    "SELECT source FROM transactions WHERE id = $id;",
                    new { id = input.Id }, transaction);
                if (source is null)
                    throw new InvalidOperationException("El movimiento no existe.");
                if (source != "manual")
                    throw new InvalidOperationException("Este movimiento se generó automáticamente y no se puede editar.");

                id = input.Id;
                connection.Execute(
                    """
                    UPDATE transactions SET
                      date = $date, type = $type, category_id = $categoryId, contact_id = $contactId,
                      account_id = $accountId, description = $description, subtotal_cents = $subtotal,
                      iva_cents = $iva, total_cents = $total, iva_rate = $ivaRate, method = $method,
                      reference = $reference, updated_at = datetime('now')
                    WHERE id = $id;
                    """,
                    new
                    {
                        id,
                        date = input.Date,
                        type = input.Type,
                        categoryId = input.CategoryId,
                        contactId = input.ContactId,
                        accountId = input.AccountId,
                        description = input.Description.Trim(),
                        subtotal = input.SubtotalCents,
                        iva,
                        total,
                        ivaRate = input.IvaRate,
                        method = input.Method?.Trim() ?? "",
                        reference = input.Reference?.Trim() ?? "",
                    }, transaction);
                connection.Execute(
                    "INSERT INTO audit_log (user_id, action, entity, entity_id, detail) VALUES ($userId, 'update', 'transaction', $id, $detail);",
                    new { userId, id = id.ToString(), detail = total.ToString() }, transaction);
            }

            return id;
        });
    }

    public static void Delete(long id, long? userId)
    {
        if (!AppState.Can(Permissions.TxWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar movimientos.");

        AppState.Db.InTransaction((connection, transaction) =>
        {
            var current = connection.QueryFirstOrDefault<TransactionSourceRow>(
                "SELECT source, total_cents FROM transactions WHERE id = $id;",
                new { id }, transaction);
            if (current is null)
                throw new InvalidOperationException("El movimiento no existe.");
            if (current.Source != "manual")
                throw new InvalidOperationException("Este movimiento se generó automáticamente y no se puede eliminar.");

            connection.Execute("DELETE FROM transactions WHERE id = $id;", new { id }, transaction);
            connection.Execute(
                "INSERT INTO audit_log (user_id, action, entity, entity_id, detail) VALUES ($userId, 'delete', 'transaction', $id, $detail);",
                new { userId, id = id.ToString(), detail = current.TotalCents.ToString() }, transaction);
        });
    }

    public static string RangeLabel(DateTime date) => date.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-MX"));
}
