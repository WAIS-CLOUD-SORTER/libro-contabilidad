using Dapper;
using Libro.Desktop.Data;
using Libro.Desktop.Payroll;
using Libro.Desktop.Util;
using Microsoft.Data.Sqlite;

namespace Libro.Desktop.Services;

public sealed class PayrollSummary
{
    public long DraftCount { get; set; }
    public long PaidCount { get; set; }
    public long DraftNetCents { get; set; }
    public long PaidThisMonthCents { get; set; }
    public long EmployeeCount { get; set; }
}

internal sealed class PayrollTransactionSource
{
    public string? Source { get; set; }
    public long? SourceId { get; set; }
}

internal sealed class PayrollCalculation
{
    public long Days { get; set; }
    public long BaseCents { get; set; }
    public long IsrCents { get; set; }
    public long ImssCents { get; set; }
    public long NetCents { get; set; }
}

public static class PayrollService
{
    public static List<PayrollPeriod> GetPeriods()
    {
        return AppState.Db.All<PayrollPeriod>(
            """
            SELECT p.*,
                   COUNT(i.id) AS employee_count,
                   COALESCE(SUM(i.net_cents), 0) AS total_net_cents,
                   COALESCE(SUM(i.isr_cents + i.imss_cents + i.infonavit_cents + i.other_deductions_cents), 0)
                     AS total_deductions_cents
            FROM payroll_periods p
            LEFT JOIN payroll_items i ON i.period_id = p.id
            GROUP BY p.id
            ORDER BY p.start_date DESC, p.id DESC;
            """);
    }

    public static PayrollPeriod? GetPeriod(long id)
    {
        return AppState.Db.One<PayrollPeriod>(
            """
            SELECT p.*,
                   COUNT(i.id) AS employee_count,
                   COALESCE(SUM(i.net_cents), 0) AS total_net_cents,
                   COALESCE(SUM(i.isr_cents + i.imss_cents + i.infonavit_cents + i.other_deductions_cents), 0)
                     AS total_deductions_cents
            FROM payroll_periods p
            LEFT JOIN payroll_items i ON i.period_id = p.id
            WHERE p.id = $id
            GROUP BY p.id;
            """, new { id });
    }

    public static List<PayrollItem> GetItems(long periodId)
    {
        return AppState.Db.All<PayrollItem>(
            """
            SELECT i.*, e.name AS employee_name, e.position AS employee_position
            FROM payroll_items i
            JOIN employees e ON e.id = i.employee_id
            WHERE i.period_id = $periodId
            ORDER BY e.name;
            """, new { periodId });
    }

    public static PayrollSummary GetSummary()
    {
        var (start, end) = Dates.CurrentMonthRange();
        var row = AppState.Db.One<PayrollSummary>(
            """
            SELECT
              (SELECT COUNT(*) FROM payroll_periods WHERE status = 'borrador') AS draft_count,
              (SELECT COUNT(*) FROM payroll_periods WHERE status = 'pagada') AS paid_count,
              (SELECT COALESCE(SUM(i.net_cents), 0)
                 FROM payroll_items i JOIN payroll_periods p ON p.id = i.period_id
                WHERE p.status = 'borrador') AS draft_net_cents,
              (SELECT COALESCE(SUM(i.net_cents), 0)
                 FROM payroll_items i JOIN payroll_periods p ON p.id = i.period_id
                WHERE p.status = 'pagada' AND p.paid_at >= $start AND p.paid_at < $end) AS paid_this_month_cents,
              (SELECT COUNT(*) FROM employees WHERE status = 'activo') AS employee_count;
            """,
            new { start = start + " 00:00:00", end = end + " 23:59:59" }) ?? new PayrollSummary();
        return row;
    }

