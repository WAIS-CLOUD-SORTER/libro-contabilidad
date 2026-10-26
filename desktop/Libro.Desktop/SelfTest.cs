using System.IO;
using System.Text;
using Dapper;
using Libro.Desktop.Data;
using Microsoft.Data.Sqlite;

namespace Libro.Desktop;

internal static class SelfTest
{
    private static readonly StringBuilder Log = new();

    public static void Run()
    {
        var logPath = Environment.GetEnvironmentVariable("LIBRO_SELFTEST_LOG")
            ?? Path.Combine(Path.GetTempPath(), "libro-selftest.log");

        try
        {
            ScryptVector();
            FreshEncryptedDatabase();
            PlaintextMigration();
            WrongKeyRejected();
            BackupRestoreAndCorruptRejected();
            Log.AppendLine("SELFTEST_OK");
        }
        catch (Exception ex)
        {
            Log.AppendLine("SELFTEST_FAIL");
            Log.AppendLine(ex.ToString());
        }
        finally
        {
            File.WriteAllText(logPath, Log.ToString(), new UTF8Encoding(false));
        }
    }

    private static void ScryptVector()
    {
        const string password = "Contraseña123!";
        const string stored =
            "scrypt$16384$8$1$00112233445566778899aabbccddeeff$4ede21eb306b93fea49c1234cf3223fb65ced66ef45626999ee20dbdfe566c8e4a216bb461e0a760e0602c163e0331118bcb6346ad87da1769ec4adb0543097e";
        Check(Scrypt.VerifyPassword(password, stored), "scrypt vector");
        Check(!Scrypt.VerifyPassword(password + "x", stored), "scrypt rechazo");
    }

    private static void FreshEncryptedDatabase()
    {
        var dir = NewTempDirectory();
        Environment.SetEnvironmentVariable("LIBRO_DATA_DIR", dir);
        var db = new Database();
        db.Initialize();

        db.Execute(
            """
            INSERT INTO company (id, name, rfc, regimen_fiscal, address, phone, email, iva_rate,
              invoice_series, next_invoice_number, imss_rate, isr_brackets)
            VALUES (1, 'Prueba', 'XAXX010101000', '601', '', '', '', 0.16, 'A', 1, 0.025, '');
            """);
        db.Execute(
            """
            INSERT INTO users (name, email, password_hash, role, active)
            VALUES ('Admin', 'admin@example.com', $hash, 'admin', 1);
            """,
            new { hash = Scrypt.HashPassword("Secreta123!") });

        var company = db.One<Company>("SELECT * FROM company WHERE id = 1;");
        Check(company?.Name == "Prueba", "empresa insertada");
        var user = db.One<User>("SELECT * FROM users WHERE email = 'admin@example.com';");
        Check(user is not null && Scrypt.VerifyPassword("Secreta123!", user.PasswordHash), "usuario y scrypt");

        Check(File.Exists(AppPaths.KeyPath), "archivo de llave");
        var header = File.ReadAllBytes(db.DbPath).Take(16).ToArray();
        Check(!header.SequenceEqual(Encoding.ASCII.GetBytes("SQLite format 3\0")), "base cifrada");

        var reopened = new Database();
        reopened.Initialize();
        var again = reopened.One<Company>("SELECT * FROM company WHERE id = 1;");
        Check(again?.Name == "Prueba", "reapertura con llave");

        Log.AppendLine($"fresh_db={db.DbPath}");
        Directory.Delete(dir, true);
    }

