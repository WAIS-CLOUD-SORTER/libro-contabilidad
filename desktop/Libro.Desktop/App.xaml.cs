using System.Windows;
using System.Windows.Threading;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Libro.Desktop.Views;

namespace Libro.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) AppLog.Error("AppDomain", ex);
            else AppLog.Write("AppDomain: " + args.ExceptionObject);
        };
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Dispatcher", args.Exception);
            MessageBox.Show(
                "Ocurrió un error inesperado.\n\n" + args.Exception.Message,
                "Libro · Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(arg => string.Equals(arg, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            SelfTest.Run();
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        AppLog.Write("Inicio de Libro");

        try
        {
            AppState.Initialize();
            AppLog.Write("Base inicializada: " + AppState.Db.DbPath);

            var devMode = e.Args.Any(arg => string.Equals(arg, "--dev", StringComparison.OrdinalIgnoreCase))
                          && Environment.GetEnvironmentVariable("LIBRO_DEV") == "1";

            if (devMode)
            {
                if (!AppState.HasUsers())
                {
                    var created = AuthService.Setup(AppState.Db, "Abarrotes La Esperanza", "Ana Martínez",
                        "admin@libro.local", "Secreta123!");
                    AppState.RefreshCompany();
                    AppState.SetCurrentUser(created);
                }
                else
                {
                    var result = AuthService.Login(AppState.Db, "admin@libro.local", "Secreta123!");
                    if (result.User is null)
                        throw new InvalidOperationException(result.Error ?? "No se pudo entrar en modo desarrollo.");
                    AppState.RefreshCompany();
                    AppState.SetCurrentUser(result.User);
                }

                ShowMainWindow();
                return;
            }

            if (!AppState.HasUsers())
            {
                AppLog.Write("Sin usuarios: mostrando configuración inicial");
                var setup = new SetupWindow();
                if (setup.ShowDialog() != true)
                {
                    AppLog.Write("Configuración inicial cancelada");
                    Shutdown();
                    return;
                }
            }
            else
            {
                AppLog.Write("Mostrando inicio de sesión");
                var login = new LoginWindow();
                if (login.ShowDialog() != true)
                {
                    AppLog.Write("Inicio de sesión cancelado");
                    Shutdown();
                    return;
                }
            }

            ShowMainWindow();
        }
        catch (Exception ex)
        {
            AppLog.Error("Inicio", ex);
            MessageBox.Show(
                "No se pudo iniciar Libro.\n\n" + ex.Message +
                "\n\nSi el problema continúa, conserva el archivo clave.bin y contacta a soporte.",
                "Libro · Error al iniciar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ShowMainWindow()
    {
        AppLog.Write("Mostrando ventana principal");
        var window = new MainWindow();
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
        StartSalesServerIfEnabled();
        PromptForTerminalSetup(window);
    }

    /// <summary>
    /// La primera vez que se abre Libro tras instalar, ofrece configurar el cobro
    /// con terminal. Se pregunta una sola vez y se recuerda la decisión.
    /// </summary>
    private void PromptForTerminalSetup(MainWindow window)
    {
        try
        {
            if (Libro.Desktop.Sync.MpCredentialStore.IsConfigured) return;

            // En modo demo y en las pruebas automatizadas el aviso estorbaría
            // (dejaría un diálogo modal abierto en medio de la ejecución).
            if (Environment.GetCommandLineArgs()
                    .Any(argument => argument.Equals("--dev", StringComparison.OrdinalIgnoreCase)))
                return;
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIBRO_SYNC"))) return;

            var settings = Libro.Desktop.Sync.SyncStore.LoadSettings();
            if (settings.MercadoPagoPrompted) return;

            settings.MercadoPagoPrompted = true;
            Libro.Desktop.Sync.SyncStore.SaveSettings(settings);

            var answer = MessageBox.Show(window,
                "¿Quieres configurar ahora el cobro con tarjeta en una terminal Mercado Pago?" +
                Environment.NewLine + Environment.NewLine +
                "Necesitas el Access Token de tu aplicación en Mercado Pago Developers y el ID del " +
                "terminal. Se guardan cifrados con tu cuenta de Windows." +
                Environment.NewLine + Environment.NewLine +
                "Si prefieres hacerlo después: Configuración → Ventas por celular.",
                "Libro · Cobro con terminal",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes) window.Navigate("configuracion");
        }
        catch (Exception ex)
        {
            AppLog.Error("Aviso de configuración de terminal", ex);
        }
    }

    /// <summary>
    /// Arranca el servidor de ventas para el teléfono si está activado en
    /// Configuración, o si LIBRO_SYNC lo fuerza (útil para pruebas y soporte).
    /// Un fallo aquí nunca debe impedir usar la aplicación.
    /// </summary>
    private void StartSalesServerIfEnabled()
    {
        try
        {
            var settings = Libro.Desktop.Sync.SyncStore.LoadSettings();
            var forced = Environment.GetEnvironmentVariable("LIBRO_SYNC");
            if (!settings.Enabled && string.IsNullOrWhiteSpace(forced)) return;

            var port = settings.Port;
            if (int.TryParse(forced, out var forcedPort) && forcedPort > 0) port = forcedPort;

            var server = Libro.Desktop.Sync.SyncServer.Start(port);
            AppLog.Write($"Servidor de ventas activo en el puerto {port}: {string.Join(", ", server.Urls)}");
        }
        catch (Exception ex)
        {
            AppLog.Error("No se pudo iniciar el servidor de ventas", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Libro.Desktop.Sync.SyncServer.Stop();
        base.OnExit(e);
    }
}