    public static long CreatePeriod(int year, int month, string periodType, int quincena)
    {
        EnsureWriter();
        if (year is < 2000 or > 2200)
            throw new ArgumentException("El año de la nómina no es válido.");
        if (month is < 1 or > 12)
            throw new ArgumentException("El mes de la nómina no es válido.");
        if (periodType is not ("mensual" or "quincenal"))
            throw new ArgumentException("El tipo de periodo no es válido.");
        if (periodType == "quincenal" && quincena is < 1 or > 2)
            throw new ArgumentException("Selecciona la quincena.");
        var q = periodType == "quincenal" ? quincena : 0;
        var (monthStart, monthEnd) = Dates.MonthRange(year, month);
        var start = periodType == "quincenal"
            ? (q == 1 ? monthStart : $"{year:0000}-{month:00}-16")
            : monthStart;
        var end = periodType == "quincenal"
            ? (q == 1 ? $"{year:0000}-{month:00}-15" : monthEnd)
            : monthEnd;

        try
        {
            return AppState.Db.InTransaction((connection, transaction) =>
            {
                connection.Execute(
                    """
                    INSERT INTO payroll_periods
                      (year, month, period_type, quincena, start_date, end_date, status, created_by)
                    VALUES ($year, $month, $type, $quincena, $start, $end, 'borrador', $userId);
                    """,
                    new { year, month, type = periodType, quincena = q, start, end, userId = AppState.UserId },
                    transaction);
                var id = connection.ExecuteScalar<long>(
                    "SELECT last_insert_rowid();", transaction: transaction);

                CalculateInto(connection, transaction, id, start, end, periodType, new Dictionary<long, PayrollItem>());

                connection.Execute(
                    """
                    INSERT INTO audit_log (user_id, action, entity, entity_id, detail)
                    VALUES ($userId, 'create', 'payroll', $id, $detail);
                    """,
                    new
                    {
                        userId = AppState.UserId,
                        id = id.ToString(),
                        detail = $"{Dates.PayrollPeriodLabel(year, month, periodType, q)}",
                    }, transaction);
                return id;
            });
        }
        catch (SqliteException ex) when (ex.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ya existe una nómina para ese periodo.");
        }
    }

    public static void Recalculate(long periodId)
    {
        EnsureWriter();
        AppState.Db.InTransaction((connection, transaction) =>
        {
            var period = GetPeriodForEdit(connection, transaction, periodId);
            if (period.Status == "pagada")
                throw new InvalidOperationException("La nómina ya fue pagada; no se puede recalcular.");

            var existing = connection.Query<PayrollItem>(
                "SELECT * FROM payroll_items WHERE period_id = $periodId;",
                new { periodId }, transaction).ToDictionary(item => item.EmployeeId);
            CalculateInto(connection, transaction, periodId, period.StartDate, period.EndDate,
                period.PeriodType, existing);
            connection.Execute(
                """
                INSERT INTO audit_log (user_id, action, entity, entity_id, detail)
                VALUES ($userId, 'recalculate', 'payroll', $id, '');
                """,
                new { userId = AppState.UserId, id = periodId.ToString() }, transaction);
        });
    }

