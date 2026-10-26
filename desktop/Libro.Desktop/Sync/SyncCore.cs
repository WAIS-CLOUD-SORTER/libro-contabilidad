using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Libro.Desktop.Data;

namespace Libro.Desktop.Sync;

/// <summary>Preferencias del servidor local de ventas para el teléfono.</summary>
public sealed class SyncSettings
{
    public bool Enabled { get; set; }
    public int Port { get; set; } = 8787;

    /// <summary>
    /// Evita repetir el aviso de configurar Mercado Pago en cada arranque.
    /// </summary>
    public bool MercadoPagoPrompted { get; set; }
}

/// <summary>Teléfono emparejado. Solo se guarda el hash del token, nunca el token.</summary>
public sealed class PairedDevice
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string LastSeenAt { get; set; } = "";
}

/// <summary>
/// Persistencia del módulo Sync en la carpeta de datos. Se usan archivos JSON y
/// no tablas nuevas a propósito: el esquema no tiene migraciones (H-40) y añadir
/// una tabla rompería las bases ya creadas.
/// </summary>
public static class SyncStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private static string SettingsPath => Path.Combine(AppPaths.DataDirectory, "sync.json");
    private static string DevicesPath => Path.Combine(AppPaths.DataDirectory, "dispositivos.json");

    public static SyncSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new SyncSettings();
            return JsonSerializer.Deserialize<SyncSettings>(File.ReadAllText(SettingsPath)) ?? new SyncSettings();
        }
        catch (Exception)
        {
            // Una configuración ilegible no debe impedir abrir la aplicación.
            return new SyncSettings();
        }
    }

    public static void SaveSettings(SyncSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Json));
    }

    public static List<PairedDevice> LoadDevices()
    {
        try
        {
            if (!File.Exists(DevicesPath)) return new List<PairedDevice>();
            return JsonSerializer.Deserialize<List<PairedDevice>>(File.ReadAllText(DevicesPath))
                   ?? new List<PairedDevice>();
        }
        catch (Exception)
        {
            return new List<PairedDevice>();
        }
    }

    public static void SaveDevices(List<PairedDevice> devices)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(DevicesPath, JsonSerializer.Serialize(devices, Json));
    }

    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static PairedDevice? FindByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = HashToken(token);
        return LoadDevices().FirstOrDefault(device =>
            string.Equals(device.TokenHash, hash, StringComparison.Ordinal));
    }

    public static void Touch(PairedDevice device)
    {
        var devices = LoadDevices();
        var stored = devices.FirstOrDefault(candidate => candidate.Id == device.Id);
        if (stored is null) return;
        stored.LastSeenAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        SaveDevices(devices);
    }
}

public enum TerminalOutcome
{
    Approved,
    Rejected,
    Pending,
    NotConfigured,
}

