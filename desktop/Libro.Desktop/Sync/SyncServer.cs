using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Libro.Desktop.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Libro.Desktop.Sync;

/// <summary>
/// Servidor HTTP local que atiende la app de ventas del teléfono.
///
/// Decisiones de seguridad:
/// - Solo escucha en direcciones privadas (10/8, 172.16/12, 192.168/16) y en
///   loopback; nunca en una interfaz pública.
/// - Apagado por defecto: hay que activarlo en Configuración.
/// - Cada teléfono se empareja con un PIN de 6 dígitos y recibe un token propio;
///   la base solo guarda el hash del token.
/// - Opera con la sesión del usuario de escritorio, así que si Libro cierra
///   sesión el teléfono deja de poder vender.
/// - No hay HTTPS: un certificado autofirmado haría que Android rechazara la
///   página. El canal es la red local de confianza más el token por dispositivo.
/// </summary>
public sealed class SyncServer : IDisposable
{
    private const int MaxPairAttemptsPerMinute = 8;

    private static readonly Dictionary<string, List<DateTime>> PairAttempts = new();

    private readonly List<string> _urls = new();
    private WebApplication? _app;

    public string Pin { get; }
    public int Port { get; }
    public IReadOnlyList<string> Urls => _urls;
    public bool IsRunning => _app is not null;

    public static SyncServer? Current { get; private set; }

    private SyncServer(int port)
    {
        Port = port;

        // LIBRO_SYNC_PIN permite fijar el PIN en pruebas automatizadas y en
        // soporte remoto. Sin la variable, el PIN es aleatorio en cada arranque.
        var forced = Environment.GetEnvironmentVariable("LIBRO_SYNC_PIN");
        Pin = forced is { Length: 6 } && forced.All(char.IsAsciiDigit)
            ? forced
            : Random.Shared.Next(100000, 999999).ToString();
    }

    public static SyncServer Start(int port)
    {
        Stop();
        var server = new SyncServer(port);
        server.StartCore();
        Current = server;
        return server;
    }

    public static void Stop()
    {
        Current?.Dispose();
        Current = null;
    }