    private static void CalculateInto(SqliteConnection connection, SqliteTransaction transaction,
        long periodId, string start, string end, string periodType, Dictionary<long, PayrollItem> existing)
    {
        var employees = connection.Query<Employee>(
            """
            SELECT * FROM employees
            WHERE status = 'activo' AND start_date <= $end AND (end_date IS NULL OR end_date >= $start)
            ORDER BY name;
            """, new { start, end }, transaction).AsList();

        var keptEmployeeIds = new List<long>();

        foreach (var employee in employees)
        {
            existing.TryGetValue(employee.Id, out var current);
            var calculated = Calculate(employee, start, end, periodType,
                current?.ExtraPerceptionsCents ?? 0,
                current?.InfonavitCents ?? 0,
                current?.OtherDeductionsCents ?? 0);
            if (calculated is null) continue;
            var extra = current?.ExtraPerceptionsCents ?? 0;
            var infonavit = current?.InfonavitCents ?? 0;
            var other = current?.OtherDeductionsCents ?? 0;
            var notes = current?.Notes ?? "";

            if (current is null)
            {
                connection.Execute(
                    """
                    INSERT INTO payroll_items
                      (period_id, employee_id, days, base_cents, extra_perceptions_cents, isr_cents,
                       imss_cents, infonavit_cents, other_deductions_cents, net_cents, notes)
                    VALUES ($periodId, $employeeId, $days, $base, $extra, $isr, $imss, $infonavit,
                            $other, $net, $notes);
                    """,
                    new
                    {
                        periodId,
                        employeeId = employee.Id,
                        days = calculated.Days,
                        @base = calculated.BaseCents,
                        extra,
                        isr = calculated.IsrCents,
                        imss = calculated.ImssCents,
                        infonavit,
                        other,
                        net = calculated.NetCents,
                        notes,
                    }, transaction);
            }
            else
            {
                connection.Execute(
                    """
                    UPDATE payroll_items SET days = $days, base_cents = $base, extra_perceptions_cents = $extra,
                      isr_cents = $isr, imss_cents = $imss, infonavit_cents = $infonavit,
                      other_deductions_cents = $other, net_cents = $net, notes = $notes
                    WHERE id = $id;
                    """,
                    new
                    {
                        id = current.Id,
                        days = calculated.Days,
                        @base = calculated.BaseCents,
                        extra,
                        isr = calculated.IsrCents,
                        imss = calculated.ImssCents,
                        infonavit,
                        other,
                        net = calculated.NetCents,
                        notes,
                    }, transaction);
            }

            keptEmployeeIds.Add(employee.Id);
        }

        RemoveObsoleteItems(connection, transaction, periodId, keptEmployeeIds);
    }

    /// <summary>
    /// H-06: al recalcular, los conceptos cuyos empleados ya no participan en el
    /// periodo (baja, alta fuera de fecha o cambio de estatus) deben desaparecer.
    /// Antes quedaban y el neto se inflaba con gente que ya no correspondía.
    /// Solo se eliminan empleados ajenos al cálculo: los ajustes manuales de los
    /// empleados vigentes se conservan porque se leen y reescriben en su fila.
    /// </summary>
    private static void RemoveObsoleteItems(SqliteConnection connection, SqliteTransaction transaction,
        long periodId, IReadOnlyCollection<long> keptEmployeeIds)
    {
        if (keptEmployeeIds.Count == 0)
        {
            connection.Execute(
                "DELETE FROM payroll_items WHERE period_id = $periodId;",
                new { periodId }, transaction);
            return;
        }

        connection.Execute(
            """
            DELETE FROM payroll_items
            WHERE period_id = $periodId AND employee_id NOT IN @keep;
            """,
            new { periodId, keep = keptEmployeeIds }, transaction);
    }

    private static PayrollCalculation? Calculate(Employee employee, string periodStart, string periodEnd,
        string periodType, long extra, long infonavit, long other)
    {
        var start = string.CompareOrdinal(employee.StartDate, periodStart) > 0
            ? employee.StartDate
            : periodStart;
        var end = employee.EndDate is not null && string.CompareOrdinal(employee.EndDate, periodEnd) < 0
            ? employee.EndDate
            : periodEnd;
        if (string.CompareOrdinal(start, end) > 0) return null;

        var periodDays = Dates.DiffDays(periodStart, periodEnd) + 1;
        var workedDays = Dates.DiffDays(start, end) + 1;
        var periodSalary = periodType == "quincenal"
            ? (long)Math.Round(employee.SalaryCents / 2d, MidpointRounding.AwayFromZero)
            : employee.SalaryCents;
        var baseCents = periodDays > 0
            ? (long)Math.Round(periodSalary * (double)workedDays / periodDays, MidpointRounding.AwayFromZero)
            : 0;
        var taxable = baseCents + extra;
        var brackets = Isr.Parse(AppState.Company.IsrBrackets);
        var isr = Isr.Compute(taxable, brackets, periodType);
        var imss = (long)Math.Round(taxable * Math.Max(0, AppState.Company.ImssRate),
            MidpointRounding.AwayFromZero);
        var net = Math.Max(0, taxable - isr - imss - infonavit - other);

        return new PayrollCalculation
        {
            Days = workedDays,
            BaseCents = baseCents,
            IsrCents = isr,
            ImssCents = imss,
            NetCents = net,
        };
    }

