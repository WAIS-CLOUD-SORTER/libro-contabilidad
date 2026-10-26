using System.Globalization;
using Dapper;
using Libro.Desktop.Data;
using Libro.Desktop.Util;
using Microsoft.Data.Sqlite;

namespace Libro.Desktop.Services;

public sealed class InvoiceLineInput
{
    public long? ProductId { get; set; }
    public string Description { get; set; } = "";
    public double Qty { get; set; } = 1;
    public long UnitPriceCents { get; set; }
    public double IvaRate { get; set; }

    public long SubtotalCents => (long)Math.Round(Qty * UnitPriceCents, MidpointRounding.AwayFromZero);
    public long IvaCents => (long)Math.Round(SubtotalCents * IvaRate, MidpointRounding.AwayFromZero);
    public long TotalCents => SubtotalCents + IvaCents;
}

public sealed class InvoiceInput
{
    public long Id { get; set; }
    public long ContactId { get; set; }
    public string Date { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string Notes { get; set; } = "";
    public string PaymentTerms { get; set; } = "";
    public List<InvoiceLineInput> Lines { get; set; } = new();
}

public sealed class InvoiceFilters
{
    public string? Status { get; set; }
    public string? Query { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
}

public sealed class InvoicePage
{
    public List<Invoice> Rows { get; set; } = new();
    public long Total { get; set; }
}

public sealed class InvoiceOverview
{
    public long ReceivableCents { get; set; }
    public long OverdueCents { get; set; }
    public long OverdueCount { get; set; }
    public long MonthTotalCents { get; set; }
    public long MonthCount { get; set; }
    public long DraftCount { get; set; }
}

internal sealed class InvoiceTotalsRow
{
    public long SubtotalCents { get; set; }
    public long IvaCents { get; set; }
    public long TotalCents { get; set; }
}

internal sealed class InvoiceStockRow
{
    public long ProductId { get; set; }
    public string Name { get; set; } = "";
    public double Stock { get; set; }
    public double Needed { get; set; }
}

internal sealed class InvoiceOverviewRow
{
    public long Receivable { get; set; }
    public long Overdue { get; set; }
    public long OverdueCount { get; set; }
    public long MonthTotal { get; set; }
    public long MonthCount { get; set; }
    public long DraftCount { get; set; }
}

internal sealed class InvoiceProductItem
{
    public long ProductId { get; set; }
    public double Qty { get; set; }
    public long UnitCostCents { get; set; }
}

internal sealed class InvoiceNumberRow
{
    public long NextNumber { get; set; }
    public string Series { get; set; } = "";
}

public static class InvoiceService
{
    public static InvoicePage GetInvoices(InvoiceFilters filters, int limit = 100, int offset = 0)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (filters.Status is "borrador" or "emitida" or "pagada" or "cancelada")
        {
            where.Add("i.status = $status");
            parameters.Add("status", filters.Status);
        }
        if (!string.IsNullOrWhiteSpace(filters.From))
        {
            where.Add("i.date >= $from");
            parameters.Add("from", filters.From);
        }
        if (!string.IsNullOrWhiteSpace(filters.To))
        {
            where.Add("i.date <= $to");
            parameters.Add("to", filters.To);
        }
        if (!string.IsNullOrWhiteSpace(filters.Query))
        {
            where.Add("(c.name LIKE $q OR CAST(i.number AS TEXT) LIKE $q OR i.notes LIKE $q)");
            parameters.Add("q", $"%{filters.Query.Trim()}%");
        }

        var whereSql = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        var total = AppState.Db.ScalarLong(
            $"SELECT COUNT(*) FROM invoices i JOIN contacts c ON c.id = i.contact_id {whereSql};",
            parameters);

        parameters.Add("limit", limit);
        parameters.Add("offset", offset);
        var rows = AppState.Db.All<Invoice>(
            $"""
             SELECT i.*, c.name AS contact_name, c.rfc AS contact_rfc, u.name AS created_by_name
             FROM invoices i
             JOIN contacts c ON c.id = i.contact_id
             LEFT JOIN users u ON u.id = i.created_by
             {whereSql}
             ORDER BY i.date DESC, i.id DESC
             LIMIT $limit OFFSET $offset;
             """,
            parameters);

        var today = Dates.TodayIso();
        foreach (var row in rows)
            row.IsOverdue = row.Status == "emitida" && string.CompareOrdinal(row.DueDate, today) < 0;

