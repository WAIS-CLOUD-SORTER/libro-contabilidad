using System.IO;

namespace Libro.Desktop.Util;

public static class AppLog
{
    private static readonly object Gate = new();

    public static string Path
    {
        get
        {
            try
            {
                var dir = Data.AppPaths.DataDirectory;
                Directory.CreateDirectory(dir);
                return System.IO.Path.Combine(dir, "libro.log");
            }
            catch
            {
                return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "libro.log");
            }
        }
    }

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // El registro nunca debe impedir que la aplicación funcione.
        }
    }

    public static void Error(string context, Exception exception) =>
        Write($"ERROR {context}: {exception}");
}
