using Libro.Desktop.Data;
using Libro.Desktop.Payroll;
using Libro.Desktop.Util;

namespace Libro.Desktop.Services;

public sealed class UserSummary
{
    public long Total { get; set; }
    public long Active { get; set; }
    public long Admins { get; set; }
}

public sealed class AuditFilters
{
    public string Query { get; set; } = "";
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public static class UserService
{
    public static List<User> GetUsers() => CompanyService.GetUsers(AppState.Db);

    public static UserSummary GetSummary()
    {
        var row = AppState.Db.One<UserSummary>(
            """
            SELECT COUNT(*) AS total,
                   COALESCE(SUM(CASE WHEN active = 1 THEN 1 ELSE 0 END), 0) AS active,
                   COALESCE(SUM(CASE WHEN active = 1 AND role = 'admin' THEN 1 ELSE 0 END), 0) AS admins
            FROM users;
            """) ?? new UserSummary();
        return row;
    }

    public static long SaveUser(long id, string name, string email, string role, bool active,
        string? password = null)
    {
        EnsureManager();
        name = Validation.Trim(name, 120);
        email = Validation.Trim(email, 160).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Escribe el nombre del usuario.");
        if (Validation.ValidateEmail(email) is { } emailError)
            throw new ArgumentException(emailError);
        if (!Permissions.Roles.Any(item => item.Key == role))
            throw new ArgumentException("El rol seleccionado no es válido.");

        var duplicate = AppState.Db.ScalarLong(
            "SELECT COUNT(*) FROM users WHERE lower(email) = $email AND id <> $id;",
            new { email, id });
        if (duplicate > 0)
            throw new InvalidOperationException("Ya existe un usuario con ese correo.");

        var now = Dates.NowIso();
        if (id > 0)
        {
            var current = AppState.Db.One<User>("SELECT * FROM users WHERE id = $id;", new { id })
                          ?? throw new InvalidOperationException("El usuario no existe.");
            if (current.Id == AppState.UserId && (!active || role != "admin") && current.Role == "admin")
                throw new InvalidOperationException(
                    "No puedes quitarte a ti mismo el rol de administrador ni desactivar tu cuenta.");
            if ((!active || role != "admin") && current.Role == "admin")
                EnsureAnotherAdmin(id);

            AppState.Db.Execute(
                """
                UPDATE users SET name = $name, email = $email, role = $role, active = $active
                WHERE id = $id;
                """, new { name, email, role, active = active ? 1 : 0, id });
            if (!string.IsNullOrEmpty(password))
            {
                if (Validation.ValidatePassword(password) is { } passwordError)
                    throw new ArgumentException(passwordError);
                AppState.Db.Execute("UPDATE users SET password_hash = $hash WHERE id = $id;",
                    new { hash = Scrypt.HashPassword(password), id });
            }
            AppState.Db.Audit(AppState.UserId, "update", "user", id, $"{name} · {role}");
            return id;
        }

        if (Validation.ValidatePassword(password) is { } error)
            throw new ArgumentException(error);
        var hash = Scrypt.HashPassword(password!);
        var newId = AppState.Db.Insert(
            """
            INSERT INTO users (name, email, password_hash, role, active, last_login_at)
            VALUES ($name, $email, $hash, $role, $active, $now);
            """,
            new { name, email, hash, role, active = active ? 1 : 0, now = (string?)null });
        AppState.Db.Audit(AppState.UserId, "create", "user", newId, $"{name} · {role}");
        return newId;
    }

    public static void SetActive(long id, bool active)
    {
        EnsureManager();
        if (id == AppState.UserId && !active)
            throw new InvalidOperationException("No puedes desactivar tu propia cuenta.");
        var user = AppState.Db.One<User>("SELECT * FROM users WHERE id = $id;", new { id })
                   ?? throw new InvalidOperationException("El usuario no existe.");
        if (!active && user.Role == "admin") EnsureAnotherAdmin(id);
        AppState.Db.Execute("UPDATE users SET active = $active WHERE id = $id;",
            new { active = active ? 1 : 0, id });
        AppState.Db.Audit(AppState.UserId, active ? "activate" : "deactivate", "user", id, user.Name);
    }

    public static void ResetPassword(long id, string password)
    {
        EnsureManager();
        var user = AppState.Db.One<User>("SELECT * FROM users WHERE id = $id;", new { id })
                   ?? throw new InvalidOperationException("El usuario no existe.");
        if (Validation.ValidatePassword(password) is { } error)
            throw new ArgumentException(error);
        AppState.Db.Execute("UPDATE users SET password_hash = $hash WHERE id = $id;",
            new { hash = Scrypt.HashPassword(password), id });
        AppState.Db.Audit(AppState.UserId, "reset_password", "user", id, user.Name);
    }

    private static void EnsureAnotherAdmin(long excludedId)
    {
        var admins = AppState.Db.ScalarLong(
            "SELECT COUNT(*) FROM users WHERE role = 'admin' AND active = 1 AND id <> $id;",
            new { id = excludedId });
        if (admins == 0)
            throw new InvalidOperationException("Debe quedar al menos un administrador activo.");
    }

    private static void EnsureManager()
    {
        if (!AppState.Can(Permissions.UsersManage))
            throw new UnauthorizedAccessException("No tienes permiso para administrar usuarios.");
    }
}

public static class AuditService
{
    public static List<AuditEntry> GetEntries(AuditFilters filters, int limit = 500)
    {
        if (!AppState.Can(Permissions.AuditView))
            throw new UnauthorizedAccessException("No tienes permiso para ver la auditoría.");
        var where = new List<string>();
        var parameters = new Dapper.DynamicParameters();
        if (!string.IsNullOrWhiteSpace(filters.Query))
        {
            where.Add("(COALESCE(u.name, '') LIKE $query OR COALESCE(a.detail, '') LIKE $query OR a.entity LIKE $query)");
            parameters.Add("query", $"%{filters.Query.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(filters.Action))
        {
            where.Add("a.action = $action");
            parameters.Add("action", filters.Action);
        }
        if (!string.IsNullOrWhiteSpace(filters.Entity))
        {
            where.Add("a.entity = $entity");
            parameters.Add("entity", filters.Entity);
        }
        if (Dates.IsValidIsoDate(filters.From))
        {
            where.Add("a.created_at >= $from");
            parameters.Add("from", filters.From + " 00:00:00");
        }
        if (Dates.IsValidIsoDate(filters.To))
        {
            where.Add("a.created_at <= $to");
            parameters.Add("to", filters.To + " 23:59:59");
        }
        parameters.Add("limit", limit);

        return AppState.Db.All<AuditEntry>(
            $"""
             SELECT a.*, u.name AS user_name
             FROM audit_log a
             LEFT JOIN users u ON u.id = a.user_id
             {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
             ORDER BY a.created_at DESC, a.id DESC
             LIMIT $limit;
             """, parameters);
    }

    public static List<string> GetActions() =>
        AppState.Db.All<string>(
            "SELECT DISTINCT action FROM audit_log ORDER BY action;").ToList();

    public static List<string> GetEntities() =>
        AppState.Db.All<string>(
            "SELECT DISTINCT entity FROM audit_log ORDER BY entity;").ToList();
}