        return new InvoicePage { Rows = rows, Total = total };
    }

    public static Invoice? GetInvoice(long id)
    {
        var invoice = AppState.Db.One<Invoice>(
            """
            SELECT i.*, c.name AS contact_name, c.rfc AS contact_rfc, u.name AS created_by_name
            FROM invoices i
            JOIN contacts c ON c.id = i.contact_id
            LEFT JOIN users u ON u.id = i.created_by
            WHERE i.id = $id;
            """,
            new { id });
        if (invoice is not null)
            invoice.IsOverdue = invoice.Status == "emitida" &&
                                string.CompareOrdinal(invoice.DueDate, Dates.TodayIso()) < 0;
        return invoice;
    }

    public static List<InvoiceItem> GetItems(long invoiceId) =>
        AppState.Db.All<InvoiceItem>(
            """
            SELECT ii.*, p.sku AS sku, p.unit AS unit
            FROM invoice_items ii
            LEFT JOIN products p ON p.id = ii.product_id
            WHERE ii.invoice_id = $invoiceId
            ORDER BY ii.position, ii.id;
            """,
            new { invoiceId });

    public static List<Payment> GetPayments(long invoiceId) =>
        AppState.Db.All<Payment>(
            """
            SELECT p.*, a.name AS account_name, u.name AS created_by_name
            FROM payments p
            LEFT JOIN accounts a ON a.id = p.account_id
            LEFT JOIN users u ON u.id = p.created_by
            WHERE p.invoice_id = $invoiceId
            ORDER BY p.date, p.id;
            """,
            new { invoiceId });

    public static InvoiceOverview GetOverview()
    {
        var (start, end) = Dates.CurrentMonthRange();
        var today = Dates.TodayIso();
        var row = AppState.Db.One<InvoiceOverviewRow>(
            """
            SELECT
              COALESCE(SUM(CASE WHEN status = 'emitida' THEN total_cents - paid_cents ELSE 0 END), 0) AS receivable,
              COALESCE(SUM(CASE WHEN status = 'emitida' AND due_date < $today THEN total_cents - paid_cents ELSE 0 END), 0) AS overdue,
              COALESCE(SUM(CASE WHEN status = 'emitida' AND due_date < $today THEN 1 ELSE 0 END), 0) AS overdue_count,
              COALESCE(SUM(CASE WHEN status IN ('emitida','pagada') AND date BETWEEN $start AND $end THEN total_cents ELSE 0 END), 0) AS month_total,
              COALESCE(SUM(CASE WHEN status IN ('emitida','pagada') AND date BETWEEN $start AND $end THEN 1 ELSE 0 END), 0) AS month_count,
              COALESCE(SUM(CASE WHEN status = 'borrador' THEN 1 ELSE 0 END), 0) AS draft_count
            FROM invoices;
            """,
            new { today, start, end }) ?? new InvoiceOverviewRow();

        return new InvoiceOverview
        {
            ReceivableCents = row.Receivable,
            OverdueCents = row.Overdue,
            OverdueCount = row.OverdueCount,
            MonthTotalCents = row.MonthTotal,
            MonthCount = row.MonthCount,
            DraftCount = row.DraftCount,
        };
    }

