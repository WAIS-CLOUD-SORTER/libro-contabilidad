using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Libro.Desktop.Data;
using Libro.Desktop.Payroll;
using Libro.Desktop.Services;
using Libro.Desktop.Util;
using Libro.Desktop.Views.Dialogs;
using Microsoft.Win32;

namespace Libro.Desktop.Views;

public sealed class AccountSettingRow
{
    public required Account Account { get; init; }
    public string Name => Account.Name;
    public string TypeLabel => Account.Type switch
    {
        "banco" => "Banco",
        "tarjeta" => "Tarjeta",
        "otro" => "Otro",
        _ => "Efectivo",
    };
    public string OpeningLabel => Money.Format(Account.OpeningBalanceCents);
    public string BalanceLabel => Money.Format(Account is AccountBalance balance
        ? balance.BalanceCents
        : Account.OpeningBalanceCents);
    public string StatusLabel => Account.Active == 1 ? "Activa" : "Inactiva";
    public string ToggleLabel => Account.Active == 1 ? "Desactivar" : "Activar";
}

public sealed class CategorySettingRow
{
    public required Category Category { get; init; }
    public string Name => Category.Name;
    public string KindLabel => Category.Kind == "ingreso" ? "Ingreso" : "Gasto";
    public string StatusLabel => Category.Archived == 1 ? "Archivada" : "Activa";
    public string ToggleLabel => Category.Archived == 1 ? "Restaurar" : "Archivar";
}

public sealed class BackupFileRow
{
    public required string Path { get; init; }
    public string FileName => System.IO.Path.GetFileName(Path);
    public string DateLabel
    {
        get
        {
            try
            {
                return File.GetLastWriteTime(Path)
                    .ToString("dd MMM yyyy, HH:mm", CultureInfo.GetCultureInfo("es-MX"));
            }
            catch
            {
                return "—";
            }
        }
    }
    public string SizeLabel
    {
        get
        {
            try
            {
                var bytes = new FileInfo(Path).Length;
                return bytes >= 1024 * 1024
                    ? $"{bytes / 1024d / 1024d:0.0} MB"
                    : $"{Math.Max(1, bytes / 1024d):0} KB";
            }
            catch
            {
                return "—";
            }
        }
    }
}

public partial class SettingsView : UserControl, IRefreshable
{
    private bool _initialized;
    private bool _canSettings;
    private bool _canAccounts;
    private bool _canBackups;

    public SettingsView()
    {
        InitializeComponent();
        SettingsNav.SelectedIndex = 0;
    }

    public void Refresh()
    {
        if (!_initialized)
        {
            _canSettings = AppState.Can(Permissions.SettingsManage);
            _canAccounts = _canSettings || AppState.Can(Permissions.AccountsWrite);
            _canBackups = AppState.Can(Permissions.BackupManage);

            foreach (var box in new[]
                     {
                         CompanyNameBox, CompanyRfcBox, RegimenBox, PhoneBox, EmailBox,
                         AddressBox, IvaRateBox, SeriesBox, NextNumberBox, ImssRateBox, IsrBox,
                     })
                box.IsReadOnly = !_canSettings;

            SaveCompanyButton.Visibility = _canSettings ? Visibility.Visible : Visibility.Collapsed;
            ResetIsrButton.Visibility = _canSettings ? Visibility.Visible : Visibility.Collapsed;
            NewAccountButton.Visibility = _canAccounts ? Visibility.Visible : Visibility.Collapsed;
            AccountActionsColumn.Visibility = _canAccounts ? Visibility.Visible : Visibility.Collapsed;
            NewCategoryButton.Visibility = _canAccounts ? Visibility.Visible : Visibility.Collapsed;
            CategoryActionsColumn.Visibility = _canAccounts ? Visibility.Visible : Visibility.Collapsed;
            CreateBackupButton.Visibility = _canBackups ? Visibility.Visible : Visibility.Collapsed;
            SaveBackupButton.Visibility = _canBackups ? Visibility.Visible : Visibility.Collapsed;
            RestoreFileButton.Visibility = _canBackups ? Visibility.Visible : Visibility.Collapsed;
            RestoreActionsColumn.Visibility = _canBackups ? Visibility.Visible : Visibility.Collapsed;
            BackupsNavItem.Visibility = _canBackups ? Visibility.Visible : Visibility.Collapsed;
            if (!_canBackups && SettingsNav.SelectedItem == BackupsNavItem) SettingsNav.SelectedIndex = 0;

            _initialized = true;
        }

        LoadCompany();
        LoadAccounts();
        LoadCategories();
        LoadBackups();
    }

