using Libro.Desktop.Data;
using System;
using System.IO;

// Herramienta de diagnóstico local. No forma parte de la aplicación instalada.
// Uso:
//   Libro.DbTool <carpeta-de-datos> [correo] [nueva-contraseña]
// Sin correo solo imprime un resumen de la base.
// Con correo y contraseña restablece la cuenta indicada (útil para una copia
// de pruebas; no debe usarse sobre datos reales sin autorización).

if (args.Length < 1)
{
    Console.Error.WriteLine("Uso: Libro.DbTool <carpeta-de-datos> [correo] [nueva-contraseña]");
    return 2;
}

var dataDir = Path.GetFullPath(args[0]);
Environment.SetEnvironmentVariable("LIBRO_DATA_DIR", dataDir);

var db = new Database();
db.Initialize();

if (args.Length >= 3)
{
    var email = args[1].Trim().ToLowerInvariant();
    var password = args[2];
    var hash = Scrypt.HashPassword(password);
    var updated = db.Execute(
        "UPDATE users SET password_hash = $hash, active = 1 WHERE lower(email) = $email;",
        new { hash, email });
    Console.WriteLine(updated > 0
        ? $"Contraseña restablecida para {email}."
        : $"No se encontró el usuario {email}.");
}

var company = db.One<dynamic>("SELECT name, rfc FROM company LIMIT 1;");
Console.WriteLine($"Base: {db.DbPath}");
Console.WriteLine($"Empresa: {company?.name ?? "(sin empresa)"} / RFC: {company?.rfc ?? ""}");
Console.WriteLine("Usuarios:");
foreach (var user in db.All<dynamic>("SELECT id, name, email, role, active FROM users ORDER BY id;"))
    Console.WriteLine($"  {user.id}: {user.name} <{user.email}> {user.role} activo={user.active}");

var tables = new[]
{
    "accounts", "categories", "contacts", "transactions", "products",
    "inventory_movements", "invoices", "invoice_items", "payments",
    "employees", "payroll_periods", "payroll_items", "audit_log",
};
foreach (var table in tables)
{
    try
    {
        var count = db.ScalarLong($"SELECT COUNT(*) FROM {table};");
        Console.WriteLine($"{table}: {count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{table}: (no disponible: {ex.Message})");
    }
}

return 0;
