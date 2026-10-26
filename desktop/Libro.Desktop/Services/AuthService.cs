using Dapper;
using Libro.Desktop.Data;
using Libro.Desktop.Payroll;
using Libro.Desktop.Util;

namespace Libro.Desktop.Services;

public sealed record LoginResult(User? User, string? Error);

public static class AuthService
{
    private static readonly Dictionary<string, (int Count, DateTime ResetAt)> Attempts = new();
    private static readonly object AttemptGate = new();
    private static readonly string DummyHash = Scrypt.HashPassword("contraseña-que-nunca-coincide");

    public static long UserCount(Database db) => db.ScalarLong("SELECT COUNT(*) FROM users;");

    public static User Setup(Database db, string companyName, string name, string email, string password)
    {
        companyName = Validation.Trim(companyName, 120);
        name = Validation.Trim(name, 120);
        email = Validation.Trim(email, 160).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(companyName)) throw new ArgumentException("Escribe el nombre de tu negocio.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Escribe tu nombre.");
        if (Validation.ValidateEmail(email) is { } emailError) throw new ArgumentException(emailError);
        if (Validation.ValidatePassword(password) is { } passwordError) throw new ArgumentException(passwordError);
        if (UserCount(db) > 0) throw new InvalidOperationException("Ya existe una cuenta de administrador.");

        var hash = Scrypt.HashPassword(password);
        var now = Dates.NowIso();

        return db.InTransaction((connection, transaction) =>
        {
            connection.Execute(
                """
                INSERT INTO company (id, name, isr_brackets)
                VALUES (1, $company, $brackets)
                ON CONFLICT(id) DO UPDATE SET name = excluded.name;
                """,
                new { company = companyName, brackets = Isr.DefaultJson },
                transaction);

            connection.Execute(
                """
                INSERT INTO users (name, email, password_hash, role, active, last_login_at)
                VALUES ($name, $email, $hash, 'admin', 1, $now);
                """,
                new { name, email, hash, now },
                transaction);

            var id = connection.ExecuteScalar<long>("SELECT last_insert_rowid();", transaction: transaction);
            connection.Execute(
                """
                INSERT INTO audit_log (user_id, action, entity, entity_id, detail)
                VALUES ($id, 'setup', 'company', '1', $detail);
                """,
                new { id, detail = "Alta inicial de " + companyName },
                transaction);

            return new User
            {
                Id = id,
                Name = name,
                Email = email,
                Role = "admin",
                Active = 1,
                LastLoginAt = now,
            };
        });
    }

    public static LoginResult Login(Database db, string email, string password)
    {
        email = Validation.Trim(email, 160).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return new LoginResult(null, "Escribe tu correo y contraseña.");

        lock (AttemptGate)
        {
            if (Attempts.TryGetValue(email, out var attempt))
            {
                if (attempt.ResetAt > DateTime.UtcNow && attempt.Count >= 10)
                    return new LoginResult(null, "Demasiados intentos. Espera unos minutos e intenta de nuevo.");
                if (attempt.ResetAt <= DateTime.UtcNow) Attempts.Remove(email);
            }
        }

        var user = db.One<User>("SELECT * FROM users WHERE email = $email;", new { email });
        var valid = user is not null
            ? Scrypt.VerifyPassword(password, user.PasswordHash)
            : Scrypt.VerifyPassword(password, DummyHash);

        if (user is null || !valid || user.Active != 1)
        {
            RecordFailure(email);
            db.Audit(null, "login_failed", "user", null, email);
            return new LoginResult(null, "Correo o contraseña incorrectos.");
        }

        lock (AttemptGate) Attempts.Remove(email);
        var now = Dates.NowIso();
        db.Execute("UPDATE users SET last_login_at = $now WHERE id = $id;", new { now, id = user.Id });
        db.Audit(user.Id, "login", "user", user.Id);
        user.LastLoginAt = now;
        return new LoginResult(user, null);
    }

    private static void RecordFailure(string email)
    {
        lock (AttemptGate)
        {
            var now = DateTime.UtcNow;
            if (Attempts.TryGetValue(email, out var attempt) && attempt.ResetAt > now)
                Attempts[email] = (attempt.Count + 1, attempt.ResetAt);
            else
                Attempts[email] = (1, now.AddMinutes(10));
        }
    }

    public static string? ChangePassword(Database db, long userId, string currentPassword, string newPassword)
    {
        var user = db.One<User>("SELECT * FROM users WHERE id = $id;", new { id = userId });
        if (user is null) return "Usuario no encontrado.";
        if (!Scrypt.VerifyPassword(currentPassword, user.PasswordHash))
            return "La contraseña actual no es correcta.";
        if (Validation.ValidatePassword(newPassword) is { } error) return error;

        db.Execute("UPDATE users SET password_hash = $hash WHERE id = $id;",
            new { hash = Scrypt.HashPassword(newPassword), id = userId });
        db.Audit(userId, "change_password", "user", userId);
        return null;
    }
}
