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

            using var message = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/point/integrations")
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

    /// <summary>Importe en centavos según la convención de Libro; Point usa pesos.</summary>
    private string BuildIntentRequest(TerminalRequest request)
    {
        var amount = (request.AmountCents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        var payload = new Dictionary<string, object?>
        {
            ["amount"] = decimal.Parse(amount, CultureInfo.InvariantCulture),
            ["description"] = string.IsNullOrWhiteSpace(request.Description) ? "Venta Libro" : request.Description,
            ["device_id"] = _deviceId,
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
                "approved" or "finished" => TerminalOutcome.Approved,
                "rejected" or "canceled" or "error" => TerminalOutcome.Rejected,
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
    /// Elige la terminal disponible: Point si hay credenciales y, si no,
    /// la simulada. Devuelve también el motivo para mostrarlo en la interfaz.
    /// </summary>
    public static (IPaymentTerminal Terminal, string Reason) Resolve()
    {
        var token = Environment.GetEnvironmentVariable("MP_ACCESS_TOKEN") ?? "";
        var device = Environment.GetEnvironmentVariable("MP_DEVICE_ID") ?? "";
        var terminal = new MercadoPagoPointTerminal(token, device);
        return terminal.IsConfigured
            ? (terminal, "Mercado Pago Point configurada.")
            : (new SimulatedTerminal(), "Sin credenciales de Mercado Pago: se usa la terminal simulada.");
    }
}