    public static long Save(InvoiceInput input, bool emit, long? userId)
    {
        if (!AppState.Can(Permissions.InvoiceWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar facturas.");
        Validate(input);

        return AppState.Db.InTransaction((connection, transaction) =>
        {
            EnsureContact(connection, transaction, input.ContactId);
            var totals = ComputeTotals(input.Lines);
            long invoiceId;

            if (input.Id == 0)
            {
                var series = connection.ExecuteScalar<string>(
                    "SELECT invoice_series FROM company WHERE id = 1;", transaction: transaction) ?? "A";
                if (string.IsNullOrWhiteSpace(series)) series = "A";

                connection.Execute(
                    """
                    INSERT INTO invoices
                      (series, number, contact_id, date, due_date, status, subtotal_cents, iva_cents,
                       total_cents, notes, payment_terms, created_by)
                    VALUES ($series, NULL, $contactId, $date, $dueDate, 'borrador', $subtotal, $iva,
                            $total, $notes, $terms, $userId);
                    """,
                    new
                    {
                        series,
                        contactId = input.ContactId,
                        date = input.Date,
                        dueDate = input.DueDate,
                        subtotal = totals.SubtotalCents,
                        iva = totals.IvaCents,
                        total = totals.TotalCents,
                        notes = input.Notes.Trim(),
                        terms = input.PaymentTerms.Trim(),
                        userId,
                    }, transaction);
                invoiceId = connection.ExecuteScalar<long>("SELECT last_insert_rowid();", transaction: transaction);
                InsertItems(connection, transaction, invoiceId, input.Lines);
                Audit(connection, transaction, userId, "create", invoiceId, $"Total {totals.TotalCents}");
            }
            else
            {
                invoiceId = input.Id;
                var status = connection.ExecuteScalar<string>(
                    "SELECT status FROM invoices WHERE id = $id;", new { id = invoiceId }, transaction);
                if (status is null)
                    throw new InvalidOperationException("La factura no existe.");
                if (status != "borrador")
                    throw new InvalidOperationException("Solo puedes editar facturas en borrador.");

                connection.Execute(
                    """
                    UPDATE invoices SET contact_id = $contactId, date = $date, due_date = $dueDate,
                      notes = $notes, payment_terms = $terms, subtotal_cents = $subtotal,
                      iva_cents = $iva, total_cents = $total, updated_at = datetime('now')
                    WHERE id = $id;
                    """,
                    new
                    {
                        id = invoiceId,
                        contactId = input.ContactId,
                        date = input.Date,
                        dueDate = input.DueDate,
                        notes = input.Notes.Trim(),
                        terms = input.PaymentTerms.Trim(),
                        subtotal = totals.SubtotalCents,
                        iva = totals.IvaCents,
                        total = totals.TotalCents,
                    }, transaction);
                connection.Execute("DELETE FROM invoice_items WHERE invoice_id = $id;",
                    new { id = invoiceId }, transaction);
                InsertItems(connection, transaction, invoiceId, input.Lines);
                Audit(connection, transaction, userId, "update", invoiceId, $"Total {totals.TotalCents}");
            }

            if (emit) EmitInternal(connection, transaction, invoiceId, userId);
            return invoiceId;
        });
    }

    public static void Emit(long invoiceId, long? userId)
    {
        if (!AppState.Can(Permissions.InvoiceWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar facturas.");

        AppState.Db.InTransaction((connection, transaction) =>
            EmitInternal(connection, transaction, invoiceId, userId));
    }

    private static void EmitInternal(SqliteConnection connection, SqliteTransaction transaction,
        long invoiceId, long? userId)
    {
        var invoice = connection.QuerySingleOrDefault<Invoice>(
            "SELECT * FROM invoices WHERE id = $id;", new { id = invoiceId }, transaction);
        if (invoice is null) throw new InvalidOperationException("La factura no existe.");
        if (invoice.Status != "borrador")
            throw new InvalidOperationException("Esta factura ya no se puede emitir.");
        EnsureContact(connection, transaction, invoice.ContactId);

        var itemCount = connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM invoice_items WHERE invoice_id = $id;", new { id = invoiceId }, transaction);
        if (itemCount == 0)
            throw new InvalidOperationException("Agrega al menos un concepto antes de emitir.");

        var totals = connection.QuerySingle<InvoiceTotalsRow>(
            """
            SELECT COALESCE(SUM(subtotal_cents),0) AS subtotal_cents,
                   COALESCE(SUM(iva_cents),0) AS iva_cents,
                   COALESCE(SUM(total_cents),0) AS total_cents
            FROM invoice_items WHERE invoice_id = $id;
            """, new { id = invoiceId }, transaction);

        var stockRows = connection.Query<InvoiceStockRow>(
            """
            SELECT p.id AS product_id, p.name, p.stock, SUM(ii.qty) AS needed
            FROM invoice_items ii
            JOIN products p ON p.id = ii.product_id
            WHERE ii.invoice_id = $id AND p.track_stock = 1
            GROUP BY p.id
            HAVING SUM(ii.qty) > p.stock;
            """, new { id = invoiceId }, transaction).ToList();
        if (stockRows.Count > 0)
        {
            var missing = stockRows[0];
            throw new InvalidOperationException(
                $"Stock insuficiente de {missing.Name}: disponible {FormatQty(missing.Stock)}, solicitado {FormatQty(missing.Needed)}.");
        }

        var company = connection.QuerySingle<InvoiceNumberRow>(
            "SELECT next_invoice_number AS next_number, invoice_series AS series FROM company WHERE id = 1;",
            transaction: transaction);
        var number = company.NextNumber <= 0 ? 1 : company.NextNumber;
        var series = string.IsNullOrWhiteSpace(invoice.Series) ? company.Series : invoice.Series;
        if (string.IsNullOrWhiteSpace(series)) series = "A";
        while (connection.ExecuteScalar<long>(
                   "SELECT COUNT(*) FROM invoices WHERE series = $series AND number = $number;",
                   new { series, number }, transaction) > 0)
            number++;

        connection.Execute(
            """
            UPDATE invoices SET number = $number, series = $series, status = 'emitida',
              subtotal_cents = $subtotal, iva_cents = $iva, total_cents = $total,
              updated_at = datetime('now')
            WHERE id = $id;
            """,
            new
            {
                id = invoiceId, series, number,
                subtotal = totals.SubtotalCents, iva = totals.IvaCents, total = totals.TotalCents,
            }, transaction);
        connection.Execute("UPDATE company SET next_invoice_number = $next WHERE id = 1;",
            new { next = number + 1 }, transaction);

        var productItems = connection.Query<InvoiceProductItem>(
            """
            SELECT product_id, qty, unit_cost_cents
            FROM invoice_items WHERE invoice_id = $id AND product_id IS NOT NULL;
            """, new { id = invoiceId }, transaction).ToList();

        var folio = $"{series}-{number:0000}";
        foreach (var item in productItems)
        {
            var product = connection.QuerySingleOrDefault<Product>(
                "SELECT * FROM products WHERE id = $id;", new { id = item.ProductId }, transaction);
            if (product is null) continue;
            if (product.TrackStock == 1)
            {
                connection.Execute("UPDATE products SET stock = stock - $qty WHERE id = $id;",
                    new { qty = item.Qty, id = item.ProductId }, transaction);
            }
            connection.Execute(
                """
                INSERT INTO inventory_movements
                  (product_id, date, type, qty, unit_cost_cents, reason, reference, created_by)
                VALUES ($productId, $date, 'salida', $qty, $cost, 'Venta facturada', $reference, $userId);
                """,
                new
                {
                    productId = item.ProductId,
                    date = invoice.Date,
                    qty = -Math.Abs(item.Qty),
                    cost = item.UnitCostCents,
                    reference = $"Factura {folio}",
                    userId,
                }, transaction);
        }

        Audit(connection, transaction, userId, "emit", invoiceId, folio);
    }

    public static void Cancel(long invoiceId, string reason, long? userId)
    {
        if (!AppState.Can(Permissions.InvoiceWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar facturas.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Escribe el motivo de la cancelación.");

        AppState.Db.InTransaction((connection, transaction) =>
        {
            var invoice = connection.QuerySingleOrDefault<Invoice>(
                "SELECT * FROM invoices WHERE id = $id;", new { id = invoiceId }, transaction);
            if (invoice is null) throw new InvalidOperationException("La factura no existe.");
            if (invoice.Status == "cancelada")
                throw new InvalidOperationException("Esta factura ya está cancelada.");
            if (invoice.PaidCents > 0)
                throw new InvalidOperationException(
                    "No puedes cancelar una factura con pagos registrados. Elimina los pagos primero.");

            if (invoice.Status != "borrador")
            {
                var productItems = connection.Query<InvoiceProductItem>(
                    """
                    SELECT product_id, qty, unit_cost_cents
                    FROM invoice_items WHERE invoice_id = $id AND product_id IS NOT NULL;
                    """, new { id = invoiceId }, transaction).ToList();
                var folio = $"{invoice.Series}-{(invoice.Number ?? 0):0000}";
                foreach (var item in productItems)
                {
                    var product = connection.QuerySingleOrDefault<Product>(
                        "SELECT * FROM products WHERE id = $id;", new { id = item.ProductId }, transaction);
                    if (product is null) continue;
                    if (product.TrackStock == 1)
                    {
                        connection.Execute("UPDATE products SET stock = stock + $qty WHERE id = $id;",
                            new { qty = item.Qty, id = item.ProductId }, transaction);
                    }
                    connection.Execute(
                        """
                        INSERT INTO inventory_movements
                          (product_id, date, type, qty, unit_cost_cents, reason, reference, created_by)
                        VALUES ($productId, $date, 'entrada', $qty, $cost, 'Cancelación de factura', $reference, $userId);
                        """,
                        new
                        {
                            productId = item.ProductId,
                            date = Dates.TodayIso(),
                            qty = Math.Abs(item.Qty),
                            cost = item.UnitCostCents,
                            reference = $"Factura {folio}",
                            userId,
                        }, transaction);
                }
            }

            connection.Execute(
                """
                UPDATE invoices SET status = 'cancelada', cancelled_reason = $reason,
                  cancelled_at = datetime('now'), updated_at = datetime('now')
                WHERE id = $id;
                """, new { reason = reason.Trim(), id = invoiceId }, transaction);
            Audit(connection, transaction, userId, "cancel", invoiceId, reason.Trim());
        });
    }

    public static void DeleteDraft(long invoiceId, long? userId)
    {
        if (!AppState.Can(Permissions.InvoiceWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar facturas.");

        AppState.Db.InTransaction((connection, transaction) =>
        {
            var status = connection.ExecuteScalar<string>(
                "SELECT status FROM invoices WHERE id = $id;", new { id = invoiceId }, transaction);
            if (status is null) throw new InvalidOperationException("La factura no existe.");
            if (status != "borrador")
                throw new InvalidOperationException("Solo puedes eliminar facturas en borrador.");

            connection.Execute("DELETE FROM invoice_items WHERE invoice_id = $id;",
                new { id = invoiceId }, transaction);
            connection.Execute("DELETE FROM invoices WHERE id = $id;", new { id = invoiceId }, transaction);
            Audit(connection, transaction, userId, "delete", invoiceId, "");
        });
    }

    public static void RegisterPayment(long invoiceId, string date, long amountCents, long? accountId,
        string method, string reference, string notes, long? userId)
    {
        if (!AppState.Can(Permissions.InvoiceWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar facturas.");
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
            throw new ArgumentException("La fecha del pago no es válida.");
        if (amountCents <= 0)
            throw new ArgumentException("El pago debe ser mayor a cero.");

        AppState.Db.InTransaction((connection, transaction) =>
        {
            var invoice = connection.QuerySingleOrDefault<Invoice>(
                "SELECT * FROM invoices WHERE id = $id;", new { id = invoiceId }, transaction);
            if (invoice is null) throw new InvalidOperationException("La factura no existe.");
            if (invoice.Status is not ("emitida" or "pagada"))
                throw new InvalidOperationException("Solo puedes registrar pagos de facturas emitidas.");

            var outstanding = invoice.TotalCents - invoice.PaidCents;
            if (amountCents > outstanding)
                throw new InvalidOperationException(
                    $"El pago excede el saldo pendiente ({Money.Format(outstanding)}).");

            if (accountId is > 0)
            {
                var exists = connection.ExecuteScalar<long>(
                    "SELECT COUNT(*) FROM accounts WHERE id = $id;", new { id = accountId }, transaction);
                if (exists == 0)
                    throw new InvalidOperationException("La cuenta de efectivo no existe.");
            }

            connection.Execute(
                """
                INSERT INTO payments
                  (invoice_id, date, amount_cents, account_id, method, reference, notes, created_by)
                VALUES ($invoiceId, $date, $amount, $accountId, $method, $reference, $notes, $userId);
                """,
                new
                {
                    invoiceId, date, amount = amountCents, accountId,
                    method = method.Trim(), reference = reference.Trim(), notes = notes.Trim(), userId,
                }, transaction);

            var newPaid = invoice.PaidCents + amountCents;
            connection.Execute(
                "UPDATE invoices SET paid_cents = $paid, status = $status, updated_at = datetime('now') WHERE id = $id;",
                new
                {
                    paid = newPaid,
                    status = newPaid >= invoice.TotalCents ? "pagada" : "emitida",
                    id = invoiceId,
                }, transaction);
            Audit(connection, transaction, userId, "payment", invoiceId, amountCents.ToString());
        });
    }

    public static void DeletePayment(long paymentId, long? userId)
    {
        if (!AppState.Can(Permissions.InvoiceWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar facturas.");

        AppState.Db.InTransaction((connection, transaction) =>
        {
            var payment = connection.QuerySingleOrDefault<Payment>(
                "SELECT id, invoice_id, amount_cents FROM payments WHERE id = $id;",
                new { id = paymentId }, transaction);
            if (payment is null) throw new InvalidOperationException("El pago no existe.");

            var invoice = connection.QuerySingleOrDefault<Invoice>(
                "SELECT * FROM invoices WHERE id = $id;", new { id = payment.InvoiceId }, transaction);
            if (invoice is null) throw new InvalidOperationException("La factura no existe.");
            if (invoice.Status == "cancelada")
                throw new InvalidOperationException("La factura está cancelada.");

            connection.Execute("DELETE FROM payments WHERE id = $id;", new { id = paymentId }, transaction);
            var newPaid = Math.Max(0, invoice.PaidCents - payment.AmountCents);
            connection.Execute(
                "UPDATE invoices SET paid_cents = $paid, status = $status, updated_at = datetime('now') WHERE id = $id;",
                new
                {
                    paid = newPaid,
                    status = newPaid >= invoice.TotalCents && invoice.TotalCents > 0 ? "pagada" : "emitida",
                    id = invoice.Id,
                }, transaction);
            Audit(connection, transaction, userId, "delete_payment", invoice.Id, payment.AmountCents.ToString());
        });
    }

    private static void Validate(InvoiceInput input)
    {
        if (input.ContactId <= 0)
            throw new ArgumentException("Selecciona un cliente.");
        if (!DateTime.TryParseExact(input.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            throw new ArgumentException("La fecha de la factura no es válida.");
        if (!DateTime.TryParseExact(input.DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dueDate))
            throw new ArgumentException("La fecha de vencimiento no es válida.");
        if (dueDate < date)
            throw new ArgumentException("La fecha de vencimiento no puede ser anterior a la fecha de la factura.");
        if (input.Lines.Count == 0)
            throw new ArgumentException("Agrega al menos un concepto.");
        if (input.Lines.Count > 100)
            throw new ArgumentException("Demasiados conceptos en una factura.");

        foreach (var line in input.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Description))
                throw new ArgumentException("Cada concepto necesita una descripción.");
            if (line.Description.Trim().Length > 300)
                throw new ArgumentException("La descripción de un concepto es demasiado larga.");
            if (line.Qty <= 0 || line.Qty > 1_000_000)
                throw new ArgumentException("La cantidad debe ser mayor a cero.");
            if (line.UnitPriceCents < 0)
                throw new ArgumentException("El precio no puede ser negativo.");
            if (line.IvaRate is < 0 or > 1)
                throw new ArgumentException("La tasa de IVA no es válida.");
        }
    }

    private static void EnsureContact(SqliteConnection connection, SqliteTransaction transaction, long contactId)
    {
        var exists = connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM contacts WHERE id = $id;", new { id = contactId }, transaction);
        if (exists == 0)
            throw new InvalidOperationException("El cliente seleccionado no existe.");
    }

    private static InvoiceTotalsRow ComputeTotals(IEnumerable<InvoiceLineInput> lines)
    {
        var totals = new InvoiceTotalsRow();
        foreach (var line in lines)
        {
            totals.SubtotalCents += line.SubtotalCents;
            totals.IvaCents += line.IvaCents;
            totals.TotalCents += line.TotalCents;
        }
        return totals;
    }

    private static void InsertItems(SqliteConnection connection, SqliteTransaction transaction,
        long invoiceId, IReadOnlyList<InvoiceLineInput> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var cost = 0L;
            if (line.ProductId is > 0)
            {
                cost = connection.ExecuteScalar<long?>(
                    "SELECT cost_cents FROM products WHERE id = $id;",
                    new { id = line.ProductId }, transaction) ?? 0;
            }

            connection.Execute(
                """
                INSERT INTO invoice_items
                  (invoice_id, product_id, position, description, qty, unit_price_cents, iva_rate,
                   subtotal_cents, iva_cents, total_cents, unit_cost_cents)
                VALUES ($invoiceId, $productId, $position, $description, $qty, $price, $rate,
                        $subtotal, $iva, $total, $cost);
                """,
                new
                {
                    invoiceId,
                    productId = line.ProductId,
                    position = index,
                    description = line.Description.Trim(),
                    qty = line.Qty,
                    price = line.UnitPriceCents,
                    rate = line.IvaRate,
                    subtotal = line.SubtotalCents,
                    iva = line.IvaCents,
                    total = line.TotalCents,
                    cost,
                }, transaction);
        }
    }

    private static void Audit(SqliteConnection connection, SqliteTransaction transaction,
        long? userId, string action, long invoiceId, string detail)
    {
        connection.Execute(
            "INSERT INTO audit_log (user_id, action, entity, entity_id, detail) VALUES ($userId, $action, 'invoice', $id, $detail);",
            new { userId, action, id = invoiceId.ToString(), detail }, transaction);
    }

    private static string FormatQty(double qty) =>
        qty.ToString("#,0.###", CultureInfo.GetCultureInfo("es-MX"));
}