    private void StartCore()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
            ContentRootPath = AppContext.BaseDirectory,
        });

        // La app no tiene consola; no queremos el logger escribiendo en stdout.
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.WriteIndented = false;
        });

        _urls.Clear();
        _urls.Add($"http://127.0.0.1:{Port}");
        foreach (var address in LanAddresses()) _urls.Add($"http://{address}:{Port}");
        builder.WebHost.UseUrls(_urls.ToArray());

        var app = builder.Build();

        app.MapGet("/", () => Results.Content(MobileUi.Html, "text/html; charset=utf-8"));
        app.MapGet("/api/health", () => Results.Json(new
        {
            app = "Libro",
            version = typeof(SyncServer).Assembly.GetName().Version?.ToString(3) ?? "2.1.0",
            paired = SyncStore.LoadDevices().Count,
            session = AppState.CurrentUser is not null,
        }));

        app.MapPost("/api/pair", Pair);
        app.MapGet("/api/catalog", (HttpContext ctx) => Guard(ctx, () =>
        {
            var products = InventoryService.GetProducts()
                .Where(product => product.Active == 1)
                .Select(product => new
                {
                    id = product.Id,
                    sku = product.Sku,
                    name = product.Name,
                    priceCents = product.PriceCents,
                    ivaRate = product.IvaRate,
                    stock = product.Stock,
                    trackStock = product.TrackStock == 1,
                });

            var contacts = ContactService.GetContacts(new ContactFilters())
                .Where(contact => contact.Archived == 0 && contact.Type == "cliente")
                .Select(contact => new { id = contact.Id, name = contact.Name, rfc = contact.Rfc });

            var accounts = CompanyService.GetAccounts(AppState.Db)
                .Select(account => new { id = account.Id, name = account.Name, type = account.Type });

            return Results.Json(new
            {
                company = AppState.Company.Name,
                user = AppState.CurrentUser?.Name ?? "",
                products,
                contacts,
                accounts,
            });
        }));

        app.MapGet("/api/sales/today", (HttpContext ctx) => Guard(ctx, () =>
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var page = InvoiceService.GetInvoices(new InvoiceFilters { From = today, To = today }, 50);
            return Results.Json(new
            {
                count = page.Rows.Count,
                totalCents = page.Rows.Sum(invoice => invoice.TotalCents),
                rows = page.Rows.Select(invoice => new
                {
                    id = invoice.Id,
                    folio = $"{invoice.Series}-{invoice.Number}",
                    contact = invoice.ContactName,
                    totalCents = invoice.TotalCents,
                    status = invoice.Status,
                }),
            });
        }));

        app.MapPost("/api/sales", (HttpContext ctx) => Guard(ctx, () => CreateSale(ctx)));

        app.MapPost("/api/sales/{id:long}/charge", (HttpContext ctx, long id) =>
            Guard(ctx, () => ChargeTerminal(id)));

        app.MapGet("/api/terminal", (HttpContext ctx) => Guard(ctx, () =>
        {
            var (terminal, reason) = MercadoPagoPointTerminal.Resolve();
            return Results.Json(new { name = terminal.Name, configured = terminal.IsConfigured, reason });
        }));

        app.Start();
        _app = app;
    }

    // ---------------------------------------------------------------- helpers

    private static IResult Guard(HttpContext context, Func<IResult> action)
    {
        if (AppState.CurrentUser is null)
            return Results.Json(new { error = "La sesión de Libro está cerrada. Inicia sesión en la computadora." },
                statusCode: StatusCodes.Status409Conflict);

        var device = Authenticate(context);
        if (device is null)
            return Results.Json(new { error = "Dispositivo no emparejado." },
                statusCode: StatusCodes.Status401Unauthorized);

        try
        {
            return action();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static PairedDevice? Authenticate(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) ||
            !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = header["Bearer ".Length..].Trim();
        var device = SyncStore.FindByToken(token);
        if (device is not null) SyncStore.Touch(device);
        return device;
    }

    private IResult Pair(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress?.ToString() ?? "desconocido";
        if (IsRateLimited(remote))
            return Results.Json(new { error = "Demasiados intentos. Espera un minuto." },
                statusCode: StatusCodes.Status429TooManyRequests);

        PairRequest? request;
        try
        {
            request = context.Request.ReadFromJsonAsync<PairRequest>().GetAwaiter().GetResult();
        }
        catch (JsonException)
        {
            return Results.Json(new { error = "Cuerpo inválido." }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Pin))
            return Results.Json(new { error = "Falta el PIN." }, statusCode: StatusCodes.Status400BadRequest);

        if (!string.Equals(request.Pin.Trim(), Pin, StringComparison.Ordinal))
        {
            RegisterAttempt(remote);
            return Results.Json(new { error = "PIN incorrecto." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var token = SyncStore.NewToken();
        var devices = SyncStore.LoadDevices();
        var device = new PairedDevice
        {
            Name = string.IsNullOrWhiteSpace(request.DeviceName) ? "Teléfono" : request.DeviceName.Trim(),
            TokenHash = SyncStore.HashToken(token),
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            LastSeenAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
        };
        devices.Add(device);
        SyncStore.SaveDevices(devices);

        return Results.Json(new
        {
            token,
            device = device.Name,
            company = AppState.Company.Name,
            user = AppState.CurrentUser?.Name ?? "",
        });
    }

    private static bool IsRateLimited(string remote)
    {
        lock (PairAttempts)
        {
            if (!PairAttempts.TryGetValue(remote, out var attempts)) return false;
            attempts.RemoveAll(at => (DateTime.UtcNow - at).TotalSeconds > 60);
            return attempts.Count >= MaxPairAttemptsPerMinute;
        }
    }

    private static void RegisterAttempt(string remote)
    {
        lock (PairAttempts)
        {
            if (!PairAttempts.TryGetValue(remote, out var attempts))
            {
                attempts = new List<DateTime>();
                PairAttempts[remote] = attempts;
            }
            attempts.Add(DateTime.UtcNow);
        }
    }

    private static IResult CreateSale(HttpContext context)
    {
        SaleRequest? request;
        try
        {
            request = context.Request.ReadFromJsonAsync<SaleRequest>().GetAwaiter().GetResult();
        }
        catch (JsonException)
        {
            return Results.Json(new { error = "Cuerpo inválido." }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (request is null || request.Lines.Count == 0)
            return Results.Json(new { error = "La venta no tiene conceptos." }, statusCode: StatusCodes.Status400BadRequest);

        if (request.ContactId <= 0)
            return Results.Json(new { error = "Falta el cliente." }, statusCode: StatusCodes.Status400BadRequest);

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var input = new InvoiceInput
        {
            ContactId = request.ContactId,
            Date = string.IsNullOrWhiteSpace(request.Date) ? today : request.Date,
            DueDate = string.IsNullOrWhiteSpace(request.DueDate) ? today : request.DueDate,
            Notes = request.Notes ?? "",
            PaymentTerms = "Contado",
            Lines = request.Lines.Select(line => new InvoiceLineInput
            {
                ProductId = line.ProductId,
                Description = line.Description ?? "",
                Qty = line.Qty <= 0 ? 1 : line.Qty,
                UnitPriceCents = line.UnitPriceCents,
                IvaRate = line.IvaRate,
            }).ToList(),
        };

        var invoiceId = InvoiceService.Save(input, emit: true, AppState.UserId);
        var invoice = InvoiceService.GetInvoice(invoiceId)
                      ?? throw new InvalidOperationException("La factura se creó pero no se pudo leer.");

        var payment = request.Payment;
        var charged = false;
        var terminalDetail = "";
        var terminalReference = "";

        if (payment is { Method.Length: > 0 } && payment.Method != "ninguno")
        {
            charged = true;
            var reference = "";
            var method = payment.Method;

            if (payment.Method == "tarjeta")
            {
                var (terminal, reason) = MercadoPagoPointTerminal.Resolve();
                var result = terminal.ChargeAsync(
                        new TerminalRequest
                        {
                            AmountCents = invoice.TotalCents,
                            Description = $"Venta {invoice.Series}-{invoice.Number}",
                        },
                        CancellationToken.None)
                    .GetAwaiter().GetResult();

                terminalDetail = $"{terminal.Name}: {result.Detail}";
                terminalReference = result.Reference;
                if (!result.Approved)
                {
                    charged = false;
                    method = "";
                    if (payment.RequireTerminal)
                    {
                        throw new InvalidOperationException(
                            $"La terminal no aprobó el cobro. {reason} {result.Detail}".Trim());
                    }
                }
                else
                {
                    reference = result.Reference;
                }
            }

            if (charged)
            {
                InvoiceService.RegisterPayment(invoiceId, invoice.Date, invoice.TotalCents,
                    payment.AccountId, method == "tarjeta" ? "Tarjeta" : "Efectivo",
                    reference, "Cobro desde el teléfono", AppState.UserId);
            }
        }

        var updated = InvoiceService.GetInvoice(invoiceId);
        return Results.Json(new
        {
            id = invoiceId,
            folio = $"{updated!.Series}-{updated.Number}",
            totalCents = updated.TotalCents,
            status = updated.Status,
            paid = charged,
            terminal = terminalDetail,
            terminalReference,
        });
    }

    private static IResult ChargeTerminal(long invoiceId)
    {
        var invoice = InvoiceService.GetInvoice(invoiceId)
                      ?? throw new InvalidOperationException("La factura no existe.");
        var outstanding = invoice.TotalCents - invoice.PaidCents;
        if (outstanding <= 0)
            throw new InvalidOperationException("La factura ya está pagada.");

        var (terminal, reason) = MercadoPagoPointTerminal.Resolve();
        var result = terminal.ChargeAsync(
                new TerminalRequest
                {
                    AmountCents = outstanding,
                    Description = $"Venta {invoice.Series}-{invoice.Number}",
                },
                CancellationToken.None)
            .GetAwaiter().GetResult();

        if (result.Approved)
        {
            InvoiceService.RegisterPayment(invoiceId, DateTime.Today.ToString("yyyy-MM-dd"),
                outstanding, null, "Tarjeta", result.Reference, "Cobro desde el teléfono", AppState.UserId);
        }

        return Results.Json(new
        {
            approved = result.Approved,
            reference = result.Reference,
            terminal = terminal.Name,
            reason,
            detail = result.Detail,
        });
    }

    /// <summary>Solo direcciones IPv4 privadas y nunca una interfaz pública.</summary>
    private static IEnumerable<string> LanAddresses()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            IPInterfaceProperties properties;
            try
            {
                properties = nic.GetIPProperties();
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var bytes = unicast.Address.GetAddressBytes();
                var isPrivate = bytes[0] == 10
                                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                                || (bytes[0] == 192 && bytes[1] == 168);
                if (isPrivate) yield return unicast.Address.ToString();
            }
        }
    }

    public void Dispose()
    {
        var app = _app;
        _app = null;
        if (app is null) return;
        try
        {
            app.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Detener el servidor nunca debe romper el cierre de la aplicación.
        }
        try
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Igual que arriba.
        }
    }

    private sealed class PairRequest
    {
        public string Pin { get; set; } = "";
        public string? DeviceName { get; set; }
    }

    private sealed class SaleRequest
    {
        public long ContactId { get; set; }
        public string? Date { get; set; }
        public string? DueDate { get; set; }
        public string? Notes { get; set; }
        public List<SaleLine> Lines { get; set; } = new();
        public SalePayment? Payment { get; set; }
    }

    private sealed class SaleLine
    {
        public long? ProductId { get; set; }
        public string? Description { get; set; }
        public double Qty { get; set; } = 1;
        public long UnitPriceCents { get; set; }
        public double IvaRate { get; set; } = 0.16;
    }

    private sealed class SalePayment
    {
        public string Method { get; set; } = "ninguno";
        public long? AccountId { get; set; }
        public bool RequireTerminal { get; set; }
    }
}
