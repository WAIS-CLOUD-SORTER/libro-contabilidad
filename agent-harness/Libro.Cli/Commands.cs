using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Libro.Desktop.Data;
using Libro.Desktop.Services;
using Libro.Desktop.Util;

namespace Libro.Cli;

internal static class Commands
{
    public const string HelpText = """
Libro CLI — interfaz agent-native para Libro Contabilidad (México, MXN, IVA).

Uso:
  libro-cli [opciones globales] <grupo> <acción> [opciones]
  libro-cli [opciones globales]                 # modo REPL

Opciones globales:
  --data-dir <ruta>       Carpeta de datos (por defecto %LOCALAPPDATA%\Libro\data)
  --json                  Salida JSON para agentes
  --email <correo>        Correo de la sesión (o LIBRO_EMAIL)
  --password <clave>      Contraseña (o LIBRO_PASSWORD; evita la línea de comandos)
  --password-env <nombre> Lee la contraseña de una variable de entorno
  --debug                 Incluye el detalle técnico en errores
  --help                  Muestra esta ayuda

Comandos:
  doctor                         Diagnóstico de datos, cifrado y respaldo
  auth setup|login|status|logout|change-password|whoami
  company show|update
  dashboard
  accounts list|balances|save|archive
  categories list|save
  contacts list|show|stats|save|archive
  products list|show|summary|save|toggle
  transactions list|show|totals|save|delete
  invoices list|show|overview|save|emit|cancel|delete|pay|delete-payment
  inventory list|summary|movements|movement
  employees list|show|summary|save|toggle
  payroll list|show|summary|create|recalc|update-item|delete-item|pay|revert|delete
  taxes summary|annual
  reports income|cashflow|aging|monthly|balances
  users list|summary|save|activate|deactivate|reset-password|roles
  audit list
  backup create|list|restore
  schema [operación]             Ejemplos JSON para las entradas
  version | help | repl

Reglas:
  - Los importes se expresan en centavos (long) para evitar errores de redondeo.
  - Las fechas usan yyyy-MM-dd; la zona horaria es México.
  - Las operaciones de escritura respetan los permisos del rol autenticado.
  - `--help` después de un comando muestra su ayuda sin ejecutar la operación.
  - Las facturas son documentos internos; no son CFDI y no se envían al SAT.
""";

    private const string HelpFooter = """
Opciones globales: --data-dir <ruta>  --json  --email <correo>  --password <clave>
                   --password-env <var>  --debug  --help
Importes en centavos; fechas yyyy-MM-dd. `schema <operación>` muestra ejemplos JSON.
""";

