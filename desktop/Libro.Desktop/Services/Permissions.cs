namespace Libro.Desktop.Services;

public sealed class SessionUser
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string Role { get; init; } = "consulta";
}

public static class Permissions
{
    public const string UsersManage = "users.manage";
    public const string SettingsManage = "settings.manage";
    public const string BackupManage = "backup.manage";
    public const string AuditView = "audit.view";
    public const string PayrollView = "payroll.view";
    public const string PayrollWrite = "payroll.write";
    public const string TaxView = "tax.view";
    public const string ReportsView = "reports.view";
    public const string TxWrite = "tx.write";
    public const string InvoiceWrite = "invoice.write";
    public const string InventoryWrite = "inventory.write";
    public const string ContactsWrite = "contacts.write";
    public const string ProductsWrite = "products.write";
    public const string AccountsWrite = "accounts.write";

    private static readonly Dictionary<string, HashSet<string>> Matrix = new()
    {
        ["admin"] = new HashSet<string>
        {
            UsersManage, SettingsManage, BackupManage, AuditView, PayrollView, PayrollWrite,
            TaxView, ReportsView, TxWrite, InvoiceWrite, InventoryWrite, ContactsWrite,
            ProductsWrite, AccountsWrite,
        },
        ["contador"] = new HashSet<string>
        {
            PayrollView, PayrollWrite, TaxView, ReportsView, TxWrite, InvoiceWrite,
            InventoryWrite, ContactsWrite, ProductsWrite, AccountsWrite, AuditView,
        },
        ["capturista"] = new HashSet<string>
        {
            TxWrite, InvoiceWrite, InventoryWrite, ContactsWrite, ProductsWrite,
        },
        ["consulta"] = new HashSet<string> { TaxView, ReportsView },
    };

    public static bool Can(string role, string permission) =>
        Matrix.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    public static string RoleLabel(string role) => role switch
    {
        "admin" => "Administrador",
        "contador" => "Contador",
        "capturista" => "Capturista",
        "consulta" => "Consulta",
        _ => role,
    };

    public static string RoleDescription(string role) => role switch
    {
        "admin" => "Acceso total, usuarios, respaldos y configuración.",
        "contador" => "Operación contable, facturas, nómina, impuestos y reportes.",
        "capturista" => "Captura de movimientos, facturas, contactos e inventario.",
        "consulta" => "Solo lectura de reportes, impuestos y operación.",
        _ => "",
    };

    public static IReadOnlyList<(string Key, string Label)> Roles => new[]
    {
        ("admin", "Administrador"),
        ("contador", "Contador"),
        ("capturista", "Capturista"),
        ("consulta", "Consulta"),
    };
}
