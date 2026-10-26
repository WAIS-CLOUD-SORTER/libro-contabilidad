using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Libro · Contabilidad")]
[assembly: AssemblyDescription("Lanzador local de Libro · Contabilidad")]
[assembly: AssemblyProduct("Libro · Contabilidad")]
[assembly: AssemblyCompany("Libro")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Libro
{
    internal static class Program
    {
        private const string BaseUrl = "http://127.0.0.1:3000";
        private static readonly string AppDir = Path.GetDirectoryName(Application.ExecutablePath);
        private static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Libro");
        private static readonly string DataDir = Path.Combine(DataRoot, "data");
        private static readonly string PidFile = Path.Combine(DataRoot, "server.pid");
        private static readonly string LogFile = Path.Combine(DataRoot, "server.log");

        private static readonly HttpClient Http = CreateHttpClient();

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool stop = HasArg(args, "--stop");
            bool startup = HasArg(args, "--startup");
            bool silent = HasArg(args, "--silent");

            try
            {
                if (stop) return StopServer(silent);

                Directory.CreateDirectory(DataDir);

                if (IsServerUp())
                {
                    if (!startup) OpenBrowser();
                    return 0;
                }

                bool createdNew;
                using (var mutex = new Mutex(true, @"Local\LibroContabilidadLauncher", out createdNew))
                {
                    if (!createdNew)
                    {
                        if (WaitForServer(20))
                        {
                            if (!startup) OpenBrowser();
                            return 0;
                        }
                        if (!silent) ShowError("Libro ya se está iniciando. Espera unos segundos y vuelve a intentarlo.");
                        return 1;
                    }

                    int pid;
                    if (!StartServer(out pid))
                    {
                        if (!silent) ShowError("No se encontró el motor de Libro (node.exe o start-server.cmd).\n\nReinstala la aplicación.");
                        return 1;
                    }

                    if (!WaitForServer(60))
                    {
                        if (!silent)
                        {
                            ShowError("Libro no respondió a tiempo.\n\nRevisa el archivo de diagnóstico:\n" + LogFile +
                                      "\n\nSi el puerto 3000 está ocupado por otro programa, ciérralo y vuelve a intentar.");
                        }
                        return 1;
                    }

                    if (!startup) OpenBrowser();
                    return 0;
                }
            }
            catch (Exception ex)
            {
                if (!silent) ShowError("No se pudo iniciar Libro.\n\n" + ex.Message);
                return 1;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler { UseProxy = false };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
        }

        private static bool IsServerUp()
        {
            try
            {
                var response = Http.GetAsync(BaseUrl + "/api/salud").GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode) return false;
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return body.IndexOf("\"libro\"", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool WaitForServer(int seconds)
        {
            var limit = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < limit)
            {
                if (IsServerUp()) return true;
                Thread.Sleep(500);
            }
            return false;
        }

        private static bool StartServer(out int pid)
        {
            pid = 0;
            string node = Path.Combine(AppDir, "node.exe");
            string script = Path.Combine(AppDir, "start-server.cmd");
            if (!File.Exists(node) || !File.Exists(script)) return false;

            string cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            var info = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = "/c \"\"" + script + "\"\"",
                WorkingDirectory = AppDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (var process = Process.Start(info))
            {
                if (process == null) return false;
                pid = process.Id;
                File.WriteAllText(PidFile, pid.ToString());
                return true;
            }
        }

        private static void OpenBrowser()
        {
            try
            {
                Process.Start(new ProcessStartInfo(BaseUrl) { UseShellExecute = true });
            }
            catch
            {
                ShowError("Libro se inició, pero no se pudo abrir el navegador.\n\nAbre manualmente:\n" + BaseUrl);
            }
        }

        private static int StopServer(bool silent)
        {
            bool wasUp = IsServerUp();
            bool stopped = false;
            int pid;

            if (File.Exists(PidFile) && int.TryParse(File.ReadAllText(PidFile).Trim(), out pid))
            {
                // Solo se mata el PID si sigue siendo el cmd.exe de este servidor.
                // Windows puede reutilizar un PID viejo para otro programa.
                if (IsOurServerProcess(pid)) stopped = KillTree(pid);
            }

            bool nodeKilled = KillNodeProcesses();
            stopped = stopped || nodeKilled;

            try
            {
                if (File.Exists(PidFile)) File.Delete(PidFile);
            }
            catch
            {
                // El archivo de PID es solo informativo.
            }

            bool stillUp = IsServerUp();
            if (!silent)
            {
                if (stillUp)
                {
                    ShowError("No se pudo detener Libro. Cierra la ventana del servidor o reinicia la computadora.");
                    return 1;
                }
                ShowInfo(stopped || wasUp ? "Libro se detuvo correctamente." : "Libro no está en ejecución.");
            }
            return stillUp ? 1 : 0;
        }

        private static bool IsOurServerProcess(int pid)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, CommandLine FROM Win32_Process WHERE ProcessId = " + pid))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        try
                        {
                            string name = Convert.ToString(item["Name"]);
                            string commandLine = Convert.ToString(item["CommandLine"]);
                            if (string.Equals(name, "cmd.exe", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrEmpty(commandLine)
                                && commandLine.IndexOf("start-server.cmd", StringComparison.OrdinalIgnoreCase) >= 0
                                && commandLine.IndexOf(AppDir, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return true;
                            }
                        }
                        finally
                        {
                            item.Dispose();
                        }
                    }
                }
            }
            catch
            {
                // Sin WMI no se puede verificar el PID; se intenta la búsqueda por línea de comandos.
            }
            return false;
        }

        private static bool KillTree(int pid)
        {
            try
            {
                string taskkill = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe");
                var info = new ProcessStartInfo
                {
                    FileName = taskkill,
                    Arguments = "/PID " + pid + " /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var process = Process.Start(info))
                {
                    if (process == null) return false;
                    process.WaitForExit(10000);
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool KillNodeProcesses()
        {
            bool killed = false;
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'node.exe'"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        try
                        {
                            string commandLine = Convert.ToString(item["CommandLine"]);
                            if (string.IsNullOrEmpty(commandLine)) continue;
                            if (commandLine.IndexOf("server.js", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            if (commandLine.IndexOf(AppDir, StringComparison.OrdinalIgnoreCase) < 0) continue;

                            int id = Convert.ToInt32(item["ProcessId"]);
                            using (var process = Process.GetProcessById(id))
                            {
                                process.Kill();
                                killed = true;
                            }
                        }
                        catch
                        {
                            // El proceso pudo haber terminado.
                        }
                        finally
                        {
                            item.Dispose();
                        }
                    }
                }
            }
            catch
            {
                // WMI puede no estar disponible; taskkill ya se intentó.
            }
            return killed;
        }

        private static bool HasArg(string[] args, string name)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void ShowInfo(string message)
        {
            MessageBox.Show(message, "Libro · Contabilidad", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, "Libro · Contabilidad", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