    public static void UpdateItem(long itemId, long periodId, double days, long baseCents, long extraCents,
        long isrCents, long imssCents, long infonavitCents, long otherCents, string notes)
    {
        EnsureWriter();
        if (days < 0)
            throw new ArgumentException("Los días trabajados no pueden ser negativos.");
        if (new[] { baseCents, extraCents, isrCents, imssCents, infonavitCents, otherCents }.Any(value => value < 0))
            throw new ArgumentException("Los importes no pueden ser negativos.");

        var period = GetPeriod(periodId) ?? throw new InvalidOperationException("La nómina no existe.");
        if (period.Status == "pagada")
            throw new InvalidOperationException("La nómina ya fue pagada; no se puede editar.");

        var net = Math.Max(0, baseCents + extraCents - isrCents - imssCents - infonavitCents - otherCents);
        var changed = AppState.Db.Execute(
            """
            UPDATE payroll_items SET days = $days, base_cents = $base, extra_perceptions_cents = $extra,
              isr_cents = $isr, imss_cents = $imss, infonavit_cents = $infonavit,
              other_deductions_cents = $other, net_cents = $net, notes = $notes
            WHERE id = $id AND period_id = $periodId;
            """,
            new
            {
                id = itemId,
                periodId,
                days,
                @base = baseCents,
                extra = extraCents,
                isr = isrCents,
                imss = imssCents,
                infonavit = infonavitCents,
                other = otherCents,
                net,
                notes = notes.Trim(),
            });
        if (changed == 0) throw new InvalidOperationException("El concepto no pertenece a esta nómina.");
        AppState.Db.Audit(AppState.UserId, "update", "payroll_item", itemId, $"Neto {net}");
    }

    public static void DeleteItem(long itemId, long periodId)
    {
        EnsureWriter();
        var period = GetPeriod(periodId) ?? throw new InvalidOperationException("La nómina no existe.");
        if (period.Status == "pagada")
            throw new InvalidOperationException("La nómina ya fue pagada; no se puede editar.");
        var changed = AppState.Db.Execute(
            "DELETE FROM payroll_items WHERE id = $id AND period_id = $periodId;",
            new { id = itemId, periodId });
        if (changed == 0) throw new InvalidOperationException("El concepto ya no existe.");
        AppState.Db.Audit(AppState.UserId, "delete", "payroll_item", itemId, "");
    }

    public static void Pay(long periodId, long accountId, string date)
    {
        EnsureWriter();
        if (!Dates.IsValidIsoDate(date))
            throw new ArgumentException("Escribe una fecha de pago válida.");
        var period = GetPeriod(periodId) ?? throw new InvalidOperationException("La nómina no existe.");
        if (period.Status == "pagada")
            throw new InvalidOperationException("La nómina ya fue pagada.");
        if (period.EmployeeCount == 0)
            throw new InvalidOperationException("Agrega al menos un empleado antes de pagar la nómina.");
        if (period.TotalNetCents <= 0)
            throw new InvalidOperationException("El neto de la nómina debe ser mayor a cero.");
        var accountName = AppState.Db.ScalarString(
            "SELECT name FROM accounts WHERE id = $id AND active = 1;", new { id = accountId });
        if (accountName is null)
            throw new InvalidOperationException("La cuenta de pago no existe o está inactiva.");

        AppState.Db.InTransaction((connection, transaction) =>
        {
            var categoryId = connection.ExecuteScalar<long?>(
                "SELECT id FROM categories WHERE name = 'Nómina' AND kind = 'gasto' LIMIT 1;",
                transaction: transaction);
            if (categoryId is null)
            {
                connection.Execute(
                    "INSERT INTO categories (name, kind) VALUES ('Nómina', 'gasto');",
                    transaction: transaction);
                categoryId = connection.ExecuteScalar<long>(
                    "SELECT last_insert_rowid();", transaction: transaction);
            }

            connection.Execute(
                """
                INSERT INTO transactions
                  (date, type, category_id, account_id, description, subtotal_cents, iva_cents,
                   total_cents, iva_rate, method, reference, source, source_id, created_by)
                VALUES ($date, 'gasto', $categoryId, $accountId, $description, $net, 0, $net, 0,
                        'Nómina', $reference, 'nomina', $periodId, $userId);
                """,
                new
                {
                    date,
                    categoryId,
                    accountId,
                    description = $"Nómina {period.Label}",
                    net = period.TotalNetCents,
                    reference = $"Periodo {period.StartDate} a {period.EndDate}",
                    periodId,
                    userId = AppState.UserId,
                }, transaction);
            var transactionId = connection.ExecuteScalar<long>(
                "SELECT last_insert_rowid();", transaction: transaction);
            connection.Execute(
                """
                UPDATE payroll_periods
                SET status = 'pagada', account_id = $accountId, transaction_id = $transactionId, paid_at = $paidAt
                WHERE id = $id;
                """,
                new
                {
                    accountId,
                    transactionId,
                    paidAt = Dates.NowIso(),
                    id = periodId,
                }, transaction);
            connection.Execute(
                """
                INSERT INTO audit_log (user_id, action, entity, entity_id, detail)
                VALUES ($userId, 'pay', 'payroll', $id, $detail);
                """,
                new
                {
                    userId = AppState.UserId,
                    id = periodId.ToString(),
                    detail = $"Neto {period.TotalNetCents}, cuenta {accountName}, fecha {date}",
                }, transaction);
        });
    }