    private static readonly Dictionary<string, string> GroupHelp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["doctor"] = """
Uso: libro-cli [globales] doctor

Inspecciona rutas, tamaño, tipo de base (missing|plaintext|encrypted), llave,
respaldos y conteos. No abre la base ni crea clave.bin cuando la llave falta.
""",
        ["version"] = """
Uso: libro-cli [globales] version

Muestra la versión del CLI, la de Libro.Desktop y la de .NET.
""",
        ["dashboard"] = """
Uso: libro-cli [globales] dashboard

Resumen del mes actual: ingresos, gastos, utilidad, IVA, por cobrar, efectivo,
inventario, facturas vencidas y productos con bajo stock.
""",
        ["auth"] = """
Uso: libro-cli [globales] auth <acción>

Acciones:
  setup            Crea la cuenta inicial en una base vacía.
                   --company <nombre> --name <administrador>
                   [--email <correo>] [--password <clave> | --password-env <var>]
  login            Inicia sesión en este proceso.
                   [--email <correo>] [--password <clave> | --password-env <var>]
  status|whoami    Muestra la sesión activa; no crea datos.
  logout           Cierra la sesión en memoria.
  change-password  Cambia la contraseña del usuario actual.
                   --password <actual> --new-password <nueva>
""",
        ["company"] = """
Uso: libro-cli [globales] company show|update

  show     Muestra la ficha de la empresa.
  update   Reemplaza la ficha desde --input <json> o --stdin.
           Requiere settings.manage. Campos: name, rfc, regimenFiscal, address,
           phone, email, ivaRate (0..1), invoiceSeries, nextInvoiceNumber,
           imssRate (0..1), isrBrackets (JSON serializado como texto).
""",
        ["accounts"] = """
Uso: libro-cli [globales] accounts list|balances|save|archive

  list      [--include-inactive]
  balances  [--include-inactive]  Saldo inicial + movimientos + cobros/pagos.
  save      --input <json> | --stdin
            Campos: id, name, type (efectivo|banco|tarjeta),
            openingBalanceCents, active. En alta se fuerza active = 1.
  archive   --id N [--restore]
""",
        ["categories"] = """
Uso: libro-cli [globales] categories list|save

  list  [--include-archived]
  save  --input <json> | --stdin
        Campos: id, name, kind (ingreso|gasto), archived.
        En alta se fuerza archived = 0.
""",
        ["contacts"] = """
Uso: libro-cli [globales] contacts list|show|stats|save|archive

  list     [--query <texto>] [--type cliente|proveedor|ambos] [--include-archived]
  show     --id N
  stats    Totales de clientes, proveedores, archivados y por cobrar.
  save     --input <json> | --stdin
           Campos: id, type, name, rfc, email, phone, address, notes.
  archive  --id N [--restore]
""",
        ["products"] = """
Uso: libro-cli [globales] products list|low-stock|show|summary|save|toggle

  list        [--query <texto>] [--low] [--include-inactive]
  low-stock   Equivale a list --low.
  show        --id N  Devuelve producto y movimientos.
  summary     Valor, productos, unidades, bajo stock y agotados.
  save        --input <json> | --stdin
              Campos: id, name, sku, description, unit, priceCents,
              costCents, ivaRate, minStock, trackStock, initialStock.
  toggle      --id N [--activate | --deactivate]
""",
        ["transactions"] = """
Uso: libro-cli [globales] transactions list|show|totals|save|delete

  list    [--from] [--to] [--type ingreso|gasto] [--category N]
          [--query <texto>] [--limit N] [--offset N]
  show    --id N
  totals  Mismos filtros que list; resume el universo filtrado.
  save    --input <json> | --stdin
          Campos: id, date, type, categoryId, contactId, accountId,
          description, subtotalCents, ivaRate, method, reference.
  delete  --id N  Solo movimientos manuales.
""",
        ["invoices"] = """
Uso: libro-cli [globales] invoices list|show|overview|save|emit|cancel|delete|pay|delete-payment

  list            [--status borrador|emitida|pagada|cancelada]
                  [--query] [--from] [--to] [--limit] [--offset]
  show            --id N  Devuelve factura, conceptos y pagos.
  overview        Por cobrar, vencido, mes y borradores.
  save            --input <json> | --stdin [--emit]
                  Campos: id, contactId, date, dueDate, notes, paymentTerms,
                  lines[] (productId, description, qty, unitPriceCents, ivaRate).
  emit            --id N
  cancel          --id N --reason "<motivo>"
  delete          --id N  Solo borradores.
  pay             --id N [--date yyyy-MM-dd] --amount-cents N
                  [--account N] [--method <texto>] [--reference] [--notes]
  delete-payment  --id <id del pago>
""",
        ["inventory"] = """
Uso: libro-cli [globales] inventory list|summary|movements|movement

  list       Alias de products list.
  summary    Alias de products summary.
  movements  --id <producto>
  movement   --input <json> | --stdin
             Campos: productId, date, type (entrada|salida|ajuste), qty,
             unitCostCents, ivaRate, reason, reference, accountId.
""",
        ["employees"] = """
Uso: libro-cli [globales] employees list|show|summary|save|toggle

  list     [--include-inactive]
  show     --id N
  summary  Activos, inactivos y nómina mensual.
  save     --input <json> | --stdin
           Campos: id, name, rfc, curp, nss, position, department,
           salaryCents, startDate, endDate, status (activo|inactivo),
           bankName, bankAccount.
  toggle   --id N [--activate | --deactivate]
""",
        ["payroll"] = """
Uso: libro-cli [globales] payroll list|show|summary|create|recalc|update-item|delete-item|pay|revert|delete

  list          Periodos con estado y neto.
  show          --id N  Periodo y conceptos.
  summary       Borradores, pagadas, netos y empleados.
  create        [--year N] [--month 1..12] [--type mensual|quincenal]
                [--quincena 1|2]
  recalc        --id N
  update-item   --item-id N --period-id N [--days N] [--base-cents N]
                [--extra-cents N] [--isr-cents N] [--imss-cents N]
                [--infonavit-cents N] [--other-cents N] [--notes <texto>]
  delete-item   --item-id N --period-id N
  pay           --id N --account N [--date yyyy-MM-dd]
  revert        --id N
  delete        --id N
""",
        ["taxes"] = """
Uso: libro-cli [globales] taxes summary|annual

  summary  [--from yyyy-MM-dd --to yyyy-MM-dd]  Por defecto, mes actual.
  annual   [--year N]  Por defecto, año actual.
""",
        ["reports"] = """
Uso: libro-cli [globales] reports income|cashflow|aging|monthly|balances

  income    [--from --to]  Estado de resultados sin IVA.
  cashflow  [--from --to]  Flujo con IVA.
  aging                    Antigüedad de saldos por cobrar.
  monthly   [--from --to]  Por defecto, últimos 12 meses.
  balances                 Saldos calculados de cuentas.
""",
        ["users"] = """
Uso: libro-cli [globales] users list|summary|save|activate|deactivate|reset-password|roles

  list             Usuarios sin hash de contraseña.
  summary          Total, activos y administradores.
  save             [--id N] --name <texto> --email <correo>
                   --role admin|contador|capturista|consulta
                   [--new-password <clave>] [--inactive]
                   --new-password es obligatorio al crear.
  activate         --id N
  deactivate       --id N
  reset-password   --id N --new-password <clave>
  roles            Catálogo de roles y descripciones.
""",
        ["audit"] = """
Uso: libro-cli [globales] audit list [filtros]

Filtros: --query <texto> --action <acción> --entity <entidad>
         --from yyyy-MM-dd --to yyyy-MM-dd --limit N
El límite por defecto es 200 y se acota a 1..1000.
""",
        ["backup"] = """
Uso: libro-cli [globales] backup create|list|restore

  create   [--output <archivo.libro>]  Crea un respaldo cifrado y muestra SHA256.
  list     [--hash]  Lista solo la carpeta estándar de respaldos.
  restore  --file <ruta> --yes  Reemplaza la base y crea un respaldo de seguridad.
""",
        ["schema"] = """
Uso: libro-cli [globales] schema [operación]

Sin argumento lista todos los ejemplos. Operaciones disponibles:
company.update, account.save, category.save, contact.save, product.save,
transaction.save, invoice.save, inventory.movement, employee.save,
payroll.item.update, payment.create, user.save.
""",
    };

    public static void ShowHelp(CliContext ctx, ParsedArgs args)
    {
        var start = args.Positionals.Count > 0 &&
                    string.Equals(args.Positionals[0], "help", StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;

        if (args.Positionals.Count <= start)
        {
            Output.Emit(new { help = HelpText }, () => Console.WriteLine(HelpText));
            return;
        }

        var group = args.Positionals[start].ToLowerInvariant();
        if (!GroupHelp.TryGetValue(group, out var text))
            throw new CliException($"Comando desconocido: `{group}`. Usa `help`.");

        var action = args.Positionals.Count > start + 1
            ? args.Positionals[start + 1].ToLowerInvariant()
            : "";
        var title = action.Length > 0 ? $"Libro CLI — {group} {action}" : $"Libro CLI — {group}";
        var body = title + Environment.NewLine + Environment.NewLine + text +
                   Environment.NewLine + Environment.NewLine + HelpFooter;
        Output.Emit(new { help = body }, () => Console.WriteLine(body));
    }

    public static void Execute(CliContext ctx, ParsedArgs args)
    {
        var positionals = args.Positionals;
        if (positionals.Count == 0)
            throw new CliException("Falta un comando. Usa `help` o `doctor`.");

        var group = positionals[0].ToLowerInvariant();
        var action = positionals.Count > 1 ? positionals[1].ToLowerInvariant() : "";

        switch (group)
        {
            case "help":
                ShowHelp(ctx, args);
                break;
            case "version":
                Version(ctx);
                break;
            case "schema":
                Schema(ctx, positionals.Skip(1).ToArray());
                break;
            case "doctor":
                Doctor(ctx);
                break;
            case "auth":
                Auth(ctx, action);
                break;
            case "company":
                Company(ctx, action);
                break;
            case "dashboard":
                Dashboard(ctx);
                break;
            case "accounts":
                Accounts(ctx, action);
                break;
            case "categories":
                Categories(ctx, action);
                break;
            case "contacts":
                Contacts(ctx, action);
                break;
            case "products":
                Products(ctx, action);
                break;
            case "transactions":
                Transactions(ctx, action);
                break;
            case "invoices":
                Invoices(ctx, action);
                break;
            case "inventory":
                Inventory(ctx, action);
                break;
            case "employees":
                Employees(ctx, action);
                break;
            case "payroll":
                Payroll(ctx, action);
                break;
            case "taxes":
                Taxes(ctx, action);
                break;
            case "reports":
                Reports(ctx, action);
                break;
            case "users":
                Users(ctx, action);
                break;
            case "audit":
                Audit(ctx, action);
                break;
            case "backup":
                Backup(ctx, action);
                break;
            default:
                throw new CliException($"Comando desconocido: `{group}`. Usa `help`.");
        }
    }

    private static void Version(CliContext ctx)
    {
        var cli = typeof(Commands).Assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        var app = typeof(AppState).Assembly.GetName().Version?.ToString() ?? "desconocida";
        var data = new { cliVersion = cli, appVersion = app, runtime = Environment.Version.ToString() };
        Output.Emit(data, () =>
        {
            Console.WriteLine($"libro-cli {cli}");
            Console.WriteLine($"Libro {app}");
            Console.WriteLine($"Runtime .NET {Environment.Version}");
        });
    }

    private static void Doctor(CliContext ctx)
    {
        var dataDir = AppPaths.DataDirectory;
        var dbPath = AppPaths.DatabasePath;
        var keyPath = AppPaths.KeyPath;
        var dbExists = File.Exists(dbPath);
        var keyExists = File.Exists(keyPath);
        var kind = dbExists ? DatabaseKind(dbPath) : "missing";
        var result = new Dictionary<string, object?>
        {
            ["dataDir"] = dataDir,
            ["databasePath"] = dbPath,
            ["keyPath"] = keyPath,
            ["databaseExists"] = dbExists,
            ["keyExists"] = keyExists,
            ["databaseKind"] = kind,
            ["sizeBytes"] = dbExists ? new FileInfo(dbPath).Length : 0,
            ["backupCount"] = Directory.Exists(AppPaths.BackupDirectory)
                ? Directory.GetFiles(AppPaths.BackupDirectory, "*.libro").Length
                : 0,
            ["opened"] = false,
        };

        Dictionary<string, long>? counts = null;
        string? openError = null;
        if (dbExists && keyExists && kind == "encrypted")
        {
            try
            {
                ctx.Initialize();
                counts = new Dictionary<string, long>
                {
                    ["users"] = Count("users"),
                    ["accounts"] = Count("accounts"),
                    ["categories"] = Count("categories"),
                    ["contacts"] = Count("contacts"),
                    ["products"] = Count("products"),
                    ["transactions"] = Count("transactions"),
                    ["invoices"] = Count("invoices"),
                    ["employees"] = Count("employees"),
                    ["payrollPeriods"] = Count("payroll_periods"),
                };
                result["opened"] = true;
                result["company"] = AppState.Company.Name;
                result["counts"] = counts;
            }
            catch (Exception ex)
            {
                openError = ex.Message;
                result["openError"] = ex.Message;
            }
        }
        else if (kind == "plaintext")
        {
            result["note"] = "La base está en texto plano. El primer comando que la abra la migrará a SQLCipher.";
        }
        else if (dbExists && !keyExists)
        {
            result["note"] = "Falta clave.bin. No se abrirá la base para no crear una llave nueva incompatible.";
        }

        Output.Emit(result, () =>
        {
            Console.WriteLine("Libro — diagnóstico");
            Console.WriteLine("------------------");
            Console.WriteLine($"Datos:        {dataDir}");
            Console.WriteLine($"Base:         {dbPath}");
            Console.WriteLine($"Llave:        {keyPath}");
            Console.WriteLine($"Base existe:  {YesNo(dbExists)}");
            Console.WriteLine($"Llave existe: {YesNo(keyExists)}");
            Console.WriteLine($"Tipo:         {kind}");
            if (dbExists) Console.WriteLine($"Tamaño:       {new FileInfo(dbPath).Length:N0} bytes");
            Console.WriteLine($"Respaldos:    {result["backupCount"]}");
            if (openError is not null)
            {
                Console.WriteLine($"No se pudo abrir: {openError}");
                return;
            }

            if (counts is not null)
            {
                Console.WriteLine($"Empresa:      {result["company"]}");
                Console.WriteLine();
                Console.WriteLine("Conteos:");
                foreach (var (key, value) in counts)
                    Console.WriteLine($"  {key,-18} {value,8:N0}");
            }
            else if (result.TryGetValue("note", out var note) && note is not null)
            {
                Console.WriteLine();
                Console.WriteLine(note);
            }
        });
    }

    private static string DatabaseKind(string path)
    {
        using var stream = File.OpenRead(path);
        var header = new byte[16];
        var read = stream.Read(header, 0, header.Length);
        if (read == 0) return "empty";
        return read >= 16 && header.AsSpan().SequenceEqual(Encoding.ASCII.GetBytes("SQLite format 3\0"))
            ? "plaintext"
            : "encrypted";
    }

    private static long Count(string table) => AppState.Db.ScalarLong($"SELECT COUNT(*) FROM {table};");

    private static void Auth(CliContext ctx, string action)
    {
        switch (action)
        {
            case "setup":
            {
                ctx.Initialize();
                if (AuthService.UserCount(AppState.Db) > 0)
                    throw new CliException("Ya existe un administrador. Usa `auth login`.");
                var company = ctx.Args.RequireText("company", "Nombre del negocio.");
                var name = ctx.Args.RequireText("name", "Nombre del administrador.");
                var email = ctx.Email;
                if (string.IsNullOrWhiteSpace(email))
                    throw new CliException("Falta --email del administrador.");
                var password = RequirePassword(ctx, "Contraseña", confirm: true);
                var user = AuthService.Setup(AppState.Db, company, name, email, password);
                AppState.SetCurrentUser(user);
                ctx.RememberLogin(user);
                AppState.RefreshCompany();
                EmitAuth(user, "Cuenta inicial creada.");
                break;
            }
            case "login":
            {
                ctx.Initialize();
                var email = ctx.Args.FirstText("email")
                            ?? Environment.GetEnvironmentVariable("LIBRO_EMAIL")
                            ?? Prompt(ctx, "Correo");
                var password = ctx.Password;
                if (string.IsNullOrEmpty(password) && ctx.Interactive)
                    password = ReadPassword("Contraseña");
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
                    throw new CliException(
                        "Faltan credenciales. Usa --email/--password, LIBRO_EMAIL/LIBRO_PASSWORD o el modo REPL.");
                var result = AuthService.Login(AppState.Db, email, password);
                if (result.User is null)
                    throw new CliException(result.Error ?? "No se pudo iniciar sesión.");
                AppState.SetCurrentUser(result.User);
                ctx.RememberLogin(result.User);
                EmitAuth(result.User, "Sesión iniciada.");
                break;
            }
            case "status":
            case "whoami":
            {
                ctx.TryAutoLogin(required: false);
                // Permite `auth status --email ... --password ...` después del comando.
                if (AppState.CurrentUser is null &&
                    !string.IsNullOrWhiteSpace(ctx.Email) &&
                    !string.IsNullOrEmpty(ctx.Password))
                {
                    var result = AuthService.Login(AppState.Db, ctx.Email!, ctx.Password!);
                    if (result.User is not null)
                    {
                        AppState.SetCurrentUser(result.User);
                        ctx.RememberLogin(result.User);
                    }
                }

                var current = ctx.LastLogin is { } remembered &&
                              remembered.Id == AppState.CurrentUser?.Id
                    ? remembered
                    : AppState.CurrentUser;
                if (current is { } user)
                    EmitAuth(user, "Sesión activa.");
                else
                {
                    var data = new { authenticated = false };
                    Output.Emit(data, () => Console.WriteLine("Sin sesión activa."));
                }
                break;
            }
            case "logout":
                AppState.SignOut();
                Output.Emit(new { authenticated = false }, () => Console.WriteLine("Sesión cerrada."));
                break;
            case "change-password":
            {
                var user = ctx.RequireUser();
                var current = ctx.Password;
                if (string.IsNullOrEmpty(current) && ctx.Interactive)
                    current = ReadPassword("Contraseña actual");
                if (string.IsNullOrEmpty(current))
                    throw new CliException("Falta la contraseña actual (--password o LIBRO_PASSWORD).");
                var next = ctx.Args.FirstText("new-password");
                if (string.IsNullOrWhiteSpace(next) && ctx.Interactive)
                    next = ReadPassword("Contraseña nueva", confirm: true);
                if (string.IsNullOrWhiteSpace(next))
                    throw new CliException("Falta --new-password.");
                var error = AuthService.ChangePassword(AppState.Db, user.Id, current, next);
                if (error is not null) throw new CliException(error);
                Output.Emit(new { changed = true }, () => Console.WriteLine("Contraseña actualizada."));
                break;
            }
            default:
                throw new CliException("Uso: auth setup|login|status|logout|change-password|whoami");
        }
    }

    private static void EmitAuth(User user, string message)
    {
        var data = new
        {
            authenticated = true,
            user = UserView.From(user),
            company = AppState.Company.Name,
        };
        Output.Emit(data, () =>
        {
            Console.WriteLine(message);
            Console.WriteLine($"  Usuario: {user.Name} <{user.Email}>");
            Console.WriteLine($"  Rol:     {Permissions.RoleLabel(user.Role)}");
            Console.WriteLine($"  Empresa: {AppState.Company.Name}");
        });
    }

    private static string RequirePassword(CliContext ctx, string label, bool confirm)
    {
        var value = ctx.Password;
        if (string.IsNullOrEmpty(value) && ctx.Interactive)
            value = ReadPassword(label, confirm);
        if (string.IsNullOrEmpty(value))
            throw new CliException(
                $"Falta la contraseña. Usa --password, --password-env o LIBRO_PASSWORD; en REPL se pedirá de forma interactiva.");
        return value;
    }

    private static string Prompt(CliContext ctx, string label)
    {
        if (!ctx.Interactive) return "";
        Console.Write($"{label}: ");
        return Console.ReadLine()?.Trim() ?? "";
    }

    private static string ReadPassword(string label, bool confirm = false)
    {
        var first = ReadPasswordOnce(label);
        if (!confirm) return first;
        var second = ReadPasswordOnce("Repite la contraseña");
        if (!string.Equals(first, second, StringComparison.Ordinal))
            throw new CliException("Las contraseñas no coinciden.");
        return first;
    }

    private static string ReadPasswordOnce(string label)
    {
        Console.Write($"{label}: ");
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0) chars.RemoveAt(chars.Count - 1);
                continue;
            }
            if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
        }
        return new string(chars.ToArray());
    }

    private static void Company(CliContext ctx, string action)
    {
        switch (action)
        {
            case "show":
                ctx.RequireUser();
                EmitCompany(AppState.Company);
                break;
            case "update":
            {
                ctx.RequirePermission(Permissions.SettingsManage);
                var company = ctx.ReadInput<Company>("company.update");
                company.Id = 1;
                CompanyService.SaveCompany(AppState.Db, company, AppState.UserId);
                AppState.RefreshCompany();
                EmitCompany(AppState.Company);
                break;
            }
            default:
                throw new CliException("Uso: company show|update --input archivo.json");
        }
    }

    private static void EmitCompany(Company company)
    {
        Output.Emit(company, () =>
        {
            Output.KeyValues(new (string, object?)[]
            {
                ("Nombre", company.Name),
                ("RFC", company.Rfc),
                ("Régimen fiscal", company.RegimenFiscal),
                ("Dirección", company.Address),
                ("Teléfono", company.Phone),
                ("Correo", company.Email),
                ("Tasa IVA", company.IvaRate.ToString("P0", System.Globalization.CultureInfo.GetCultureInfo("es-MX"))),
                ("Serie facturas", company.InvoiceSeries),
                ("Próximo folio", company.NextInvoiceNumber),
                ("Tasa IMSS", company.ImssRate.ToString("P1", System.Globalization.CultureInfo.GetCultureInfo("es-MX"))),
            });
        });
    }

    private static void Dashboard(CliContext ctx)
    {
        ctx.RequireUser();
        var data = DashboardService.GetDashboard(AppState.Db);
        Output.Emit(data, () =>
        {
            Output.KeyValues(new (string, object?)[]
            {
                ("Periodo", $"{data.RangeStart} a {data.RangeEnd}"),
                ("Ingresos facturados", Output.Money(data.RevenueCents)),
                ("Otros ingresos", Output.Money(data.OtherIncomeCents)),
                ("Gastos", Output.Money(data.ExpenseCents)),
                ("Utilidad", Output.Money(data.ProfitCents)),
                ("IVA trasladado", Output.Money(data.IvaTrasladadoCents)),
                ("IVA acreditable", Output.Money(data.IvaAcreditableCents)),
                ("Por cobrar", Output.Money(data.ReceivableCents)),
                ("Facturas vencidas", data.OverdueCount),
                ("Efectivo", Output.Money(data.CashCents)),
                ("Inventario", Output.Money(data.InventoryCents)),
            });
            if (data.LowStock.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Bajo stock:");
                Output.Table(data.LowStock,
                    ("SKU", product => product.Sku),
                    ("Producto", product => product.Name),
                    ("Stock", product => product.StockLabel));
            }
        });
    }

    private static void Accounts(CliContext ctx, string action)
    {
        switch (action)
        {
            case "list":
            {
                ctx.RequireUser();
                var rows = CompanyService.GetAccounts(AppState.Db, ctx.Args.Has("include-inactive"));
                Output.Emit(rows, () => Output.Table(rows,
                    ("ID", account => account.Id.ToString()),
                    ("Nombre", account => account.Name),
                    ("Tipo", account => account.Type),
                    ("Saldo inicial", account => Output.Money(account.OpeningBalanceCents)),
                    ("Activa", account => account.Active == 1 ? "Sí" : "No")));
                break;
            }
            case "balances":
            {
                ctx.RequireUser();
                var rows = CompanyService.GetAccountBalances(AppState.Db, ctx.Args.Has("include-inactive"));
                Output.Emit(rows, () => Output.Table(rows,
                    ("ID", account => account.Id.ToString()),
                    ("Nombre", account => account.Name),
                    ("Tipo", account => account.Type),
                    ("Saldo", account => Output.Money(account.BalanceCents)),
                    ("Activa", account => account.Active == 1 ? "Sí" : "No")));
                break;
            }
            case "save":
            {
                ctx.RequireAnyPermission(Permissions.AccountsWrite, Permissions.SettingsManage);
                var account = ctx.ReadInput<Account>("account.save");
                if (account.Id == 0) account.Active = 1;
                CompanyService.SaveAccount(AppState.Db, account, AppState.UserId);
                var id = account.Id > 0
                    ? account.Id
                    : AppState.Db.ScalarLong(
                        "SELECT id FROM accounts WHERE name = $name ORDER BY id DESC LIMIT 1;",
                        new { name = account.Name.Trim() });
                var saved = CompanyService.GetAccounts(AppState.Db, true).FirstOrDefault(item => item.Id == id)
                            ?? account;
                Output.Emit(saved, () =>
                {
                    Console.WriteLine($"Cuenta guardada: #{saved.Id} {saved.Name}");
                });
                break;
            }
            case "archive":
            {
                ctx.RequireAnyPermission(Permissions.AccountsWrite, Permissions.SettingsManage);
                var id = ctx.Args.RequireLong("id", "ID de la cuenta.");
                var restore = ctx.Args.Has("restore");
                CompanyService.ArchiveAccount(AppState.Db, id, !restore, AppState.UserId);
                Output.Emit(new { id, active = restore }, () =>
                    Console.WriteLine(restore ? $"Cuenta #{id} restaurada." : $"Cuenta #{id} desactivada."));
                break;
            }
            default:
                throw new CliException("Uso: accounts list|balances|save|archive [--id N] [--restore]");
        }
    }

    private static void Categories(CliContext ctx, string action)
    {
        switch (action)
        {
            case "list":
            {
                ctx.RequireUser();
                var rows = CompanyService.GetCategories(AppState.Db, ctx.Args.Has("include-archived"));
                Output.Emit(rows, () => Output.Table(rows,
                    ("ID", category => category.Id.ToString()),
                    ("Nombre", category => category.Name),
                    ("Tipo", category => category.Kind),
                    ("Archivada", category => category.Archived == 1 ? "Sí" : "No")));
                break;
            }
            case "save":
            {
                ctx.RequireAnyPermission(Permissions.AccountsWrite, Permissions.SettingsManage);
                var category = ctx.ReadInput<Category>("category.save");
                if (category.Id == 0) category.Archived = 0;
                if (category.Kind is not ("ingreso" or "gasto"))
                    throw new CliException("category.kind debe ser ingreso o gasto.");
                CompanyService.SaveCategory(AppState.Db, category, AppState.UserId);
                var id = category.Id > 0
                    ? category.Id
                    : AppState.Db.ScalarLong(
                        "SELECT id FROM categories WHERE name = $name ORDER BY id DESC LIMIT 1;",
                        new { name = category.Name.Trim() });
                var saved = CompanyService.GetCategories(AppState.Db, true).FirstOrDefault(item => item.Id == id)
                            ?? category;
                Output.Emit(saved, () => Console.WriteLine($"Categoría guardada: #{saved.Id} {saved.Name}"));
                break;
            }
            default:
                throw new CliException("Uso: categories list|save --input archivo.json");
        }
    }

    private static void Contacts(CliContext ctx, string action)
    {
        switch (action)
        {
            case "list":
            {
                ctx.RequireUser();
                var rows = ContactService.GetContacts(new ContactFilters
                {
                    Query = ctx.Args.Text("query"),
                    Type = ctx.Args.Text("type"),
                    IncludeArchived = ctx.Args.Has("include-archived"),
                });
                Output.Emit(rows, () => Output.Table(rows,
                    ("ID", contact => contact.Id.ToString()),
                    ("Nombre", contact => contact.Name),
                    ("Tipo", contact => contact.Type),
                    ("RFC", contact => contact.Rfc),
                    ("Correo", contact => contact.Email),
                    ("Por cobrar", contact => Output.Money(contact.ReceivableCents))));
                break;
            }
            case "show":
            {
                ctx.RequireUser();
                var id = ctx.Args.RequireLong("id", "ID del contacto.");
                var contact = ContactService.GetContact(id)
                              ?? throw new CliException($"No existe el contacto #{id}.");
                Output.Emit(contact, () => Output.KeyValues(new (string, object?)[]
                {
                    ("ID", contact.Id),
                    ("Nombre", contact.Name),
                    ("Tipo", contact.Type),
                    ("RFC", contact.Rfc),
                    ("Correo", contact.Email),
                    ("Teléfono", contact.Phone),
                    ("Dirección", contact.Address),
                    ("Notas", contact.Notes),
                    ("Archivado", contact.Archived == 1 ? "Sí" : "No"),
                    ("Por cobrar", Output.Money(contact.ReceivableCents)),
                }));
                break;
            }
            case "stats":
            {
                ctx.RequireUser();
                var stats = ContactService.GetStats();
                Output.Emit(stats, () => Output.KeyValues(new (string, object?)[]
                {
                    ("Clientes", stats.Clients),
                    ("Proveedores", stats.Providers),
                    ("Archivados", stats.Archived),
                    ("Por cobrar", Output.Money(stats.ReceivableCents)),
                }));
                break;
            }
            case "save":
            {
                ctx.RequirePermission(Permissions.ContactsWrite);
                var contact = ctx.ReadInput<Contact>("contact.save");
                if (contact.Id == 0) contact.Archived = 0;
                var id = ContactService.Save(contact, AppState.UserId);
                var saved = ContactService.GetContact(id)
                            ?? throw new CliException("El contacto se guardó pero no se pudo recargar.");
                Output.Emit(saved, () => Console.WriteLine($"Contacto guardado: #{saved.Id} {saved.Name}"));
                break;
            }
            case "archive":
            {
                ctx.RequirePermission(Permissions.ContactsWrite);
                var id = ctx.Args.RequireLong("id", "ID del contacto.");
                var restore = ctx.Args.Has("restore");
                ContactService.SetArchived(id, !restore, AppState.UserId);
                Output.Emit(new { id, archived = !restore }, () =>
                    Console.WriteLine(restore ? $"Contacto #{id} restaurado." : $"Contacto #{id} archivado."));
                break;
            }
            default:
                throw new CliException("Uso: contacts list|show|stats|save|archive");
        }
    }

    private static void Products(CliContext ctx, string action)
    {
        switch (action)
        {
            case "list":
            case "low-stock":
            {
                ctx.RequireUser();
                var rows = InventoryService.GetProducts(new InventoryFilters
                {
                    Query = ctx.Args.Text("query"),
                    OnlyLow = ctx.Args.Has("low") || action == "low-stock",
                    IncludeInactive = ctx.Args.Has("include-inactive"),
                });
                Output.Emit(rows, () => Output.Table(rows,
                    ("ID", product => product.Id.ToString()),
                    ("SKU", product => product.Sku),
                    ("Producto", product => product.Name),
                    ("Stock", product => product.StockLabel),
                    ("Precio", product => Output.Money(product.PriceCents)),
                    ("Costo", product => Output.Money(product.CostCents)),
                    ("Estado", product => product.InventoryStatus)));
                break;
            }
            case "show":
            {
                ctx.RequireUser();
                var id = ctx.Args.RequireLong("id", "ID del producto.");
                var product = InventoryService.GetProduct(id)
                              ?? throw new CliException($"No existe el producto #{id}.");
                var movements = InventoryService.GetMovements(id);
                var data = new { product, movements };
                Output.Emit(data, () =>
                {
                    Output.KeyValues(new (string, object?)[]
                    {
                        ("ID", product.Id),
                        ("SKU", product.Sku),
                        ("Producto", product.Name),
                        ("Unidad", product.Unit),
                        ("Precio", Output.Money(product.PriceCents)),
                        ("Costo", Output.Money(product.CostCents)),
                        ("IVA", product.IvaRate.ToString("P0")),
                        ("Stock", product.StockLabel),
                        ("Mínimo", Output.Qty(product.MinStock)),
                        ("Estado", product.InventoryStatus),
                        ("Activo", product.Active == 1 ? "Sí" : "No"),
                    });
                    Console.WriteLine();
                    Console.WriteLine("Movimientos:");
                    Output.Table(movements,
                        ("Fecha", movement => movement.Date),
                        ("Tipo", movement => movement.TypeLabel),
                        ("Cantidad", movement => movement.QtyLabel),
                        ("Costo", movement => Output.Money(movement.UnitCostCents)),
                        ("Motivo", movement => Output.Truncate(movement.Reason)));
                });
                break;
            }
            case "summary":
            {
                ctx.RequireUser();
                var summary = InventoryService.GetSummary();
                Output.Emit(summary, () => Output.KeyValues(new (string, object?)[]
                {
                    ("Valor inventario", Output.Money(summary.ValueCents)),
                    ("Productos", summary.ProductCount),
                    ("Unidades", Output.Qty(summary.UnitCount)),
                    ("Bajo stock", summary.LowStockCount),
                    ("Agotados", summary.OutOfStockCount),
                }));
                break;
            }
            case "save":
            {
                ctx.RequireAnyPermission(Permissions.InventoryWrite, Permissions.ProductsWrite);
                var input = ctx.ReadInput<ProductInput>("product.save");
                var id = InventoryService.SaveProduct(input, AppState.UserId);
                var product = InventoryService.GetProduct(id)
                              ?? throw new CliException("El producto se guardó pero no se pudo recargar.");
                Output.Emit(product, () => Console.WriteLine($"Producto guardado: #{product.Id} {product.Name}"));
                break;
            }
            case "toggle":
            {
                ctx.RequireAnyPermission(Permissions.InventoryWrite, Permissions.ProductsWrite);
                var id = ctx.Args.RequireLong("id", "ID del producto.");
                var product = InventoryService.GetProduct(id)
                              ?? throw new CliException($"No existe el producto #{id}.");
                var active = ctx.Args.Has("activate")
                    ? true
                    : ctx.Args.Has("deactivate")
                        ? false
                        : product.Active != 1;
                InventoryService.ToggleActive(id, active, AppState.UserId);
                Output.Emit(new { id, active }, () =>
                    Console.WriteLine(active ? $"Producto #{id} activado." : $"Producto #{id} desactivado."));
                break;
            }
            default:
                throw new CliException("Uso: products list|low-stock|show|summary|save|toggle");
        }
    }

    private static void Transactions(CliContext ctx, string action)
    {
        switch (action)
        {
            case "list":
            {
                ctx.RequireUser();
                var (limit, offset) = Paging(ctx.Args);
                var page = TransactionService.GetTransactions(Filters(ctx.Args), limit, offset);
                Output.Emit(page, () => Output.Table(page.Rows,
                    ("ID", row => row.Id.ToString()),
                    ("Fecha", row => row.Date),
                    ("Tipo", row => row.Type),
                    ("Descripción", row => Output.Truncate(row.Description)),
                    ("Categoría", row => row.CategoryName ?? ""),
                    ("Cuenta", row => row.AccountName ?? ""),
                    ("Total", row => Output.Money(row.TotalCents))));
                break;
            }
            case "show":
            {
                ctx.RequireUser();
                var id = ctx.Args.RequireLong("id", "ID del movimiento.");
                var row = TransactionService.GetTransaction(id)
                          ?? throw new CliException($"No existe el movimiento #{id}.");
                Output.Emit(row, () => Output.KeyValues(new (string, object?)[]
                {
                    ("ID", row.Id),
                    ("Fecha", row.Date),
                    ("Tipo", row.Type),
                    ("Descripción", row.Description),
                    ("Categoría", row.CategoryName ?? ""),
                    ("Contacto", row.ContactName ?? ""),
                    ("Cuenta", row.AccountName ?? ""),
                    ("Subtotal", Output.Money(row.SubtotalCents)),
                    ("IVA", Output.Money(row.IvaCents)),
                    ("Total", Output.Money(row.TotalCents)),
                    ("Método", row.Method),
                    ("Referencia", row.Reference),
                    ("Origen", row.Source),
                }));
                break;
            }
            case "totals":
            {
                ctx.RequireUser();
                var totals = TransactionService.GetTotals(Filters(ctx.Args));
                Output.Emit(totals, () => Output.KeyValues(new (string, object?)[]
                {
                    ("Ingresos subtotal", Output.Money(totals.IncomeSubtotal)),
                    ("Ingresos IVA", Output.Money(totals.IncomeIva)),
                    ("Gastos subtotal", Output.Money(totals.ExpenseSubtotal)),
                    ("Gastos IVA", Output.Money(totals.ExpenseIva)),
                    ("Neto", Output.Money(totals.Net)),
                    ("IVA neto", Output.Money(totals.NetIva)),
                    ("Movimientos", totals.Count),
                }));
                break;
            }
            case "save":
            {
                ctx.RequirePermission(Permissions.TxWrite);
                var input = ctx.ReadInput<TransactionRow>("transaction.save");
                var id = TransactionService.Save(input, AppState.UserId);
                var row = TransactionService.GetTransaction(id)
                          ?? throw new CliException("El movimiento se guardó pero no se pudo recargar.");
                Output.Emit(row, () => Console.WriteLine($"Movimiento guardado: #{row.Id} {row.Description}"));
                break;
            }
            case "delete":
            {
                ctx.RequirePermission(Permissions.TxWrite);
                var id = ctx.Args.RequireLong("id", "ID del movimiento.");
                TransactionService.Delete(id, AppState.UserId);
                Output.Emit(new { deleted = true, id }, () => Console.WriteLine($"Movimiento #{id} eliminado."));
                break;
            }
            default:
                throw new CliException("Uso: transactions list|show|totals|save|delete");
        }
    }

    private static TransactionFilters Filters(ParsedArgs args) => new()
    {
        From = args.Text("from"),
        To = args.Text("to"),
        Type = args.Text("type"),
        CategoryId = args.OptionalLong("category"),
        Query = args.Text("query"),
    };

    private static void Invoices(CliContext ctx, string action)
    {
        switch (action)
        {
            case "list":
            {
                ctx.RequireUser();
                var (limit, offset) = Paging(ctx.Args);
                var page = InvoiceService.GetInvoices(new InvoiceFilters
                {
                    Status = ctx.Args.Text("status"),
                    Query = ctx.Args.Text("query"),
                    From = ctx.Args.Text("from"),
                    To = ctx.Args.Text("to"),
                }, limit, offset);
                Output.Emit(page, () => Output.Table(page.Rows,
                    ("ID", invoice => invoice.Id.ToString()),
                    ("Folio", invoice => invoice.DisplayFolio),
                    ("Cliente", invoice => invoice.ContactName ?? ""),
                    ("Fecha", invoice => invoice.Date),
                    ("Estado", invoice => invoice.Status),
                    ("Total", invoice => Output.Money(invoice.TotalCents)),
                    ("Saldo", invoice => Output.Money(invoice.BalanceCents))));
                break;
            }
            case "show":
            {
                ctx.RequireUser();
                var id = ctx.Args.RequireLong("id", "ID de la factura.");
                var invoice = InvoiceService.GetInvoice(id)
                              ?? throw new CliException($"No existe la factura #{id}.");
                var items = InvoiceService.GetItems(id);
                var payments = InvoiceService.GetPayments(id);
                var data = new { invoice, items, payments };
                Output.Emit(data, () =>
                {
                    Output.KeyValues(new (string, object?)[]
                    {
                        ("ID", invoice.Id),
                        ("Folio", invoice.DisplayFolio),
                        ("Cliente", invoice.ContactName ?? ""),
                        ("RFC", invoice.ContactRfc ?? ""),
                        ("Fecha", invoice.Date),
                        ("Vencimiento", invoice.DueDate),
                        ("Estado", invoice.Status),
                        ("Subtotal", Output.Money(invoice.SubtotalCents)),
                        ("IVA", Output.Money(invoice.IvaCents)),
                        ("Total", Output.Money(invoice.TotalCents)),
                        ("Pagado", Output.Money(invoice.PaidCents)),
                        ("Saldo", Output.Money(invoice.BalanceCents)),
                        ("Notas", invoice.Notes),
                    });
                    Console.WriteLine();
                    Console.WriteLine("Conceptos:");
                    Output.Table(items,
                        ("Descripción", item => item.Description),
                        ("Cantidad", item => Output.Qty(item.Qty)),
                        ("Precio", item => Output.Money(item.UnitPriceCents)),
                        ("IVA", item => Output.Money(item.IvaCents)),
                        ("Total", item => Output.Money(item.TotalCents)));
                    if (payments.Count > 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Pagos:");
                        Output.Table(payments,
                            ("ID", payment => payment.Id.ToString()),
                            ("Fecha", payment => payment.Date),
                            ("Importe", payment => Output.Money(payment.AmountCents)),
                            ("Método", payment => payment.Method),
                            ("Cuenta", payment => payment.AccountName ?? ""));
                    }
                });
                break;
            }
            case "overview":
            {
                ctx.RequireUser();
                var overview = InvoiceService.GetOverview();
                Output.Emit(overview, () => Output.KeyValues(new (string, object?)[]
                {
                    ("Por cobrar", Output.Money(overview.ReceivableCents)),
                    ("Vencido", Output.Money(overview.OverdueCents)),
                    ("Facturas vencidas", overview.OverdueCount),
                    ("Total del mes", Output.Money(overview.MonthTotalCents)),
                    ("Facturas del mes", overview.MonthCount),
                    ("Borradores", overview.DraftCount),
                }));
                break;
            }
            case "save":
            {
                ctx.RequirePermission(Permissions.InvoiceWrite);
                var input = ctx.ReadInput<InvoiceInput>("invoice.save");
                var id = InvoiceService.Save(input, ctx.Args.Has("emit"), AppState.UserId);
                var invoice = InvoiceService.GetInvoice(id)
                              ?? throw new CliException("La factura se guardó pero no se pudo recargar.");
                Output.Emit(invoice, () => Console.WriteLine($"Factura guardada: #{invoice.Id} {invoice.DisplayFolio}"));
                break;
            }
            case "emit":
            {
                ctx.RequirePermission(Permissions.InvoiceWrite);
                var id = ctx.Args.RequireLong("id", "ID de la factura.");
                InvoiceService.Emit(id, AppState.UserId);
                var invoice = InvoiceService.GetInvoice(id)
                              ?? throw new CliException("La factura se emitió pero no se pudo recargar.");
                Output.Emit(invoice, () => Console.WriteLine($"Factura emitida: {invoice.DisplayFolio}"));
                break;
            }
            case "cancel":
            {
                ctx.RequirePermission(Permissions.InvoiceWrite);
                var id = ctx.Args.RequireLong("id", "ID de la factura.");
                var reason = ctx.Args.RequireText("reason", "Motivo de la cancelación.");
                InvoiceService.Cancel(id, reason, AppState.UserId);
                var invoice = InvoiceService.GetInvoice(id)
                              ?? throw new CliException("La factura se canceló pero no se pudo recargar.");
                Output.Emit(invoice, () => Console.WriteLine($"Factura cancelada: {invoice.DisplayFolio}"));
                break;
            }
            case "delete":
            {
                ctx.RequirePermission(Permissions.InvoiceWrite);
                var id = ctx.Args.RequireLong("id", "ID de la factura.");
                InvoiceService.DeleteDraft(id, AppState.UserId);
                Output.Emit(new { deleted = true, id }, () => Console.WriteLine($"Borrador #{id} eliminado."));
                break;
            }
            case "pay":
            {
                ctx.RequirePermission(Permissions.InvoiceWrite);
                var id = ctx.Args.RequireLong("id", "ID de la factura.");
                var date = ctx.Args.Text("date") ?? Dates.TodayIso();
                var amount = ctx.Args.RequireLong("amount-cents", "Importe exacto del pago en centavos.");
                var accountId = ctx.Args.OptionalLong("account");
                InvoiceService.RegisterPayment(
                    id,
                    date,
                    amount,
                    accountId,
                    ctx.Args.Text("method") ?? "Transferencia",
                    ctx.Args.Text("reference") ?? "",
                    ctx.Args.Text("notes") ?? "",
                    AppState.UserId);
                var invoice = InvoiceService.GetInvoice(id)
                              ?? throw new CliException("El pago se registró pero no se pudo recargar la factura.");
                Output.Emit(invoice, () => Console.WriteLine(
                    $"Pago registrado en {invoice.DisplayFolio}. Saldo: {Output.Money(invoice.BalanceCents)}"));
                break;
            }
            case "delete-payment":
            {
                ctx.RequirePermission(Permissions.InvoiceWrite);
                var id = ctx.Args.RequireLong("id", "ID del pago.");
                InvoiceService.DeletePayment(id, AppState.UserId);
                Output.Emit(new { deleted = true, id }, () => Console.WriteLine($"Pago #{id} eliminado."));
                break;
            }
            default:
                throw new CliException("Uso: invoices list|show|overview|save|emit|cancel|delete|pay|delete-payment");
        }
    }

    private static void Inventory(CliContext ctx, string action)
    {
        switch (action)
        {
            case "list":
                Products(ctx, "list");
                break;
            case "summary":
                Products(ctx, "summary");
                break;
            case "movements":
            {
                ctx.RequireUser();
                var id = ctx.Args.RequireLong("id", "ID del producto.");
                var rows = InventoryService.GetMovements(id);
                Output.Emit(rows, () => Output.Table(rows,
                    ("ID", movement => movement.Id.ToString()),
                    ("Fecha", movement => movement.Date),
                    ("Tipo", movement => movement.TypeLabel),
                    ("Cantidad", movement => movement.QtyLabel),
                    ("Costo", movement => Output.Money(movement.UnitCostCents)),
                    ("Motivo", movement => Output.Truncate(movement.Reason)),
                    ("Referencia", movement => movement.Reference)));
                break;
            }
            case "movement":
            {
                ctx.RequireAnyPermission(Permissions.InventoryWrite, Permissions.ProductsWrite);
                var input = ctx.ReadInput<InventoryMovementInput>("inventory.movement");
                var id = InventoryService.AddMovement(input, AppState.UserId);
                var product = InventoryService.GetProduct(input.ProductId)
                              ?? throw new CliException("El movimiento se guardó pero no se pudo recargar el producto.");
                Output.Emit(new { id, product }, () => Console.WriteLine(
                    $"Movimiento #{id} registrado. Stock de {product.Name}: {product.StockLabel}"));
                break;
            }
            default:
                throw new CliException("Uso: inventory list|summary|movements|movement");
        }
    }

    private static void Employees(CliContext ctx, string action)
    {
        switch (action)
        {
            case "list":
            {
                ctx.RequirePermission(Permissions.PayrollView);
                var rows = EmployeeService.GetEmployees(ctx.Args.Has("include-inactive"));
                Output.Emit(rows, () => Output.Table(rows,
                    ("ID", employee => employee.Id.ToString()),
                    ("Nombre", employee => employee.Name),
                    ("Puesto", employee => employee.Position),
                    ("Departamento", employee => employee.Department),
                    ("Sueldo", employee => Output.Money(employee.SalaryCents)),
                    ("Estado", employee => employee.StatusLabel)));
                break;
            }
            case "show":
            {
                ctx.RequirePermission(Permissions.PayrollView);
                var id = ctx.Args.RequireLong("id", "ID del empleado.");
                var employee = EmployeeService.GetEmployee(id)
                               ?? throw new CliException($"No existe el empleado #{id}.");
                Output.Emit(employee, () => Output.KeyValues(new (string, object?)[]
                {
                    ("ID", employee.Id),
                    ("Nombre", employee.Name),
                    ("RFC", employee.Rfc),
                    ("CURP", employee.Curp),
                    ("NSS", employee.Nss),
                    ("Puesto", employee.Position),
                    ("Departamento", employee.Department),
                    ("Sueldo", Output.Money(employee.SalaryCents)),
                    ("Ingreso", employee.StartDate),
                    ("Estado", employee.StatusLabel),
                    ("Banco", employee.BankName),
                    ("Cuenta", employee.BankAccount),
                }));
                break;
            }
            case "summary":
            {
                ctx.RequirePermission(Permissions.PayrollView);
                var summary = EmployeeService.GetSummary();
                Output.Emit(summary, () => Output.KeyValues(new (string, object?)[]
                {
                    ("Activos", summary.ActiveCount),
                    ("Inactivos", summary.InactiveCount),
                    ("Nómina mensual", Output.Money(summary.MonthlyPayrollCents)),
                }));
                break;
            }
            case "save":
            {
                ctx.RequirePermission(Permissions.PayrollWrite);
                var input = ctx.ReadInput<EmployeeInput>("employee.save");
                var id = EmployeeService.SaveEmployee(input);
                var employee = EmployeeService.GetEmployee(id)
                               ?? throw new CliException("El empleado se guardó pero no se pudo recargar.");
                Output.Emit(employee, () => Console.WriteLine($"Empleado guardado: #{employee.Id} {employee.Name}"));
                break;
            }
            case "toggle":
            {
                ctx.RequirePermission(Permissions.PayrollWrite);
                var id = ctx.Args.RequireLong("id", "ID del empleado.");
                var employee = EmployeeService.GetEmployee(id)
                               ?? throw new CliException($"No existe el empleado #{id}.");
                var active = ctx.Args.Has("activate")
                    ? true
                    : ctx.Args.Has("deactivate")
                        ? false
                        : employee.Status != "activo";
                EmployeeService.ToggleEmployee(id, active);
                Output.Emit(new { id, active }, () =>
                    Console.WriteLine(active ? $"Empleado #{id} activado." : $"Empleado #{id} desactivado."));
                break;
            }
            default:
                throw new CliException("Uso: employees list|show|summary|save|toggle");
        }
    }

    private static void Payroll(CliContext ctx, string action)
    {
        switch (action)
        {
            case "list":
            {
                ctx.RequirePermission(Permissions.PayrollView);
                var rows = PayrollService.GetPeriods();
                Output.Emit(rows, () => Output.Table(rows,
                    ("ID", period => period.Id.ToString()),
                    ("Periodo", period => period.Label),
                    ("Rango", period => period.RangeLabel),
                    ("Estado", period => period.StatusLabel),
                    ("Empleados", period => period.EmployeeCount.ToString()),
                    ("Neto", period => Output.Money(period.TotalNetCents))));
                break;
            }
            case "show":
            {
                ctx.RequirePermission(Permissions.PayrollView);
                var id = ctx.Args.RequireLong("id", "ID del periodo.");
                var period = PayrollService.GetPeriod(id)
                             ?? throw new CliException($"No existe la nómina #{id}.");
                var items = PayrollService.GetItems(id);
                var data = new { period, items };
                Output.Emit(data, () =>
                {
                    Output.KeyValues(new (string, object?)[]
                    {
                        ("ID", period.Id),
                        ("Periodo", period.Label),
                        ("Rango", period.RangeLabel),
                        ("Estado", period.StatusLabel),
                        ("Empleados", period.EmployeeCount),
                        ("Neto", Output.Money(period.TotalNetCents)),
                        ("Deducciones", Output.Money(period.TotalDeductionsCents)),
                    });
                    Console.WriteLine();
                    Output.Table(items,
                        ("Empleado", item => item.EmployeeName ?? ""),
                        ("Días", item => Output.Qty(item.Days)),
                        ("Base", item => Output.Money(item.BaseCents)),
                        ("Extra", item => Output.Money(item.ExtraPerceptionsCents)),
                        ("Deducciones", item => Output.Money(item.DeductionsCents)),
                        ("Neto", item => Output.Money(item.NetCents)));
                });
                break;
            }
            case "summary":
            {
                ctx.RequirePermission(Permissions.PayrollView);
                var summary = PayrollService.GetSummary();
                Output.Emit(summary, () => Output.KeyValues(new (string, object?)[]
                {
                    ("Borradores", summary.DraftCount),
                    ("Pagadas", summary.PaidCount),
                    ("Neto borradores", Output.Money(summary.DraftNetCents)),
                    ("Pagado este mes", Output.Money(summary.PaidThisMonthCents)),
                    ("Empleados", summary.EmployeeCount),
                }));
                break;
            }
            case "create":
            {
                ctx.RequirePermission(Permissions.PayrollWrite);
                var (year, month) = Dates.CurrentMonth();
                year = ctx.Args.OptionalInt("year", year);
                month = ctx.Args.OptionalInt("month", month);
                var type = ctx.Args.Text("type") ?? "mensual";
                var quincena = ctx.Args.OptionalInt("quincena", 0);
                var id = PayrollService.CreatePeriod(year, month, type, quincena);
                var period = PayrollService.GetPeriod(id)
                             ?? throw new CliException("La nómina se creó pero no se pudo recargar.");
                Output.Emit(period, () => Console.WriteLine($"Nómina creada: #{period.Id} {period.Label}"));
                break;
            }
            case "recalc":
            {
                ctx.RequirePermission(Permissions.PayrollWrite);
                var id = ctx.Args.RequireLong("id", "ID del periodo.");
                PayrollService.Recalculate(id);
                var period = PayrollService.GetPeriod(id)
                             ?? throw new CliException("La nómina se recalculó pero no se pudo recargar.");
                Output.Emit(period, () => Console.WriteLine($"Nómina recalculada: #{period.Id} {period.Label}"));
                break;
            }
            case "update-item":
            {
                ctx.RequirePermission(Permissions.PayrollWrite);
                var itemId = ctx.Args.RequireLong("item-id", "ID del concepto.");
                var periodId = ctx.Args.RequireLong("period-id", "ID del periodo.");
                var days = ctx.Args.OptionalDouble("days") ?? 0;
                var baseCents = ctx.Args.OptionalLong("base-cents") ?? 0;
                var extraCents = ctx.Args.OptionalLong("extra-cents") ?? 0;
                var isrCents = ctx.Args.OptionalLong("isr-cents") ?? 0;
                var imssCents = ctx.Args.OptionalLong("imss-cents") ?? 0;
                var infonavitCents = ctx.Args.OptionalLong("infonavit-cents") ?? 0;
                var otherCents = ctx.Args.OptionalLong("other-cents") ?? 0;
                PayrollService.UpdateItem(itemId, periodId, days, baseCents, extraCents,
                    isrCents, imssCents, infonavitCents, otherCents, ctx.Args.Text("notes") ?? "");
                var items = PayrollService.GetItems(periodId);
                var item = items.FirstOrDefault(candidate => candidate.Id == itemId)
                           ?? throw new CliException("El concepto se actualizó pero no se pudo recargar.");
                Output.Emit(item, () => Console.WriteLine($"Concepto #{item.Id} actualizado. Neto: {Output.Money(item.NetCents)}"));
                break;
            }
            case "delete-item":
            {
                ctx.RequirePermission(Permissions.PayrollWrite);
                var itemId = ctx.Args.RequireLong("item-id", "ID del concepto.");
                var periodId = ctx.Args.RequireLong("period-id", "ID del periodo.");
                PayrollService.DeleteItem(itemId, periodId);
                Output.Emit(new { deleted = true, itemId, periodId }, () =>
                    Console.WriteLine($"Concepto #{itemId} eliminado."));
                break;
            }
            case "pay":
            {
                ctx.RequirePermission(Permissions.PayrollWrite);
                var id = ctx.Args.RequireLong("id", "ID del periodo.");
                var accountId = ctx.Args.RequireLong("account", "ID de la cuenta de pago.");
                var date = ctx.Args.Text("date") ?? Dates.TodayIso();
                PayrollService.Pay(id, accountId, date);
                var period = PayrollService.GetPeriod(id)
                             ?? throw new CliException("La nómina se pagó pero no se pudo recargar.");
                Output.Emit(period, () => Console.WriteLine($"Nómina pagada: #{period.Id} {period.Label}"));
                break;
            }
            case "revert":
            {
                ctx.RequirePermission(Permissions.PayrollWrite);
                var id = ctx.Args.RequireLong("id", "ID del periodo.");
                PayrollService.RevertPayment(id);
                var period = PayrollService.GetPeriod(id)
                             ?? throw new CliException("La nómina se revirtió pero no se pudo recargar.");
                Output.Emit(period, () => Console.WriteLine($"Pago revertido: #{period.Id} {period.Label}"));
                break;
            }
            case "delete":
            {
                ctx.RequirePermission(Permissions.PayrollWrite);
                var id = ctx.Args.RequireLong("id", "ID del periodo.");
                PayrollService.DeletePeriod(id);
                Output.Emit(new { deleted = true, id }, () => Console.WriteLine($"Nómina #{id} eliminada."));
                break;
            }
            default:
                throw new CliException("Uso: payroll list|show|summary|create|recalc|update-item|delete-item|pay|revert|delete");
        }
    }

    private static void Taxes(CliContext ctx, string action)
    {
        ctx.RequirePermission(Permissions.TaxView);
        switch (action)
        {
            case "summary":
            {
                var (from, to) = Range(ctx.Args);
                var summary = TaxService.GetSummary(from, to);
                Output.Emit(summary, () =>
                {
                    Output.KeyValues(new (string, object?)[]
                    {
                        ("Periodo", $"{summary.From} a {summary.To}"),
                        ("Facturas", summary.InvoiceCount),
                        ("Ingresos facturados", Output.Money(summary.InvoiceSubtotalCents)),
                        ("IVA facturado", Output.Money(summary.InvoiceIvaCents)),
                        ("Otros ingresos", Output.Money(summary.OtherIncomeSubtotalCents)),
                        ("IVA otros ingresos", Output.Money(summary.OtherIncomeIvaCents)),
                        ("Gastos", Output.Money(summary.ExpenseSubtotalCents)),
                        ("IVA acreditable", Output.Money(summary.ExpenseIvaCents)),
                        ("IVA trasladado", Output.Money(summary.IvaTrasladadoCents)),
                        ("IVA neto", Output.Money(summary.IvaNetoCents)),
                    });
                    Console.WriteLine();
                    Console.WriteLine("Ingresos:");
                    Output.Table(summary.IncomeRows,
                        ("Fecha", row => row.Date),
                        ("Tipo", row => row.Kind),
                        ("Folio", row => row.Folio),
                        ("Nombre", row => row.Name),
                        ("Subtotal", row => Output.Money(row.SubtotalCents)),
                        ("IVA", row => Output.Money(row.IvaCents)),
                        ("Total", row => Output.Money(row.TotalCents)));
                });
                break;
            }
            case "annual":
            {
                var current = Dates.CurrentMonth().Year;
                var year = ctx.Args.OptionalInt("year", current);
                var rows = TaxService.GetAnnual(year);
                Output.Emit(rows, () => Output.Table(rows,
                    ("Mes", row => row.Label),
                    ("Trasladado", row => Output.Money(row.TrasladadoCents)),
                    ("Acreditable", row => Output.Money(row.AcreditableCents)),
                    ("Neto", row => Output.Money(row.NetoCents))));
                break;
            }
            default:
                throw new CliException("Uso: taxes summary|annual [--year N]");
        }
    }

    private static void Reports(CliContext ctx, string action)
    {
        ctx.RequirePermission(Permissions.ReportsView);
        switch (action)
        {
            case "income":
            {
                var (from, to) = Range(ctx.Args);
                var report = ReportService.GetIncomeStatement(from, to);
                Output.Emit(report, () =>
                {
                    Output.KeyValues(new (string, object?)[]
                    {
                        ("Periodo", $"{report.From} a {report.To}"),
                        ("Ingresos facturados", Output.Money(report.InvoicedRevenueCents)),
                        ("Otros ingresos", Output.Money(report.OtherIncomeCents)),
                        ("Ingresos totales", Output.Money(report.TotalIncomeCents)),
                        ("Gastos", Output.Money(report.TotalExpensesCents)),
                        ("Utilidad operativa", Output.Money(report.OperatingProfitCents)),
                        ("Costo de ventas ref.", Output.Money(report.CogsReferenceCents)),
                        ("Margen bruto ref.", Output.Money(report.GrossMarginReferenceCents)),
                    });
                    Console.WriteLine();
                    Output.Table(report.ExpensesByCategory,
                        ("Categoría", row => row.Name),
                        ("Importe", row => Output.Money(row.AmountCents)));
                });
                break;
            }
            case "cashflow":
            {
                var (from, to) = Range(ctx.Args);
                var report = ReportService.GetCashflow(from, to);
                Output.Emit(report, () =>
                {
                    Output.KeyValues(new (string, object?)[]
                    {
                        ("Periodo", $"{report.From} a {report.To}"),
                        ("Cobros de facturas", Output.Money(report.InvoicePaymentsCents)),
                        ("Otros ingresos", Output.Money(report.OtherIncomeCents)),
                        ("Gastos", Output.Money(report.ExpensesCents)),
                        ("Flujo neto", Output.Money(report.NetCents)),
                    });
                    Console.WriteLine();
                    Output.Table(report.Monthly,
                        ("Mes", row => row.Label),
                        ("Entradas", row => Output.Money(row.InflowCents)),
                        ("Salidas", row => Output.Money(row.OutflowCents)),
                        ("Neto", row => Output.Money(row.NetCents)));
                });
                break;
            }
            case "aging":
            {
                var report = ReportService.GetReceivableAging();
                Output.Emit(report, () =>
                {
                    Output.Table(report.Buckets,
                        ("Antigüedad", row => row.Label),
                        ("Importe", row => Output.Money(row.AmountCents)),
                        ("Facturas", row => row.Count.ToString()));
                    Console.WriteLine();
                    Output.Table(report.Invoices,
                        ("Folio", row => row.Folio),
                        ("Cliente", row => row.ContactName),
                        ("Vence", row => row.DueDate),
                        ("Saldo", row => Output.Money(row.BalanceCents)),
                        ("Días", row => row.OverdueDays.ToString()),
                        ("Bucket", row => row.Bucket));
                });
                break;
            }
            case "monthly":
            {
                var (from, to) = Range(ctx.Args, lastTwelveMonths: true);
                var rows = ReportService.GetMonthlyPerformance(from, to);
                Output.Emit(rows, () => Output.Table(rows,
                    ("Mes", row => row.Label),
                    ("Facturado", row => Output.Money(row.InvoicedCents)),
                    ("Otros ingresos", row => Output.Money(row.OtherIncomeCents)),
                    ("Gastos", row => Output.Money(row.ExpenseCents)),
                    ("Utilidad", row => Output.Money(row.ProfitCents))));
                break;
            }
            case "balances":
            {
                var rows = ReportService.GetAccountBalances();
                Output.Emit(rows, () => Output.Table(rows,
                    ("Cuenta", row => row.Name),
                    ("Tipo", row => row.Type),
                    ("Saldo", row => Output.Money(row.BalanceCents))));
                break;
            }
            default:
                throw new CliException("Uso: reports income|cashflow|aging|monthly|balances");
        }
    }

    private static void Users(CliContext ctx, string action)
    {
        switch (action)
        {
            case "list":
            {
                ctx.RequirePermission(Permissions.UsersManage);
                var rows = UserView.From(UserService.GetUsers());
                Output.Emit(rows, () => Output.Table(rows,
                    ("ID", user => user.Id.ToString()),
                    ("Nombre", user => user.Name),
                    ("Correo", user => user.Email),
                    ("Rol", user => user.RoleLabel),
                    ("Activo", user => user.Active ? "Sí" : "No"),
                    ("Último acceso", user => user.LastLoginAt ?? "")));
                break;
            }
            case "summary":
            {
                ctx.RequirePermission(Permissions.UsersManage);
                var summary = UserService.GetSummary();
                Output.Emit(summary, () => Output.KeyValues(new (string, object?)[]
                {
                    ("Total", summary.Total),
                    ("Activos", summary.Active),
                    ("Administradores", summary.Admins),
                }));
                break;
            }
            case "save":
            {
                ctx.RequirePermission(Permissions.UsersManage);
                var id = ctx.Args.OptionalLong("id") ?? 0;
                var name = ctx.Args.RequireText("name", "Nombre del usuario.");
                var email = ctx.Args.RequireText("email", "Correo del usuario.");
                var role = ctx.Args.RequireText("role", "admin, contador, capturista o consulta.");
                var active = !ctx.Args.Has("inactive");
                var password = ctx.Args.Text("new-password");
                if (id == 0 && string.IsNullOrWhiteSpace(password))
                    throw new CliException("Falta --new-password para el usuario nuevo.");
                var userId = UserService.SaveUser(id, name, email, role, active, password);
                var user = UserService.GetUsers().FirstOrDefault(candidate => candidate.Id == userId)
                           ?? throw new CliException("El usuario se guardó pero no se pudo recargar.");
                Output.Emit(UserView.From(user), () => Console.WriteLine($"Usuario guardado: #{user.Id} {user.Name}"));
                break;
            }
            case "activate":
            case "deactivate":
            {
                ctx.RequirePermission(Permissions.UsersManage);
                var id = ctx.Args.RequireLong("id", "ID del usuario.");
                var active = action == "activate";
                UserService.SetActive(id, active);
                Output.Emit(new { id, active }, () =>
                    Console.WriteLine(active ? $"Usuario #{id} activado." : $"Usuario #{id} desactivado."));
                break;
            }
            case "reset-password":
            {
                ctx.RequirePermission(Permissions.UsersManage);
                var id = ctx.Args.RequireLong("id", "ID del usuario.");
                var password = ctx.Args.Text("new-password");
                if (string.IsNullOrWhiteSpace(password))
                    throw new CliException("Falta --new-password con la contraseña nueva.");
                UserService.ResetPassword(id, password);
                Output.Emit(new { id, changed = true }, () => Console.WriteLine($"Contraseña del usuario #{id} actualizada."));
                break;
            }
            case "roles":
            {
                ctx.RequireUser();
                var rows = Permissions.Roles.Select(role => new
                {
                    key = role.Key,
                    label = role.Label,
                    description = Permissions.RoleDescription(role.Key),
                }).ToList();
                Output.Emit(rows, () => Output.Table(rows,
                    ("Clave", role => role.key),
                    ("Rol", role => role.label),
                    ("Descripción", role => role.description)));
                break;
            }
            default:
                throw new CliException("Uso: users list|summary|save|activate|deactivate|reset-password|roles");
        }
    }

    private static void Audit(CliContext ctx, string action)
    {
        if (action != "list")
            throw new CliException("Uso: audit list [--query] [--action] [--entity] [--from] [--to] [--limit]");
        ctx.RequirePermission(Permissions.AuditView);
        var (limit, _) = Paging(ctx.Args, 200);
        var rows = AuditService.GetEntries(new AuditFilters
        {
            Query = ctx.Args.Text("query") ?? "",
            Action = ctx.Args.Text("action") ?? "",
            Entity = ctx.Args.Text("entity") ?? "",
            From = ctx.Args.Text("from") ?? "",
            To = ctx.Args.Text("to") ?? "",
        }, limit);
        Output.Emit(rows, () => Output.Table(rows,
            ("Fecha", entry => entry.CreatedAt),
            ("Acción", entry => entry.Action),
            ("Entidad", entry => entry.Entity),
            ("Detalle", entry => Output.Truncate(entry.Detail)),
            ("Usuario", entry => entry.UserName ?? "")));
    }

    private static void Backup(CliContext ctx, string action)
    {
        ctx.RequirePermission(Permissions.BackupManage);
        switch (action)
        {
            case "create":
            {
                var requested = ctx.Args.Text("output");
                var path = string.IsNullOrWhiteSpace(requested)
                    ? null
                    : Path.GetFullPath(requested);
                var created = AppState.Db.BackupTo(path);
                var info = new FileInfo(created);
                var hash = Sha256(created);
                var data = new
                {
                    path = created,
                    sizeBytes = info.Length,
                    sha256 = hash,
                    createdAt = info.CreationTimeUtc.ToString("O"),
                };
                Output.Emit(data, () =>
                {
                    Console.WriteLine($"Respaldo creado: {created}");
                    Console.WriteLine($"  Tamaño: {info.Length:N0} bytes");
                    Console.WriteLine($"  SHA256: {hash}");
                });
                break;
            }
            case "list":
            {
                var directory = AppPaths.BackupDirectory;
                var rows = Directory.Exists(directory)
                    ? Directory.GetFiles(directory, "*.libro")
                        .Select(path => new FileInfo(path))
                        .OrderByDescending(info => info.LastWriteTimeUtc)
                        .Select(info => new BackupRow(
                            info.Name,
                            info.FullName,
                            info.Length,
                            info.LastWriteTimeUtc.ToString("O"),
                            ctx.Args.Has("hash") ? Sha256(info.FullName) : null))
                        .ToList()
                    : new List<BackupRow>();
                Output.Emit(rows, () => Output.Table(rows,
                    ("Archivo", row => row.Name),
                    ("Tamaño", row => row.SizeBytes.ToString("N0")),
                    ("Modificado", row => row.ModifiedAt),
                    ("SHA256", row => row.Sha256 ?? "")));
                break;
            }
            case "restore":
            {
                var file = ctx.Args.RequireText("file", "Ruta del respaldo .libro.");
                if (!ctx.Args.Has("yes"))
                    throw new CliException("La restauración reemplaza la base actual. Repite con --yes.");
                var path = Path.GetFullPath(file);
                var safety = AppState.Db.RestoreFrom(path);
                AppState.RefreshCompany();
                var data = new { restoredFrom = path, safetyBackup = safety };
                Output.Emit(data, () =>
                {
                    Console.WriteLine($"Base restaurada desde {path}");
                    Console.WriteLine($"Respaldo de seguridad: {safety}");
                });
                break;
            }
            default:
                throw new CliException("Uso: backup create|list|restore");
        }
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static (int Limit, int Offset) Paging(ParsedArgs args, int defaultLimit = 100)
    {
        var limit = Math.Clamp(args.OptionalInt("limit", defaultLimit), 1, 1000);
        var offset = Math.Max(0, args.OptionalInt("offset", 0));
        return (limit, offset);
    }

    private static (string From, string To) Range(ParsedArgs args, bool lastTwelveMonths = false)
    {
        var from = args.Text("from");
        var to = args.Text("to");
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
        {
            if (lastTwelveMonths)
            {
                var months = Dates.LastMonths(12);
                return (months[0].Start, months[^1].End);
            }
            return Dates.CurrentMonthRange();
        }

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            throw new CliException("Debes indicar --from y --to juntos (yyyy-MM-dd).");
        if (!Dates.IsValidIsoDate(from) || !Dates.IsValidIsoDate(to))
            throw new CliException("--from y --to deben tener formato yyyy-MM-dd.");
        if (string.CompareOrdinal(from, to) > 0)
            throw new CliException("--from no puede ser posterior a --to.");
        return (from, to);
    }

    private static string YesNo(bool value) => value ? "sí" : "no";

    private static void Schema(CliContext ctx, string[] requested)
    {
        var examples = BuildSchemas();
        if (requested.Length == 0)
        {
            Output.Emit(examples, () => Console.WriteLine(JsonSerializer.Serialize(examples, Output.JsonOptions)));
            return;
        }

        var key = string.Join(".", requested);
        if (!examples.TryGetValue(key, out var value))
        {
            throw new CliException(
                $"No hay esquema para `{key}`. Disponibles: {string.Join(", ", examples.Keys.OrderBy(item => item))}.");
        }
        Output.Emit(value, () => Console.WriteLine(JsonSerializer.Serialize(value, Output.JsonOptions)));
    }

    private static Dictionary<string, object> BuildSchemas() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["company.update"] = new
        {
            name = "Mi negocio",
            rfc = "XAXX010101000",
            regimenFiscal = "601",
            address = "Calle 1, Ciudad de México",
            phone = "5555555555",
            email = "contacto@ejemplo.mx",
            ivaRate = 0.16,
            invoiceSeries = "A",
            nextInvoiceNumber = 1,
            imssRate = 0.025,
            isrBrackets = "[]",
        },
        ["account.save"] = new
        {
            id = 0,
            name = "Caja chica",
            type = "efectivo",
            openingBalanceCents = 0,
            active = 1,
        },
        ["category.save"] = new
        {
            id = 0,
            name = "Fletes",
            kind = "gasto",
            archived = 0,
        },
        ["contact.save"] = new
        {
            id = 0,
            type = "cliente",
            name = "Cliente de ejemplo",
            rfc = "XAXX010101000",
            email = "cliente@ejemplo.mx",
            phone = "5555555555",
            address = "Calle 1, Ciudad de México",
            notes = "",
        },
        ["product.save"] = new
        {
            id = 0,
            name = "Producto de ejemplo",
            sku = "SKU-001",
            description = "",
            unit = "pieza",
            priceCents = 10000,
            costCents = 6000,
            ivaRate = 0.16,
            minStock = 1,
            trackStock = true,
            initialStock = 10,
        },
        ["transaction.save"] = new
        {
            id = 0,
            date = Dates.TodayIso(),
            type = "gasto",
            categoryId = 4,
            contactId = (long?)null,
            accountId = 1,
            description = "Compra de material",
            subtotalCents = 10000,
            ivaRate = 0.16,
            method = "Efectivo",
            reference = "",
        },
        ["invoice.save"] = new
        {
            id = 0,
            contactId = 1,
            date = Dates.TodayIso(),
            dueDate = Dates.AddDays(Dates.TodayIso(), 30),
            notes = "",
            paymentTerms = "Contado",
            lines = new[]
            {
                new
                {
                    productId = (long?)null,
                    description = "Servicio de ejemplo",
                    qty = 1,
                    unitPriceCents = 100000,
                    ivaRate = 0.16,
                },
            },
        },
        ["inventory.movement"] = new
        {
            productId = 1,
            date = Dates.TodayIso(),
            type = "entrada",
            qty = 1,
            unitCostCents = 6000,
            ivaRate = 0.16,
            reason = "Compra",
            reference = "",
            accountId = (long?)1,
        },
        ["employee.save"] = new
        {
            id = 0,
            name = "Empleado de ejemplo",
            rfc = "XAXX010101000",
            curp = "",
            nss = "",
            position = "Auxiliar",
            department = "Administración",
            salaryCents = 800000,
            startDate = Dates.TodayIso(),
            status = "activo",
            bankName = "",
            bankAccount = "",
        },
        ["payroll.item.update"] = new
        {
            itemId = 1,
            periodId = 1,
            days = 15,
            baseCents = 400000,
            extraCents = 0,
            isrCents = 0,
            imssCents = 0,
            infonavitCents = 0,
            otherCents = 0,
            notes = "",
        },
        ["payment.create"] = new
        {
            id = 1,
            date = Dates.TodayIso(),
            amountCents = 50000,
            account = 1,
            method = "Transferencia",
            reference = "",
            notes = "",
        },
        ["user.save"] = new
        {
            id = 0,
            name = "Usuario de ejemplo",
            email = "usuario@ejemplo.mx",
            role = "capturista",
            active = true,
            newPassword = "Cambiar123!",
        },
    };

    private sealed record BackupRow(
        string Name,
        string Path,
        long SizeBytes,
        string ModifiedAt,
        string? Sha256);
}
