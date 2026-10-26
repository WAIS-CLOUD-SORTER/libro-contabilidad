using System.IO;
using System.Text;

namespace Libro.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // La consola puede no permitir cambiar la codificación cuando está redirigida.
        }

        ParsedArgs parsed;
        CliContext context;
        try
        {
            parsed = ParsedArgs.Parse(args);
            context = new CliContext(parsed);
            Output.Json = parsed.Has("json");
        }
        catch (CliException ex)
        {
            Output.Error(ex.Message);
            return ex.ExitCode;
        }

        if (parsed.Has("help"))
        {
            try
            {
                Commands.ShowHelp(context, parsed);
                return 0;
            }
            catch (CliException ex)
            {
                Output.Error(ex.Message);
                return ex.ExitCode;
            }
        }

        if (parsed.Positionals.Count == 0 ||
            string.Equals(parsed.Positionals[0], "repl", StringComparison.OrdinalIgnoreCase))
        {
            return RunRepl(context, parsed);
        }

        return RunOnce(context, parsed);
    }

    private static int RunOnce(CliContext context, ParsedArgs args)
    {
        context.Args = args;
        Output.Json = args.Has("json");
        try
        {
            Commands.Execute(context, args);
            return 0;
        }
        catch (CliException ex)
        {
            Output.Error(ex.Message, context.Debug, ex);
            return ex.ExitCode;
        }
        catch (UnauthorizedAccessException ex)
        {
            Output.Error(ex.Message, context.Debug, ex);
            return 1;
        }
        catch (ArgumentException ex)
        {
            Output.Error(ex.Message, context.Debug, ex);
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            Output.Error(ex.Message, context.Debug, ex);
            return 1;
        }
        catch (IOException ex)
        {
            Output.Error(ex.Message, context.Debug, ex);
            return 1;
        }
        catch (Exception ex)
        {
            Output.Error("Error inesperado: " + ex.Message, context.Debug, ex);
            return 1;
        }
    }

    private static int RunRepl(CliContext context, ParsedArgs initial)
    {
        context.Interactive = true;
        context.Args = initial;
        Output.Json = initial.Has("json");

        try
        {
            if (context.TryAutoLogin(required: false))
            {
                // La sesión se mantiene en memoria durante toda la REPL.
            }
        }
        catch (Exception ex)
        {
            Output.Error(ex.Message, context.Debug, ex);
        }

        if (!Output.Json)
        {
            Console.WriteLine();
            Console.WriteLine("Libro CLI — modo interactivo");
            Console.WriteLine("Escribe `help` para ver comandos, `exit` para salir.");
            Console.WriteLine($"Datos: {Libro.Desktop.Data.AppPaths.DataDirectory}");
            if (Libro.Desktop.Services.AppState.CurrentUser is { } user)
                Console.WriteLine($"Sesión: {user.Name} <{user.Email}> · {Libro.Desktop.Services.Permissions.RoleLabel(user.Role)}");
            else
                Console.WriteLine("Sesión: sin iniciar (usa `auth login`)");
            Console.WriteLine();
        }

        while (true)
        {
            if (!Output.Json)
            {
                var who = Libro.Desktop.Services.AppState.CurrentUser?.Name;
                Console.Write(string.IsNullOrWhiteSpace(who) ? "libro> " : $"{who}> ");
            }

            string? line;
            try
            {
                line = Console.ReadLine();
            }
            catch (Exception ex)
            {
                Output.Error("No se pudo leer la entrada: " + ex.Message, context.Debug, ex);
                return 1;
            }

            if (line is null) break;
            line = line.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('#')) continue;
            if (line is "exit" or "quit" or "q") break;
            if (line is "clear" or "cls")
            {
                try { Console.Clear(); } catch { /* consola redirigida */ }
                continue;
            }

            var tokens = CliContext.Tokenize(line);
            if (tokens.Count == 0) continue;
            if (tokens[0] is "exit" or "quit" or "q") break;

            ParsedArgs command;
            try
            {
                command = ParsedArgs.Parse(tokens);
                MergeGlobals(initial, command);
                context.Args = command;
                Output.Json = command.Has("json");
            }
            catch (CliException ex)
            {
                Output.Error(ex.Message, context.Debug, ex);
                continue;
            }

            if (command.Has("help"))
            {
                try
                {
                    Commands.ShowHelp(context, command);
                }
                catch (Exception ex)
                {
                    Output.Error(ex.Message, context.Debug, ex);
                }
                continue;
            }

            try
            {
                Commands.Execute(context, command);
            }
            catch (CliException ex)
            {
                Output.Error(ex.Message, context.Debug, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                Output.Error(ex.Message, context.Debug, ex);
            }
            catch (ArgumentException ex)
            {
                Output.Error(ex.Message, context.Debug, ex);
            }
            catch (InvalidOperationException ex)
            {
                Output.Error(ex.Message, context.Debug, ex);
            }
            catch (IOException ex)
            {
                Output.Error(ex.Message, context.Debug, ex);
            }
            catch (Exception ex)
            {
                Output.Error("Error inesperado: " + ex.Message, context.Debug, ex);
            }
        }

        return 0;
    }

    private static void MergeGlobals(ParsedArgs initial, ParsedArgs current)
    {
        var initialDir = initial.FirstText("data-dir");
        var currentDir = current.FirstText("data-dir");
        if (!string.IsNullOrWhiteSpace(initialDir) && !string.IsNullOrWhiteSpace(currentDir) &&
            !string.Equals(Path.GetFullPath(initialDir), Path.GetFullPath(currentDir), StringComparison.OrdinalIgnoreCase))
        {
            throw new CliException("No se puede cambiar --data-dir dentro de la REPL. Reinicia el proceso.");
        }

        foreach (var key in new[] { "data-dir", "email", "password", "password-env" })
        {
            if (!current.GlobalOptionValues.ContainsKey(key) &&
                initial.GlobalText(key) is { } value)
            {
                current.GlobalOptionValues[key] = value;
            }
        }

        if (initial.Has("json")) current.GlobalFlags.Add("json");
        if (initial.Has("debug")) current.GlobalFlags.Add("debug");
    }
}