    private static void PlaintextMigration()
    {
        var dir = NewTempDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "contabilidad.db");
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        };
        using (var connection = new SqliteConnection(builder.ToString()))
        {
            connection.Open();
            connection.Execute(Schema.Sql);
            connection.Execute(
                """
                INSERT INTO company (id, name, rfc, regimen_fiscal, address, phone, email, iva_rate,
                  invoice_series, next_invoice_number, imss_rate, isr_brackets)
                VALUES (1, 'Negocio plano', '', '', '', '', '', 0.16, 'A', 1, 0.025, '');
                """);
            connection.Execute(
                """
                INSERT INTO users (name, email, password_hash, role, active)
                VALUES ('Admin plano', 'admin@plano.local', $hash, 'admin', 1);
                """,
                new { hash = Scrypt.HashPassword("Secreta123!") });
            connection.Execute(
                "INSERT INTO accounts (name, type, opening_balance_cents) VALUES ('Caja plana', 'efectivo', 100000);");
            connection.Execute(
                """
                INSERT INTO categories (name, kind) VALUES
                  ('Ventas planas', 'ingreso'),
                  ('Insumos planos', 'gasto');
                """);
            connection.Execute(
                "INSERT INTO contacts (type, name, rfc) VALUES ('cliente', 'Cliente plano', 'XAXX010101000');");
            connection.Execute(
                """
                INSERT INTO products (sku, name, unit, price_cents, cost_cents, stock, min_stock)
                VALUES ('PLANO-1', 'Producto plano', 'pieza', 10000, 6000, 10, 2);
                """);
            connection.Execute(
                """
                INSERT INTO transactions (date, type, category_id, contact_id, account_id, description,
                  subtotal_cents, iva_cents, total_cents, iva_rate, created_by)
                VALUES ('2026-10-01', 'ingreso', 1, 1, 1, 'Venta plana', 10000, 1600, 11600, 0.16, 1);
                """);
            connection.Execute(
                """
                INSERT INTO inventory_movements (product_id, date, type, qty, unit_cost_cents, reason,
                  account_id, transaction_id, created_by)
                VALUES (1, '2026-10-01', 'entrada', 10, 6000, 'Compra plana', 1, 1, 1);
                """);
            connection.Execute(
                """
                INSERT INTO invoices (series, number, contact_id, date, due_date, status,
                  subtotal_cents, iva_cents, total_cents, paid_cents, created_by)
                VALUES ('A', 1, 1, '2026-10-01', '2026-10-31', 'emitida', 10000, 1600, 11600, 0, 1);
                """);
            connection.Execute(
                """
                INSERT INTO invoice_items (invoice_id, product_id, position, description, qty,
                  unit_price_cents, iva_rate, subtotal_cents, iva_cents, total_cents, unit_cost_cents)
                VALUES (1, 1, 1, 'Producto plano', 1, 10000, 0.16, 10000, 1600, 11600, 6000);
                """);
            connection.Execute(
                """
                INSERT INTO payments (invoice_id, date, amount_cents, account_id, method, created_by)
                VALUES (1, '2026-10-02', 5000, 1, 'efectivo', 1);
                """);
            connection.Execute(
                """
                INSERT INTO employees (name, rfc, position, department, salary_cents, start_date, status)
                VALUES ('Empleado plano', 'XAXX010101000', 'Puesto', 'Departamento', 1200000, '2026-01-01', 'activo');
                """);
            connection.Execute(
                """
                INSERT INTO payroll_periods (year, month, period_type, quincena, start_date, end_date,
                  status, account_id, created_by)
                VALUES (2026, 10, 'mensual', 0, '2026-10-01', '2026-10-31', 'borrador', 1, 1);
                """);
            connection.Execute(
                """
                INSERT INTO payroll_items (period_id, employee_id, days, base_cents, extra_perceptions_cents,
                  isr_cents, imss_cents, infonavit_cents, other_deductions_cents, net_cents)
                VALUES (1, 1, 31, 1200000, 0, 100000, 30000, 0, 0, 1070000);
                """);
            connection.Execute(
                """
                INSERT INTO audit_log (user_id, action, entity, entity_id, detail)
                VALUES (1, 'crear', 'empresa', '1', 'prueba de migración');
                """);
            connection.Execute(
                """
                INSERT INTO sessions (token_hash, user_id, created_at, expires_at, user_agent)
                VALUES ('hash-de-prueba', 1, datetime('now'), datetime('now', '+30 days'), 'prueba');
                """);
        }

        Environment.SetEnvironmentVariable("LIBRO_DATA_DIR", dir);
        var db = new Database();
        db.Initialize();
        var company = db.One<Company>("SELECT * FROM company WHERE id = 1;");
        Check(company?.Name == "Negocio plano", "migración conserva datos");
        var migratedUser = db.One<User>("SELECT * FROM users WHERE email = 'admin@plano.local';");
        Check(migratedUser is not null && Scrypt.VerifyPassword("Secreta123!", migratedUser.PasswordHash),
            "migración conserva usuarios y contraseñas");
        var migratedTables = new (string Table, long Expected)[]
        {
            ("company", 1),
            ("users", 1),
            ("sessions", 1),
            ("audit_log", 1),
            ("accounts", 1),
            ("categories", 2),
            ("contacts", 1),
            ("products", 1),
            ("transactions", 1),
            ("inventory_movements", 1),
            ("invoices", 1),
            ("invoice_items", 1),
            ("payments", 1),
            ("employees", 1),
            ("payroll_periods", 1),
            ("payroll_items", 1),
        };
        foreach (var (table, expected) in migratedTables)
        {
            Check(db.ScalarLong($"SELECT COUNT(*) FROM {table};") == expected,
                $"migración conserva {table}");
        }
        Check(db.ScalarLong("SELECT total_cents FROM invoices WHERE id = 1;") == 11600,
            "migración conserva importes");
        var header = File.ReadAllBytes(db.DbPath).Take(16).ToArray();
        Check(!header.SequenceEqual(Encoding.ASCII.GetBytes("SQLite format 3\0")), "migración queda cifrada");
        Check(!File.Exists(db.DbPath + ".plano"), "no queda copia plana");

        Log.AppendLine($"migrated_db={db.DbPath}");
        Directory.Delete(dir, true);
    }

    private static void WrongKeyRejected()
    {
        var source = NewTempDirectory();
        Environment.SetEnvironmentVariable("LIBRO_DATA_DIR", source);
        var db = new Database();
        db.Initialize();
        db.Execute(
            """
            INSERT INTO company (id, name, rfc, regimen_fiscal, address, phone, email, iva_rate,
              invoice_series, next_invoice_number, imss_rate, isr_brackets)
            VALUES (1, 'Original', '', '', '', '', '', 0.16, 'A', 1, 0.025, '');
            """);

        var copy = NewTempDirectory();
        File.Copy(db.DbPath, Path.Combine(copy, "contabilidad.db"));
        Environment.SetEnvironmentVariable("LIBRO_DATA_DIR", copy);

        var rejected = false;
        try
        {
            var wrong = new Database();
            wrong.Initialize();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Check(rejected, "llave incorrecta rechazada");
        Directory.Delete(source, true);
        Directory.Delete(copy, true);
    }

    private static void BackupRestoreAndCorruptRejected()
    {
        var dir = NewTempDirectory();
        Environment.SetEnvironmentVariable("LIBRO_DATA_DIR", dir);
        var db = new Database();
        db.Initialize();
        db.Execute(
            """
            INSERT INTO company (id, name, rfc, regimen_fiscal, address, phone, email, iva_rate,
              invoice_series, next_invoice_number, imss_rate, isr_brackets)
            VALUES (1, 'Respaldo', '', '', '', '', '', 0.16, 'A', 1, 0.025, '');
            """);

        var backup = Path.Combine(dir, "respaldo.libro");
        db.BackupTo(backup);
        Check(File.Exists(backup), "respaldo creado");
        Check(!File.ReadAllBytes(backup).Take(16)
                .SequenceEqual(Encoding.ASCII.GetBytes("SQLite format 3\0")), "respaldo cifrado");

        var corrupt = Path.Combine(dir, "corrupto.libro");
        var bytes = File.ReadAllBytes(backup);
        bytes[100] ^= 0x5A;
        File.WriteAllBytes(corrupt, bytes);
        var rejected = false;
        try
        {
            db.RestoreFrom(corrupt);
        }
        catch (Exception)
        {
            rejected = true;
        }

        Check(rejected, "respaldo corrupto rechazado");
        var still = db.One<Company>("SELECT * FROM company WHERE id = 1;");
        Check(still?.Name == "Respaldo", "base intacta tras rechazo");

        db.Execute("UPDATE company SET name = 'Modificado' WHERE id = 1;");
        db.RestoreFrom(backup);
        var restored = db.One<Company>("SELECT * FROM company WHERE id = 1;");
        Check(restored?.Name == "Respaldo", "restauración conserva datos");
        var header = File.ReadAllBytes(db.DbPath).Take(16).ToArray();
        Check(!header.SequenceEqual(Encoding.ASCII.GetBytes("SQLite format 3\0")), "restauración queda cifrada");

        Log.AppendLine($"backup_db={db.DbPath}");
        Directory.Delete(dir, true);
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "libro-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Check(bool condition, string name)
    {
        Log.AppendLine((condition ? "OK " : "FAIL ") + name);
        if (!condition) throw new InvalidOperationException("Falló: " + name);
    }
}
