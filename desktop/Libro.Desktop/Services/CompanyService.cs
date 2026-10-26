using Dapper;
using Libro.Desktop.Data;
using Libro.Desktop.Payroll;

namespace Libro.Desktop.Services;

public static class CompanyService
{
    public static Company GetCompany(Database db) =>
        db.One<Company>("SELECT * FROM company WHERE id = 1;") ?? new Company
        {
            Id = 1,
            Name = "Mi negocio",
            IsrBrackets = Isr.DefaultJson,
        };

    public static void SaveCompany(Database db, Company company, long? userId)
    {
        db.Execute(
            """
            UPDATE company SET
              name = $name, rfc = $rfc, regimen_fiscal = $regimen, address = $address,
              phone = $phone, email = $email, iva_rate = $ivaRate, invoice_series = $series,
              next_invoice_number = $nextNumber, imss_rate = $imssRate, isr_brackets = $brackets
            WHERE id = 1;
            """,
            new
            {
                name = company.Name.Trim(),
                rfc = company.Rfc.Trim().ToUpperInvariant(),
                regimen = company.RegimenFiscal.Trim(),
                address = company.Address.Trim(),
                phone = company.Phone.Trim(),
                email = company.Email.Trim(),
                ivaRate = company.IvaRate,
                series = string.IsNullOrWhiteSpace(company.InvoiceSeries) ? "A" : company.InvoiceSeries.Trim(),
                nextNumber = company.NextInvoiceNumber,
                imssRate = company.ImssRate,
                brackets = company.IsrBrackets,
            });
        db.Audit(userId, "update", "company", 1, company.Name);
    }

    public static List<Account> GetAccounts(Database db, bool includeInactive = false) =>
        db.All<Account>(
            $"SELECT * FROM accounts {(includeInactive ? "" : "WHERE active = 1")} ORDER BY name;");

    public static List<Category> GetCategories(Database db, bool includeArchived = false) =>
        db.All<Category>(
            $"SELECT * FROM categories {(includeArchived ? "" : "WHERE archived = 0")} ORDER BY kind, name;");

    public static List<Contact> GetContacts(Database db, bool includeArchived = false) =>
        db.All<Contact>(
            $"SELECT * FROM contacts {(includeArchived ? "" : "WHERE archived = 0")} ORDER BY name;");

    public static List<Product> GetProducts(Database db, bool includeInactive = false) =>
        db.All<Product>(
            $"SELECT * FROM products {(includeInactive ? "" : "WHERE active = 1")} ORDER BY name;");

    public static List<Employee> GetEmployees(Database db, bool includeInactive = false) =>
        db.All<Employee>(
            $"SELECT * FROM employees {(includeInactive ? "" : "WHERE status = 'activo'")} ORDER BY name;");

    public static List<User> GetUsers(Database db) =>
        db.All<User>(
            """
            SELECT id, name, email, role, active, created_at, last_login_at
            FROM users ORDER BY name;
            """);

    public static List<AccountBalance> GetAccountBalances(Database db, bool includeInactive = false) =>
        db.All<AccountBalance>(
            $"""
             SELECT a.*,
               a.opening_balance_cents
               + COALESCE((SELECT SUM(CASE WHEN t.type = 'ingreso' THEN t.total_cents ELSE -t.total_cents END)
                           FROM transactions t WHERE t.account_id = a.id), 0)
               + COALESCE((SELECT SUM(p.amount_cents) FROM payments p WHERE p.account_id = a.id), 0)
               AS balance_cents
             FROM accounts a
             {(includeInactive ? "" : "WHERE a.active = 1")}
             ORDER BY a.name;
             """);

    public static void SaveAccount(Database db, Account account, long? userId)
    {
        var name = account.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Escribe el nombre de la cuenta.");
        if (account.Id == 0)
        {
            var id = db.Insert(
                """
                INSERT INTO accounts (name, type, opening_balance_cents, active)
                VALUES ($name, $type, $opening, 1);
                """,
                new { name, type = account.Type, opening = account.OpeningBalanceCents });
            db.Audit(userId, "create", "account", id, name);
        }
        else
        {
            db.Execute(
                """
                UPDATE accounts SET name = $name, type = $type,
                  opening_balance_cents = $opening, active = $active
                WHERE id = $id;
                """,
                new { name, type = account.Type, opening = account.OpeningBalanceCents, active = account.Active, id = account.Id });
            db.Audit(userId, "update", "account", account.Id, name);
        }
    }

    public static void ArchiveAccount(Database db, long id, bool archived, long? userId)
    {
        db.Execute("UPDATE accounts SET active = $active WHERE id = $id;", new { active = archived ? 0 : 1, id });
        db.Audit(userId, archived ? "archive" : "restore", "account", id);
    }

    public static void SaveCategory(Database db, Category category, long? userId)
    {
        var name = category.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Escribe el nombre de la categoría.");
        if (category.Id == 0)
        {
            var id = db.Insert(
                "INSERT INTO categories (name, kind, archived) VALUES ($name, $kind, 0);",
                new { name, kind = category.Kind });
            db.Audit(userId, "create", "category", id, name);
        }
        else
        {
            db.Execute(
                "UPDATE categories SET name = $name, kind = $kind, archived = $archived WHERE id = $id;",
                new { name, kind = category.Kind, archived = category.Archived, id = category.Id });
            db.Audit(userId, "update", "category", category.Id, name);
        }
    }
}
