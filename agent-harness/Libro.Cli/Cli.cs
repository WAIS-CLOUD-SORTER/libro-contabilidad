using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Libro.Desktop.Data;
using Libro.Desktop.Services;

namespace Libro.Cli;

internal sealed class CliException : Exception
{
    public int ExitCode { get; }

    public CliException(string message, int exitCode = 1) : base(message)
    {
        ExitCode = exitCode;
    }
}

internal sealed class ParsedArgs
{
    private static readonly HashSet<string> BooleanOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "json", "debug", "help", "include-inactive", "include-archived", "low",
        "restore", "activate", "deactivate", "inactive", "emit", "yes", "stdin", "quiet",
        "no-color", "pretty", "all", "hash",
    };

    private static readonly HashSet<string> GlobalOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "data-dir", "email", "password", "password-env", "json", "debug", "help",
        "no-color", "pretty",
    };

    public List<string> Positionals { get; } = new();
    public Dictionary<string, string?> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string?> GlobalOptionValues { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> GlobalFlags { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static ParsedArgs Parse(IEnumerable<string> tokens)
    {
        var parsed = new ParsedArgs();
        var list = tokens.ToList();
        var literal = false;
        var seenCommand = false;

        for (var index = 0; index < list.Count; index++)
        {
            var token = list[index];
            if (literal)
            {
                parsed.Positionals.Add(token);
                continue;
            }

            if (token == "--")
            {
                literal = true;
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length == 2)
            {
                parsed.Positionals.Add(token);
                seenCommand = true;
                continue;
            }

            var raw = token[2..];
            string key;
            string? value = null;
            var equals = raw.IndexOf('=');
            if (equals >= 0)
            {
                key = raw[..equals];
                value = raw[(equals + 1)..];
            }
            else
            {
                key = raw;
            }

            // Antes del primer comando, --email/--password son credenciales globales.
            // Después del comando pertenecen a la operación (p. ej. users save --email).
            var isGlobal = !seenCommand && GlobalOptions.Contains(key);
            var optionTarget = isGlobal ? parsed.GlobalOptionValues : parsed.Options;
            var flagTarget = isGlobal ? parsed.GlobalFlags : parsed.Flags;

            if (value is not null)
            {
                optionTarget[key] = value;
                continue;
            }

            if (BooleanOptions.Contains(key))
            {
                flagTarget.Add(key);
                continue;
            }

            if (index + 1 < list.Count && !list[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                optionTarget[key] = list[++index];
            }
            else
            {
                flagTarget.Add(key);
            }
        }

        return parsed;
    }

    public bool Has(string name) =>
        Options.ContainsKey(name) || Flags.Contains(name) ||
        GlobalOptionValues.ContainsKey(name) || GlobalFlags.Contains(name);

    public string? Text(string name) =>
        Options.TryGetValue(name, out var value) ? value : null;

    public string? GlobalText(string name) =>
        GlobalOptionValues.TryGetValue(name, out var value) ? value : null;

    public string? FirstText(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Text(name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            value = GlobalText(name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }

    public string RequireText(string name, string hint)
    {
        var value = Text(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new CliException($"Falta --{name}. {hint}");
        return value.Trim();
    }

    public long RequireLong(string name, string hint)
    {
        var value = RequireText(name, hint);
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new CliException($"--{name} debe ser un número entero.");
        return parsed;
    }

    public long? OptionalLong(string name)
    {
        var value = Text(name);
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new CliException($"--{name} debe ser un número entero.");
        return parsed;
    }

    public int OptionalInt(string name, int fallback)
    {
        var value = Text(name);
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new CliException($"--{name} debe ser un número entero.");
        return parsed;
    }

    public double? OptionalDouble(string name)
    {
        var value = Text(name);
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new CliException($"--{name} debe ser un número.");
        return parsed;
    }
}

internal static class Output
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static readonly JsonSerializerOptions InputOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static bool Json { get; set; }

    public static void Emit(object? value, Action? human = null)
    {
        if (Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
            return;
        }

        if (human is not null)
        {
            human();
            return;
        }

        Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
    }

    public static void Error(string message, bool debug = false, Exception? exception = null)
    {
        if (Json)
        {
            var payload = new
            {
                ok = false,
                error = message,
                detail = debug ? exception?.ToString() : null,
            };
            Console.Error.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return;
        }

        Console.Error.WriteLine("ERROR: " + message);
        if (debug && exception is not null)
            Console.Error.WriteLine(exception);
    }

    public static void Info(string message)
    {
        if (!Json) Console.WriteLine(message);
    }

    public static void Table<T>(IEnumerable<T> source, params (string Header, Func<T, string> Value)[] columns)
    {
        var rows = source.ToList();
        if (rows.Count == 0)
        {
            Console.WriteLine("(sin resultados)");
            return;
        }

        var widths = columns.Select(column => Math.Max(
            column.Header.Length,
            rows.Max(row => Display(column.Value(row)).Length))).ToArray();

        Console.WriteLine(string.Join("  ", columns.Select((column, index) =>
            column.Header.PadRight(widths[index]))));
        Console.WriteLine(string.Join("  ", widths.Select(width => new string('-', width))));
        foreach (var row in rows)
        {
            Console.WriteLine(string.Join("  ", columns.Select((column, index) =>
                Display(column.Value(row)).PadRight(widths[index]))));
        }
    }

    public static void KeyValues(IEnumerable<(string Key, object? Value)> values)
    {
        var list = values.ToList();
        var width = list.Count == 0 ? 0 : list.Max(item => item.Key.Length);
        foreach (var (key, value) in list)
            Console.WriteLine($"{key.PadRight(width)}  {Display(value)}");
    }

    public static string Display(object? value) => value switch
    {
        null => "",
        bool boolean => boolean ? "sí" : "no",
        _ => Convert.ToString(value, Mx) ?? "",
    };

    public static string Money(long cents) => (cents / 100m).ToString("C2", Mx);

    public static string Number(long value) => value.ToString("N0", Mx);

    public static string Qty(double value) => value.ToString("#,0.###", Mx);

    public static string Date(string? iso) => string.IsNullOrWhiteSpace(iso) ? "" : iso;

    public static string Truncate(string? value, int max = 44)
    {
        value ??= "";
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }
}

internal sealed class CliContext
{
    public ParsedArgs Args { get; set; }
    public bool Interactive { get; set; }
    public bool Initialized { get; private set; }
    public User? LastLogin { get; private set; }
    public string? DataDirectory { get; }

    public void RememberLogin(User user) => LastLogin = user;

    public CliContext(ParsedArgs args)
    {
        Args = args;
        var requested = args.FirstText("data-dir");
        if (!string.IsNullOrWhiteSpace(requested))
        {
            try
            {
                DataDirectory = Path.GetFullPath(requested);
                Environment.SetEnvironmentVariable("LIBRO_DATA_DIR", DataDirectory);
            }
            catch (Exception ex)
            {
                throw new CliException("La ruta de --data-dir no es válida: " + ex.Message);
            }
        }
    }

    public bool Json => Args.Has("json");
    public bool Debug => Args.Has("debug");

    public string? Email => Args.FirstText("email") ?? Environment.GetEnvironmentVariable("LIBRO_EMAIL");

    public string? LoginEmail =>
        Args.GlobalText("email") ?? Environment.GetEnvironmentVariable("LIBRO_EMAIL");

    public string? Password
    {
        get
        {
            var envName = Args.FirstText("password-env");
            if (!string.IsNullOrWhiteSpace(envName))
            {
                var value = Environment.GetEnvironmentVariable(envName);
                if (string.IsNullOrEmpty(value))
                    throw new CliException($"La variable de entorno {envName} está vacía o no existe.");
                return value;
            }

            var direct = Args.FirstText("password");
            if (!string.IsNullOrEmpty(direct)) return direct;
            return Environment.GetEnvironmentVariable("LIBRO_PASSWORD");
        }
    }

    public string? LoginPassword
    {
        get
        {
            var envName = Args.GlobalText("password-env") ?? Args.Text("password-env");
            if (!string.IsNullOrWhiteSpace(envName))
            {
                var value = Environment.GetEnvironmentVariable(envName);
                if (string.IsNullOrEmpty(value))
                    throw new CliException($"La variable de entorno {envName} está vacía o no existe.");
                return value;
            }

            var direct = Args.GlobalText("password");
            if (!string.IsNullOrEmpty(direct)) return direct;
            return Environment.GetEnvironmentVariable("LIBRO_PASSWORD");
        }
    }

    public void Initialize()
    {
        if (Initialized) return;
        if (!string.IsNullOrWhiteSpace(DataDirectory))
            Environment.SetEnvironmentVariable("LIBRO_DATA_DIR", DataDirectory);
        AppState.Initialize();
        Initialized = true;
    }

    public bool TryAutoLogin(bool required)
    {
        Initialize();
        if (AppState.CurrentUser is not null) return true;

        var email = LoginEmail;
        var password = LoginPassword;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            if (!required) return false;
            throw new CliException(
                "Faltan credenciales. Usa --email y --password, o define LIBRO_EMAIL y LIBRO_PASSWORD. " +
                "También puedes ejecutar `auth login` dentro del modo REPL.");
        }

        var result = AuthService.Login(AppState.Db, email, password);
        if (result.User is null)
            throw new CliException(result.Error ?? "No se pudo iniciar sesión.");

        AppState.SetCurrentUser(result.User);
        RememberLogin(result.User);
        return true;
    }

    public User RequireUser()
    {
        TryAutoLogin(required: true);
        return AppState.CurrentUser ?? throw new CliException("No hay una sesión activa.");
    }

    public void RequirePermission(string permission)
    {
        RequireUser();
        if (!AppState.Can(permission))
        {
            var role = AppState.CurrentUser?.Role ?? "sin sesión";
            throw new CliException(
                $"Permiso insuficiente: se requiere `{permission}`. Tu rol actual es `{role}`.");
        }
    }

    public void RequireAnyPermission(params string[] permissions)
    {
        RequireUser();
        if (permissions.Any(AppState.Can)) return;
        var role = AppState.CurrentUser?.Role ?? "sin sesión";
        throw new CliException(
            $"Permiso insuficiente: se requiere alguno de {string.Join(", ", permissions.Select(p => $"`{p}`"))}. " +
            $"Tu rol actual es `{role}`.");
    }

    public T ReadInput<T>(string operation)
    {
        string json;
        if (Args.Has("stdin"))
        {
            json = Console.In.ReadToEnd();
        }
        else
        {
            var path = Args.Text("input");
            if (string.IsNullOrWhiteSpace(path))
                throw new CliException(
                    $"Falta --input <archivo.json> o --stdin para {operation}. " +
                    $"Consulta `schema {operation}` para ver la forma esperada.");
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new CliException($"No se pudo leer {path}: {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(json))
            throw new CliException("La entrada JSON está vacía.");

        try
        {
            return JsonSerializer.Deserialize<T>(json, Output.InputOptions)
                   ?? throw new CliException("La entrada JSON no produjo un objeto válido.");
        }
        catch (JsonException ex)
        {
            throw new CliException("JSON inválido: " + ex.Message);
        }
    }

    public static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        var tokenStarted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (quote is not null)
            {
                if (character == quote.Value)
                {
                    quote = null;
                }
                else if (character == '\\' && index + 1 < line.Length &&
                         (line[index + 1] == quote.Value || line[index + 1] == '\\'))
                {
                    // Conserva rutas Windows: solo se escapa la comilla activa o
                    // una barra invertida literal. C:\temp\file.json no se altera.
                    current.Append(line[++index]);
                    tokenStarted = true;
                }
                else
                {
                    current.Append(character);
                    tokenStarted = true;
                }
                continue;
            }

            if (character is '"' or '\'')
            {
                quote = character;
                tokenStarted = true;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (tokenStarted)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    tokenStarted = false;
                }
                continue;
            }

            current.Append(character);
            tokenStarted = true;
        }

        if (tokenStarted) tokens.Add(current.ToString());
        return tokens;
    }
}

internal sealed record UserView(
    long Id,
    string Name,
    string Email,
    string Role,
    string RoleLabel,
    bool Active,
    string CreatedAt,
    string? LastLoginAt)
{
    public static UserView From(User user) => new(
        user.Id,
        user.Name,
        user.Email,
        user.Role,
        Permissions.RoleLabel(user.Role),
        user.Active == 1,
        user.CreatedAt,
        user.LastLoginAt);

    public static List<UserView> From(IEnumerable<User> users) => users.Select(From).ToList();
}
