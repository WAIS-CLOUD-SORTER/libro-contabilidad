using Libro.Desktop.Data;

namespace Libro.Desktop.Services;

public static class AppState
{
    private static readonly object Gate = new();
    private static Database? _database;

    public static Database Db
    {
        get
        {
            if (_database is null) throw new InvalidOperationException("La base de datos no está inicializada.");
            return _database;
        }
    }

    public static User? CurrentUser { get; private set; }
    public static Company Company { get; private set; } = new();

    public static void Initialize()
    {
        lock (Gate)
        {
            if (_database is not null) return;
            _database = new Database();
            _database.Initialize();
            Company = CompanyService.GetCompany(_database);
        }
    }

    public static bool HasUsers() => Db.ScalarLong("SELECT COUNT(*) FROM users;") > 0;

    public static void RefreshCompany() => Company = CompanyService.GetCompany(Db);

    public static void SetCurrentUser(User user)
    {
        CurrentUser = new User
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Active = user.Active,
        };
    }

    public static void SignOut() => CurrentUser = null;

    public static bool Can(string permission) =>
        CurrentUser is not null && Permissions.Can(CurrentUser.Role, permission);

    public static long? UserId => CurrentUser?.Id;
}