public sealed class TerminalCharge
{
    public TerminalOutcome Outcome { get; init; }
    public string Reference { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool Approved => Outcome == TerminalOutcome.Approved;
}

public sealed class TerminalRequest
{
    public long AmountCents { get; init; }
    public string Description { get; init; } = "";
}

/// <summary>
/// Contrato de cobro en terminal. La app depende de esta interfaz para que el
/// modo simulado y la terminal real sean intercambiables y comprobables.
/// </summary>
public interface IPaymentTerminal
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<TerminalCharge> ChargeAsync(TerminalRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Cobro simulado: siempre aprueba. Se usa cuando no hay credenciales y en las
/// pruebas automatizadas, para poder verificar el flujo completo sin terminal.
/// </summary>
public sealed class SimulatedTerminal : IPaymentTerminal
{
    public string Name => "Simulada";
    public bool IsConfigured => true;

    public Task<TerminalCharge> ChargeAsync(TerminalRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new TerminalCharge
        {
            Outcome = TerminalOutcome.Approved,
            Reference = "SIM-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            Detail = "Cobro simulado (sin credenciales de Mercado Pago)",
        });
}

/// <summary>
/// Terminal Mercado Pago Point.
///
/// IMPORTANTE: esta implementación está escrita contra la API de integraciones
/// de Point pero NO se ha podido validar contra el servicio real porque todavía
/// no hay credenciales. La forma del cuerpo y la ruta deben confirmarse con la
/// documentación vigente de Mercado Pago antes de usarla con dinero real.
/// Toda la superficie que hay que ajustar está en BuildIntentRequest y
/// ParseIntentResponse; el resto (autenticación, reintentos y errores) no cambia.
/// </summary>
public sealed class MercadoPagoPointTerminal : IPaymentTerminal
{
    private const string DefaultBaseUrl = "https://api.mercadopago.com";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _accessToken;
    private readonly string _deviceId;
    private readonly string _baseUrl;

    public MercadoPagoPointTerminal(string accessToken, string deviceId, string? baseUrl = null)
    {
        _accessToken = accessToken ?? "";
        _deviceId = deviceId ?? "";
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
    }

    public string Name => "Mercado Pago Point";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_accessToken) && !string.IsNullOrWhiteSpace(_deviceId);

    public async Task<TerminalCharge> ChargeAsync(TerminalRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            return new TerminalCharge
            {
                Outcome = TerminalOutcome.NotConfigured,
                Detail = "Faltan el access_token o el ID de dispositivo de Mercado Pago.",
            };

        if (request.AmountCents <= 0)
            return new TerminalCharge { Outcome = TerminalOutcome.Rejected, Detail = "Importe inválido." };

        try
        {
            using var content = new StringContent(
                BuildIntentRequest(request), Encoding.UTF8, "application/json");

            using var message = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/orders")
            {
                Content = content,
            };
            message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_accessToken}");

            using var response = await Http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new TerminalCharge
                {
                    Outcome = TerminalOutcome.Rejected,
                    Detail = $"Mercado Pago respondió {(int)response.StatusCode}: {Trim(body)}",
                };

            return ParseIntentResponse(body);
        }
        catch (OperationCanceledException)
        {
            return new TerminalCharge { Outcome = TerminalOutcome.Pending, Detail = "El cobro no respondió a tiempo." };
        }
        catch (HttpRequestException ex)
        {
            return new TerminalCharge { Outcome = TerminalOutcome.Rejected, Detail = ex.Message };
        }
    }

    /// <summary>
    /// Cuerpo para Orders API (Point). Importe en pesos con dos decimales según
    /// la convención de Mercado Pago; Libro guarda centavos.
    /// ADVERTENCIA: la forma exacta de este cuerpo NO se ha podido validar
    /// contra el servicio real. Si Mercado Pago responde 400, el mensaje se
    /// devuelve tal cual en `Detail` para corregir este método.
    /// </summary>
    private string BuildIntentRequest(TerminalRequest request)
    {
        var amount = (request.AmountCents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        var payload = new Dictionary<string, object?>
        {
            ["type"] = "point",
            ["external_reference"] = "libro-" + Guid.NewGuid().ToString("N")[..12],
            ["description"] = string.IsNullOrWhiteSpace(request.Description) ? "Venta Libro" : request.Description,
            ["device_id"] = _deviceId,
            ["transactions"] = new Dictionary<string, object?>
            {
                ["payments"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["amount"] = amount,
                        ["payment_method"] = new Dictionary<string, object?>
                        {
                            ["id"] = "point",
                            ["type"] = "on_terminal",
                        },
                    },
                },
            },
        };
        return JsonSerializer.Serialize(payload);
    }

    private static TerminalCharge ParseIntentResponse(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var id = root.TryGetProperty("id", out var idValue) ? idValue.ToString() : "";
            var status = root.TryGetProperty("status", out var statusValue)
                ? statusValue.GetString() ?? ""
                : "";

            var outcome = status.ToLowerInvariant() switch
            {
                // Orders API usa processed/finished; se conservan los valores de la
                // API anterior por si el terminal responde con el modelo viejo.
                "processed" or "approved" or "finished" => TerminalOutcome.Approved,
                "failed" or "rejected" or "canceled" or "cancelled" or "error" or "refunded"
                    => TerminalOutcome.Rejected,
                _ => TerminalOutcome.Pending,
            };

            return new TerminalCharge { Outcome = outcome, Reference = id, Detail = body };
        }
        catch (JsonException)
        {
            return new TerminalCharge { Outcome = TerminalOutcome.Pending, Detail = Trim(body) };
        }
    }

    private static string Trim(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";

    /// <summary>
    /// Elige la terminal disponible: Point si hay credenciales guardadas (o en
    /// el entorno) y, si no, la simulada. Devuelve el motivo para la interfaz.
    /// </summary>
    public static (IPaymentTerminal Terminal, string Reason) Resolve()
    {
        var stored = MpCredentialStore.Load();
        var token = string.IsNullOrWhiteSpace(stored.AccessToken)
            ? Environment.GetEnvironmentVariable("MP_ACCESS_TOKEN") ?? ""
            : stored.AccessToken;
        var device = string.IsNullOrWhiteSpace(stored.DeviceId)
            ? Environment.GetEnvironmentVariable("MP_DEVICE_ID") ?? ""
            : stored.DeviceId;

        var terminal = new MercadoPagoPointTerminal(token, device);
        return terminal.IsConfigured
            ? (terminal, "Mercado Pago Point configurada.")
            : (new SimulatedTerminal(), "Sin credenciales de Mercado Pago: se usa la terminal simulada.");
    }
}