    private void SettingsNav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CompanyPanel is null) return;
        var tag = (SettingsNav.SelectedItem as ListBoxItem)?.Tag as string ?? "negocio";
        CompanyPanel.Visibility = tag == "negocio" ? Visibility.Visible : Visibility.Collapsed;
        AccountsPanel.Visibility = tag == "cuentas" ? Visibility.Visible : Visibility.Collapsed;
        CategoriesPanel.Visibility = tag == "categorias" ? Visibility.Visible : Visibility.Collapsed;
        BackupsPanel.Visibility = tag == "respaldos" ? Visibility.Visible : Visibility.Collapsed;
        SyncPanel.Visibility = tag == "sync" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "sync") LoadSync();
    }

    private void LoadSync()
    {
        try
        {
            var settings = Libro.Desktop.Sync.SyncStore.LoadSettings();
            SyncEnabledCheck.IsChecked = settings.Enabled;

            var server = Libro.Desktop.Sync.SyncServer.Current;
            if (server is { IsRunning: true })
            {
                SyncUrlBox.Text = server.Urls.FirstOrDefault(url => !url.Contains("127.0.0.1"))
                                  ?? server.Urls.FirstOrDefault() ?? "";
                SyncPinBox.Text = server.Pin;
                SyncStatusText.Text = "Servidor activo. Abre la dirección en el teléfono, escribe el PIN y empareja.";
            }
            else
            {
                SyncUrlBox.Text = "";
                SyncPinBox.Text = "";
                SyncStatusText.Text = "Servidor apagado. Actívalo y pulsa Aplicar.";
            }

            var devices = Libro.Desktop.Sync.SyncStore.LoadDevices();
            SyncDevicesList.ItemsSource = devices.Count == 0
                ? new[] { "Ningún teléfono emparejado todavía." }
                : devices.Select(device => $"{device.Name} · último uso {device.LastSeenAt}").ToArray();
            SyncErrorText.Text = "";
            LoadMercadoPago();
        }
        catch (Exception ex)
        {
            AppLog.Error("Cargar ventas por celular", ex);
            SyncErrorText.Text = "No se pudo leer la configuración: " + ex.Message;
        }
    }

    private void SyncApply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = Libro.Desktop.Sync.SyncStore.LoadSettings();
            settings.Enabled = SyncEnabledCheck.IsChecked == true;
            Libro.Desktop.Sync.SyncStore.SaveSettings(settings);

            if (settings.Enabled)
            {
                Libro.Desktop.Sync.SyncServer.Start(settings.Port);
                AppLog.Write($"Servidor de ventas activo en el puerto {settings.Port}");
            }
            else
            {
                Libro.Desktop.Sync.SyncServer.Stop();
                AppLog.Write("Servidor de ventas detenido");
            }

            LoadSync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Aplicar ventas por celular", ex);
            SyncErrorText.Text = ex.Message;
        }
    }

    private void SyncRevoke_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var answer = owner is null
            ? MessageBoxResult.No
            : MessageBox.Show(owner,
                "¿Quitar todos los teléfonos emparejados? Tendrás que emparejarlos otra vez.",
                "Libro", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        Libro.Desktop.Sync.SyncStore.SaveDevices(new List<Libro.Desktop.Sync.PairedDevice>());
        LoadSync();
    }

    // ------------------------------------------------ Mercado Pago (terminal)

    private void LoadMercadoPago()
    {
        var credentials = Libro.Desktop.Sync.MpCredentialStore.Load();
        MpDeviceBox.Text = credentials.DeviceId;

        // El token nunca se vuelve a mostrar: si ya hay uno guardado se indica
        // y dejar el campo vacío al guardar conserva el existente.
        MpStatusText.Text = Libro.Desktop.Sync.MpCredentialStore.IsConfigured
            ? "Hay un Access Token guardado en este equipo. Déjalo vacío para conservarlo."
            : "Sin Access Token: se usa la terminal simulada.";

        var (terminal, reason) = Libro.Desktop.Sync.MercadoPagoPointTerminal.Resolve();
        MpStatusText.Text += $"\nTerminal activa: {terminal.Name}. {reason}";
        MpErrorText.Text = "";
    }

    private async void MpTest_Click(object sender, RoutedEventArgs e)
    {
        MpErrorText.Text = "";
        MpDeviceCombo.ItemsSource = null;

        var token = MpTokenBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(token))
            token = Libro.Desktop.Sync.MpCredentialStore.Load().AccessToken;

        if (string.IsNullOrWhiteSpace(token))
        {
            MpErrorText.Text = "Escribe el Access Token para probarlo.";
            return;
        }

        MpTestButton.IsEnabled = false;
        MpStatusText.Text = "Consultando terminales en Mercado Pago…";
        try
        {
            var (ok, detail, terminals) =
                await Libro.Desktop.Sync.MpTerminals.ListAsync(token, CancellationToken.None);

            if (!ok)
            {
                MpStatusText.Text = "";
                MpErrorText.Text = detail;
                return;
            }

            if (terminals.Count == 0)
            {
                MpStatusText.Text = "Credenciales válidas, pero no hay terminales Point en la cuenta. " +
                                    "Configura el terminal en Mercado Pago antes de cobrar.";
                return;
            }

            MpDeviceCombo.ItemsSource = terminals
                .Select(terminal => $"{terminal.Name} · {terminal.Id}")
                .ToList();
            MpStatusText.Text = $"Credenciales válidas. {terminals.Count} terminal(es) encontrado(s); " +
                                "elige uno para completar el ID.";

            if (string.IsNullOrWhiteSpace(MpDeviceBox.Text)) MpDeviceBox.Text = terminals[0].Id;
        }
        finally
        {
            MpTestButton.IsEnabled = true;
        }
    }

    private void MpDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MpDeviceCombo.SelectedItem is not string selected) return;
        var parts = selected.Split('·');
        if (parts.Length == 2) MpDeviceBox.Text = parts[1].Trim();
    }

    private void MpSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var existing = Libro.Desktop.Sync.MpCredentialStore.Load();
            var token = MpTokenBox.Password.Trim();
            if (string.IsNullOrWhiteSpace(token)) token = existing.AccessToken;

            if (string.IsNullOrWhiteSpace(token))
            {
                MpErrorText.Text = "Sin Access Token no se puede configurar el cobro con terminal.";
                return;
            }

            Libro.Desktop.Sync.MpCredentialStore.Save(new Libro.Desktop.Sync.MercadoPagoCredentials
            {
                AccessToken = token,
                DeviceId = MpDeviceBox.Text.Trim(),
            });

            MpTokenBox.Clear();
            AppLog.Write("Credenciales de Mercado Pago actualizadas");
            LoadMercadoPago();
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar credenciales de Mercado Pago", ex);
            MpErrorText.Text = "No se pudieron guardar: " + ex.Message;
        }
    }

    private void MpForget_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var answer = owner is null
            ? MessageBoxResult.No
            : MessageBox.Show(owner,
                "¿Borrar el Access Token guardado en este equipo? Volverás a la terminal simulada.",
                "Libro", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        Libro.Desktop.Sync.MpCredentialStore.Clear();
        MpTokenBox.Clear();
        MpDeviceBox.Text = "";
        MpDeviceCombo.ItemsSource = null;
        AppLog.Write("Credenciales de Mercado Pago borradas");
        LoadMercadoPago();
    }

    private void LoadCompany()
    {
        var company = AppState.Company;
        CompanyNameBox.Text = company.Name;
        CompanyRfcBox.Text = company.Rfc;
        RegimenBox.Text = company.RegimenFiscal;
        AddressBox.Text = company.Address;
        PhoneBox.Text = company.Phone;
        EmailBox.Text = company.Email;
        IvaRateBox.Text = (company.IvaRate * 100d).ToString("0.####", CultureInfo.CurrentCulture);
        SeriesBox.Text = company.InvoiceSeries;
        NextNumberBox.Text = company.NextInvoiceNumber.ToString(CultureInfo.CurrentCulture);
        ImssRateBox.Text = (company.ImssRate * 100d).ToString("0.####", CultureInfo.CurrentCulture);
        try
        {
            var brackets = Isr.Parse(company.IsrBrackets);
            IsrBox.Text = JsonSerializer.Serialize(brackets, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
        }
        catch
        {
            IsrBox.Text = company.IsrBrackets;
        }
    }

    private void SaveCompany_Click(object sender, RoutedEventArgs e)
    {
        if (!_canSettings) return;
        CompanyErrorText.Text = "";
        if (string.IsNullOrWhiteSpace(CompanyNameBox.Text))
        {
            CompanyErrorText.Text = "Escribe el nombre del negocio.";
            return;
        }
        if (!TryParsePercent(IvaRateBox.Text, out var ivaRate))
        {
            CompanyErrorText.Text = "El IVA debe ser un porcentaje entre 0 y 100.";
            return;
        }
        if (!TryParsePercent(ImssRateBox.Text, out var imssRate))
        {
            CompanyErrorText.Text = "El IMSS debe ser un porcentaje entre 0 y 100.";
            return;
        }
        if (!long.TryParse(NextNumberBox.Text.Trim(), out var nextNumber) || nextNumber < 1)
        {
            CompanyErrorText.Text = "El siguiente folio debe ser un número mayor a cero.";
            return;
        }
        var series = SeriesBox.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(series) || series.Length > 10 ||
            !series.All(char.IsLetterOrDigit))
        {
            CompanyErrorText.Text = "La serie debe tener de 1 a 10 letras o números, sin espacios.";
            return;
        }
        if (CompanyRfcBox.Text.Trim().Length > 0 && CompanyRfcBox.Text.Trim().Length is not (12 or 13))
        {
            CompanyErrorText.Text = "El RFC debe tener 12 o 13 caracteres (puedes dejarlo vacío).";
            return;
        }
        if (!TryParseBrackets(IsrBox.Text, out var brackets, out var bracketError))
        {
            CompanyErrorText.Text = bracketError;
            return;
        }

        try
        {
            var company = new Company
            {
                Id = 1,
                Name = CompanyNameBox.Text.Trim(),
                Rfc = CompanyRfcBox.Text.Trim().ToUpperInvariant(),
                RegimenFiscal = RegimenBox.Text.Trim(),
                Address = AddressBox.Text.Trim(),
                Phone = PhoneBox.Text.Trim(),
                Email = EmailBox.Text.Trim(),
                IvaRate = ivaRate,
                InvoiceSeries = series,
                NextInvoiceNumber = nextNumber,
                ImssRate = imssRate,
                IsrBrackets = JsonSerializer.Serialize(brackets, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }),
            };
            CompanyService.SaveCompany(AppState.Db, company, AppState.UserId);
            AppState.RefreshCompany();
            LoadCompany();
            (Window.GetWindow(this) as MainWindow)?.RefreshCompanyChrome();
            CompanyErrorText.Text = "Configuración guardada.";
            CompanyErrorText.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar configuración", ex);
            CompanyErrorText.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            CompanyErrorText.Text = ex.Message;
        }
    }

    private void ResetIsr_Click(object sender, RoutedEventArgs e)
    {
        if (!_canSettings) return;
        if (MessageBox.Show(
                "¿Restaurar las tarifas de ISR de referencia incluidas en Libro? " +
                "Se reemplazarán las tarifas actuales al guardar.",
                "Libro · Configuración", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
            MessageBoxResult.Yes) return;
        IsrBox.Text = JsonSerializer.Serialize(Isr.Parse(Isr.DefaultJson), new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }

    private static bool TryParsePercent(string? raw, out double rate)
    {
        rate = 0;
        var text = (raw ?? "").Trim().Replace("%", "").Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent) ||
            percent is < 0 or > 100)
            return false;
        rate = percent / 100d;
        return true;
    }

    private static bool TryParseBrackets(string raw, out List<IsrBracket> brackets, out string error)
    {
        brackets = new List<IsrBracket>();
        error = "";
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            };
            var parsed = JsonSerializer.Deserialize<List<IsrBracket>>(raw, options);
            if (parsed is not { Count: > 0 })
            {
                error = "Las tarifas de ISR no pueden estar vacías.";
                return false;
            }
            for (var i = 0; i < parsed.Count; i++)
            {
                var bracket = parsed[i];
                if (bracket.Lower < 0 || bracket.Fixed < 0 || bracket.Rate is < 0 or > 1)
                {
                    error = $"La tarifa {i + 1} tiene un límite, cuota o tasa inválidos.";
                    return false;
                }
                if (bracket.Upper is not null && bracket.Upper <= bracket.Lower)
                {
                    error = $"La tarifa {i + 1} tiene un límite superior menor o igual al inferior.";
                    return false;
                }
                if (i > 0 && parsed[i - 1].Upper is null)
                {
                    error = "Solo la última tarifa puede no tener límite superior.";
                    return false;
                }
            }
            if (parsed[^1].Upper is not null)
            {
                error = "La última tarifa de ISR debe tener el límite superior vacío (null).";
                return false;
            }
            brackets = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            error = "Las tarifas de ISR no son un JSON válido: " + ex.Message;
            return false;
        }
    }

    private void LoadAccounts()
    {
        var accounts = CompanyService.GetAccountBalances(AppState.Db, true);
        AccountsGrid.ItemsSource = accounts.Select(account => new AccountSettingRow
        {
            Account = account,
        }).ToList();
    }

    private void NewAccount_Click(object sender, RoutedEventArgs e)
    {
        if (!_canAccounts) return;
        var dialog = new AccountDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadAccounts();
    }

    private void EditAccount_Click(object sender, RoutedEventArgs e)
    {
        if (!_canAccounts || sender is not Button { Tag: AccountSettingRow row }) return;
        var dialog = new AccountDialog(row.Account) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadAccounts();
    }

    private void ToggleAccount_Click(object sender, RoutedEventArgs e)
    {
        if (!_canAccounts || sender is not Button { Tag: AccountSettingRow row }) return;
        var deactivate = row.Account.Active == 1;
        if (MessageBox.Show(
                deactivate
                    ? $"¿Desactivar la cuenta \"{row.Name}\"? No se borrarán sus movimientos."
                    : $"¿Reactivar la cuenta \"{row.Name}\"?",
                "Libro · Configuración", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
            MessageBoxResult.Yes) return;
        try
        {
            CompanyService.ArchiveAccount(AppState.Db, row.Account.Id, deactivate, AppState.UserId);
            LoadAccounts();
        }
        catch (Exception ex)
        {
            AppLog.Error("Cambiar estado de cuenta", ex);
            MessageBox.Show(ex.Message, "Libro · Configuración",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadCategories()
    {
        var categories = CompanyService.GetCategories(AppState.Db, true);
        if (ShowArchivedCategories.IsChecked != true)
            categories = categories.Where(category => category.Archived == 0).ToList();
        CategoriesGrid.ItemsSource = categories.Select(category => new CategorySettingRow
        {
            Category = category,
        }).ToList();
    }

    private void ShowArchived_Click(object sender, RoutedEventArgs e) => LoadCategories();

    private void NewCategory_Click(object sender, RoutedEventArgs e)
    {
        if (!_canAccounts) return;
        var dialog = new CategoryDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadCategories();
    }

    private void EditCategory_Click(object sender, RoutedEventArgs e)
    {
        if (!_canAccounts || sender is not Button { Tag: CategorySettingRow row }) return;
        var dialog = new CategoryDialog(row.Category) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true) LoadCategories();
    }

    private void ToggleCategory_Click(object sender, RoutedEventArgs e)
    {
        if (!_canAccounts || sender is not Button { Tag: CategorySettingRow row }) return;
        var archive = row.Category.Archived == 0;
        if (MessageBox.Show(
                archive
                    ? $"¿Archivar la categoría \"{row.Name}\"? No aparecerá en movimientos nuevos."
                    : $"¿Restaurar la categoría \"{row.Name}\"?",
                "Libro · Configuración", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
            MessageBoxResult.Yes) return;
        try
        {
            var category = new Category
            {
                Id = row.Category.Id,
                Name = row.Category.Name,
                Kind = row.Category.Kind,
                Archived = archive ? 1 : 0,
            };
            CompanyService.SaveCategory(AppState.Db, category, AppState.UserId);
            LoadCategories();
        }
        catch (Exception ex)
        {
            AppLog.Error("Archivar categoría", ex);
            MessageBox.Show(ex.Message, "Libro · Configuración",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadBackups()
    {
        BackupDirectoryText.Text = AppState.Db.BackupDirectory;
        if (!_canBackups)
        {
            BackupsGrid.ItemsSource = null;
            return;
        }
        try
        {
            Directory.CreateDirectory(AppState.Db.BackupDirectory);
            var files = Directory.GetFiles(AppState.Db.BackupDirectory, "*.libro")
                .Select(path => new BackupFileRow { Path = path })
                .OrderByDescending(row => File.GetLastWriteTime(row.Path))
                .ToList();
            BackupsGrid.ItemsSource = files;
        }
        catch (Exception ex)
        {
            AppLog.Error("Listar respaldos", ex);
            BackupStatusText.Text = "No se pudo leer la carpeta de respaldos: " + ex.Message;
        }
    }

    private void CreateBackup_Click(object sender, RoutedEventArgs e)
    {
        if (!_canBackups) return;
        try
        {
            var path = AppState.Db.BackupTo();
            BackupStatusText.Text = $"Respaldo creado: {System.IO.Path.GetFileName(path)}";
            LoadBackups();
            MessageBox.Show("Respaldo cifrado creado correctamente.\n\n" + path,
                "Libro · Respaldos", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Crear respaldo", ex);
            BackupStatusText.Text = "No se pudo crear el respaldo.";
            MessageBox.Show("No se pudo crear el respaldo.\n\n" + ex.Message,
                "Libro · Respaldos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveBackup_Click(object sender, RoutedEventArgs e)
    {
        if (!_canBackups) return;
        var dialog = new SaveFileDialog
        {
            Title = "Guardar copia del respaldo",
            Filter = "Respaldo de Libro (*.libro)|*.libro",
            FileName = $"Libro-{DateTime.Now:yyyyMMdd-HHmmss}.libro",
            AddExtension = true,
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            AppState.Db.BackupTo(dialog.FileName);
            BackupStatusText.Text = $"Copia guardada: {dialog.FileName}";
            MessageBox.Show("Copia cifrada guardada correctamente.\n\n" + dialog.FileName,
                "Libro · Respaldos", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Guardar copia de respaldo", ex);
            MessageBox.Show("No se pudo guardar la copia.\n\n" + ex.Message,
                "Libro · Respaldos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppState.Db.BackupDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{AppState.Db.BackupDirectory}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("Abrir carpeta de respaldos", ex);
            MessageBox.Show("No se pudo abrir la carpeta.\n\n" + ex.Message,
                "Libro · Respaldos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        if (!_canBackups || sender is not Button { Tag: BackupFileRow row }) return;
        Restore(row.Path);
    }

    private void RestoreFile_Click(object sender, RoutedEventArgs e)
    {
        if (!_canBackups) return;
        var dialog = new OpenFileDialog
        {
            Title = "Restaurar un respaldo",
            Filter = "Respaldo de Libro (*.libro;*.db)|*.libro;*.db|Todos los archivos (*.*)|*.*",
        };
        if (dialog.ShowDialog() == true) Restore(dialog.FileName);
    }

    private void Restore(string path)
    {
        if (MessageBox.Show(
                "Restaurar reemplazará TODOS los datos actuales con el contenido del respaldo.\n\n" +
                "Antes de restaurar se creará un respaldo de seguridad. Libro se cerrará al terminar.\n\n" +
                "¿Continuar?",
                "Libro · Restaurar respaldo", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes) return;

        try
        {
            var safety = AppState.Db.RestoreFrom(path);
            MessageBox.Show(
                "Respaldo restaurado correctamente.\n\n" +
                $"Se guardó una copia de seguridad en:\n{safety}\n\n" +
                "Libro se cerrará para recargar los datos restaurados.",
                "Libro · Respaldos", MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            AppLog.Error("Restaurar respaldo", ex);
            MessageBox.Show(
                "No se pudo restaurar el respaldo. La base actual se conserva.\n\n" + ex.Message,
                "Libro · Respaldos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