    public static void RevertPayment(long periodId)
    {
        EnsureWriter();
        AppState.Db.InTransaction((connection, transaction) =>
        {
            var period = GetPeriodForEdit(connection, transaction, periodId);
            if (period.Status != "pagada")
                throw new InvalidOperationException("La nómina no está pagada.");

            if (period.TransactionId is > 0)
            {
                var source = connection.QuerySingleOrDefault<PayrollTransactionSource>(
                    "SELECT source, source_id FROM transactions WHERE id = $id;",
                    new { id = period.TransactionId }, transaction);
                if (source is { Source: not null })
                {
                    if (source.Source != "nomina" || source.SourceId != period.Id)
                        throw new InvalidOperationException("El gasto vinculado ya no corresponde a esta nómina.");
                    connection.Execute(
                        "DELETE FROM transactions WHERE id = $id;",
                        new { id = period.TransactionId }, transaction);
                }
            }

            connection.Execute(
                """
                UPDATE payroll_periods
                SET status = 'borrador', account_id = NULL, transaction_id = NULL, paid_at = NULL
                WHERE id = $id;
                """, new { id = periodId }, transaction);
            connection.Execute(
                """
                INSERT INTO audit_log (user_id, action, entity, entity_id, detail)
                VALUES ($userId, 'revert_payment', 'payroll', $id, '');
                """,
                new { userId = AppState.UserId, id = periodId.ToString() }, transaction);
        });
    }

    public static void DeletePeriod(long periodId)
    {
        EnsureWriter();
        var period = GetPeriod(periodId) ?? throw new InvalidOperationException("La nómina no existe.");
        if (period.Status == "pagada")
            throw new InvalidOperationException(
                "No se puede eliminar una nómina pagada. Revierte el pago primero.");
        AppState.Db.Execute("DELETE FROM payroll_periods WHERE id = $id;", new { id = periodId });
        AppState.Db.Audit(AppState.UserId, "delete", "payroll", periodId, period.Label);
    }

    private static PayrollPeriod GetPeriodForEdit(SqliteConnection connection, SqliteTransaction transaction,
        long periodId)
    {
        return connection.QuerySingleOrDefault<PayrollPeriod>(
            "SELECT * FROM payroll_periods WHERE id = $id;",
            new { id = periodId }, transaction)
            ?? throw new InvalidOperationException("La nómina no existe.");
    }

    private static void EnsureWriter()
    {
        if (!AppState.Can(Permissions.PayrollWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar la nómina.");
    }
}