/// <summary>Credenciales de Mercado Pago introducidas por el usuario.</summary>
public sealed class MercadoPagoCredentials
{
    public string AccessToken { get; set; } = "";
    public string DeviceId { get; set; } = "";
}

/// <summary>
/// Guarda el Access Token cifrado con DPAPI del usuario actual, igual que
/// clave.bin. Nunca se escribe en texto plano ni se registra en la bitácora.
/// </summary>
public static class MpCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Libro.MercadoPago.v1");
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("LMP1");

    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "mercadopago.bin");

    public static MercadoPagoCredentials Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new MercadoPagoCredentials();
            var blob = File.ReadAllBytes(FilePath);
            if (blob.Length <= Magic.Length || !blob.AsSpan(0, Magic.Length).SequenceEqual(Magic))
                return new MercadoPagoCredentials();

            var plain = ProtectedData.Unprotect(
                blob.AsSpan(Magic.Length).ToArray(), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<MercadoPagoCredentials>(Encoding.UTF8.GetString(plain))
                   ?? new MercadoPagoCredentials();
        }
        catch (Exception)
        {
            // Ilegibles o de otra cuenta de Windows: se tratan como ausentes.
            return new MercadoPagoCredentials();
        }
    }

    public static void Save(MercadoPagoCredentials credentials)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(credentials));
        var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

        var output = new byte[Magic.Length + encrypted.Length];
        Magic.CopyTo(output, 0);
        encrypted.CopyTo(output, Magic.Length);

        var temporary = FilePath + ".tmp";
        File.WriteAllBytes(temporary, output);
        File.Move(temporary, FilePath, overwrite: true);
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (IOException)
        {
            // No es crítico para la operación.
        }
    }

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(Load().AccessToken);
}

/// <summary>
/// Consulta los terminales Point de la cuenta. Es una llamada de solo lectura,
/// así que permite validar el Access Token sin cobrar nada.
/// </summary>
public static class MpTerminals
{
    private const string BaseUrl = "https://api.mercadopago.com";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public sealed class TerminalInfo
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Mode { get; init; } = "";
    }

    public static async Task<(bool Ok, string Detail, List<TerminalInfo> Terminals)> ListAsync(
        string accessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return (false, "Falta el Access Token.", new List<TerminalInfo>());

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/terminals/v1/list");
            message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");

            using var response = await Http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return (false,
                    $"Mercado Pago respondió {(int)response.StatusCode}: {Truncate(body)}",
                    new List<TerminalInfo>());

            return (true, "Credenciales válidas.", Parse(body));
        }
        catch (OperationCanceledException)
        {
            return (false, "Mercado Pago no respondió a tiempo.", new List<TerminalInfo>());
        }
        catch (HttpRequestException ex)
        {
            return (false, ex.Message, new List<TerminalInfo>());
        }
    }

    private static List<TerminalInfo> Parse(string body)
    {
        var result = new List<TerminalInfo>();
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var array = default(JsonElement);
            var found = false;

            if (root.ValueKind == JsonValueKind.Array)
            {
                array = root;
                found = true;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { "data", "devices", "results" })
                {
                    if (root.TryGetProperty(name, out var candidate) &&
                        candidate.ValueKind == JsonValueKind.Array)
                    {
                        array = candidate;
                        found = true;
                        break;
                    }
                }
            }

            if (!found) return result;

            foreach (var item in array.EnumerateArray())
            {
                result.Add(new TerminalInfo
                {
                    Id = Text(item, "id", "device_id"),
                    Name = Text(item, "name", "pos_id", "serial_number"),
                    Mode = Text(item, "operating_mode", "mode"),
                });
            }
        }
        catch (JsonException)
        {
            // Lista vacía; el detalle del error ya se devolvió al llamador.
        }
        return result;
    }

    private static string Text(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? "";
        }
        return "";
    }

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300] + "…";
}
