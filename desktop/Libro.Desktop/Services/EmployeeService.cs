using Libro.Desktop.Data;
using Libro.Desktop.Util;

namespace Libro.Desktop.Services;

public sealed class EmployeeInput
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Rfc { get; set; } = "";
    public string Curp { get; set; } = "";
    public string Nss { get; set; } = "";
    public string Position { get; set; } = "";
    public string Department { get; set; } = "";
    public long SalaryCents { get; set; }
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public string Status { get; set; } = "activo";
    public string BankName { get; set; } = "";
    public string BankAccount { get; set; } = "";
}

public sealed class EmployeeSummary
{
    public long ActiveCount { get; set; }
    public long InactiveCount { get; set; }
    public long MonthlyPayrollCents { get; set; }
}

public static class EmployeeService
{
    public static List<Employee> GetEmployees(bool includeInactive = false) =>
        AppState.Db.All<Employee>(
            $"SELECT * FROM employees {(includeInactive ? "" : "WHERE status = 'activo'")} ORDER BY name;");

    public static Employee? GetEmployee(long id) =>
        AppState.Db.One<Employee>("SELECT * FROM employees WHERE id = $id;", new { id });

    public static EmployeeSummary GetSummary()
    {
        var row = AppState.Db.One<EmployeeSummary>(
            """
            SELECT
              COALESCE(SUM(CASE WHEN status = 'activo' THEN 1 ELSE 0 END), 0) AS active_count,
              COALESCE(SUM(CASE WHEN status = 'inactivo' THEN 1 ELSE 0 END), 0) AS inactive_count,
              COALESCE(SUM(CASE WHEN status = 'activo' THEN salary_cents ELSE 0 END), 0) AS monthly_payroll_cents
            FROM employees;
            """) ?? new EmployeeSummary();
        return row;
    }

    public static long SaveEmployee(EmployeeInput input)
    {
        EnsureWriter();
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("Escribe el nombre del empleado.");
        if (input.Name.Trim().Length > 160)
            throw new ArgumentException("El nombre es demasiado largo.");
        if (!Dates.IsValidIsoDate(input.StartDate))
            throw new ArgumentException("La fecha de ingreso no es válida.");
        if (!string.IsNullOrWhiteSpace(input.EndDate))
        {
            if (!Dates.IsValidIsoDate(input.EndDate))
                throw new ArgumentException("La fecha de baja no es válida.");
            if (string.CompareOrdinal(input.EndDate, input.StartDate) < 0)
                throw new ArgumentException("La fecha de baja no puede ser anterior a la de ingreso.");
        }
        if (input.SalaryCents < 0)
            throw new ArgumentException("El salario no puede ser negativo.");
        if (input.Rfc.Trim().Length > 20 || input.Curp.Trim().Length > 30 || input.Nss.Trim().Length > 30)
            throw new ArgumentException("Los datos fiscales son demasiado largos.");
        if (input.Status is not ("activo" or "inactivo"))
            throw new ArgumentException("El estatus del empleado no es válido.");

        var data = new
        {
            id = input.Id,
            name = input.Name.Trim(),
            rfc = input.Rfc.Trim().ToUpperInvariant(),
            curp = input.Curp.Trim().ToUpperInvariant(),
            nss = input.Nss.Trim(),
            position = input.Position.Trim(),
            department = input.Department.Trim(),
            salary = input.SalaryCents,
            startDate = input.StartDate,
            endDate = string.IsNullOrWhiteSpace(input.EndDate) ? null : input.EndDate,
            status = input.Status,
            bankName = input.BankName.Trim(),
            bankAccount = input.BankAccount.Trim(),
        };

        if (input.Id > 0)
        {
            var exists = AppState.Db.ScalarLong(
                "SELECT COUNT(*) FROM employees WHERE id = $id;", new { id = input.Id });
            if (exists == 0) throw new InvalidOperationException("El empleado no existe.");
            AppState.Db.Execute(
                """
                UPDATE employees SET name = $name, rfc = $rfc, curp = $curp, nss = $nss,
                  position = $position, department = $department, salary_cents = $salary,
                  start_date = $startDate, end_date = $endDate, status = $status,
                  bank_name = $bankName, bank_account = $bankAccount
                WHERE id = $id;
                """, data);
            AppState.Db.Audit(AppState.UserId, "update", "employee", input.Id, data.name);
            return input.Id;
        }

        var id = AppState.Db.Insert(
            """
            INSERT INTO employees
              (name, rfc, curp, nss, position, department, salary_cents, start_date, end_date,
               status, bank_name, bank_account)
            VALUES ($name, $rfc, $curp, $nss, $position, $department, $salary, $startDate, $endDate,
                    $status, $bankName, $bankAccount);
            """, data);
        AppState.Db.Audit(AppState.UserId, "create", "employee", id, data.name);
        return id;
    }

    public static void ToggleEmployee(long id, bool active)
    {
        EnsureWriter();
        var employee = GetEmployee(id) ?? throw new InvalidOperationException("El empleado no existe.");
        var status = active ? "activo" : "inactivo";
        AppState.Db.Execute(
            "UPDATE employees SET status = $status WHERE id = $id;", new { status, id });
        AppState.Db.Audit(AppState.UserId, active ? "restore" : "deactivate", "employee", id, employee.Name);
    }

    private static void EnsureWriter()
    {
        if (!AppState.Can(Permissions.PayrollWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar empleados.");
    }
}
