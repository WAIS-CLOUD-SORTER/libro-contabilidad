using Dapper;
using Libro.Desktop.Data;
using Libro.Desktop.Util;

namespace Libro.Desktop.Services;

public sealed class ContactFilters
{
    public string? Query { get; set; }
    public string? Type { get; set; }
    public bool IncludeArchived { get; set; }
}

public sealed class ContactStats
{
    public long Clients { get; set; }
    public long Providers { get; set; }
    public long Archived { get; set; }
    public long ReceivableCents { get; set; }
}

public static class ContactService
{
    public static List<Contact> GetContacts(ContactFilters filters)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!filters.IncludeArchived)
            where.Add("c.archived = 0");
        if (filters.Type is "cliente" or "proveedor")
        {
            where.Add("(c.type = $type OR c.type = 'ambos')");
            parameters.Add("type", filters.Type);
        }
        if (!string.IsNullOrWhiteSpace(filters.Query))
        {
            where.Add("(c.name LIKE $q OR c.rfc LIKE $q OR c.email LIKE $q)");
            parameters.Add("q", $"%{filters.Query.Trim()}%");
        }

        var whereSql = where.Count == 0 ? "" : "WHERE " + string.Join(" AND ", where);
        return AppState.Db.All<Contact>(
            $"""
             SELECT c.*,
                    COALESCE(SUM(CASE WHEN i.status = 'emitida' THEN i.total_cents - i.paid_cents ELSE 0 END), 0)
                      AS receivable_cents
             FROM contacts c
             LEFT JOIN invoices i ON i.contact_id = c.id
             {whereSql}
             GROUP BY c.id
             ORDER BY c.name;
             """,
            parameters);
    }

    public static Contact? GetContact(long id) =>
        AppState.Db.One<Contact>("SELECT * FROM contacts WHERE id = $id;", new { id });

    public static ContactStats GetStats()
    {
        var contacts = AppState.Db.All<Contact>("SELECT * FROM contacts;");
        var receivable = AppState.Db.ScalarLong(
            "SELECT COALESCE(SUM(total_cents - paid_cents), 0) FROM invoices WHERE status = 'emitida';");

        return new ContactStats
        {
            Clients = contacts.Count(c => c.Archived == 0 && c.Type is "cliente" or "ambos"),
            Providers = contacts.Count(c => c.Archived == 0 && c.Type is "proveedor" or "ambos"),
            Archived = contacts.Count(c => c.Archived == 1),
            ReceivableCents = receivable,
        };
    }

    public static long Save(Contact contact, long? userId)
    {
        if (!AppState.Can(Permissions.ContactsWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar contactos.");

        var name = contact.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Escribe el nombre del contacto.");
        if (name.Length > 160)
            throw new ArgumentException("El nombre es demasiado largo.");
        if (contact.Type is not ("cliente" or "proveedor" or "ambos"))
            throw new ArgumentException("Selecciona un tipo de contacto válido.");
        if (!string.IsNullOrWhiteSpace(contact.Email) && !Validation.IsValidEmail(contact.Email))
            throw new ArgumentException("Escribe un correo válido.");
        if (contact.Rfc.Trim().Length > 20)
            throw new ArgumentException("El RFC es demasiado largo.");
        if (contact.Phone.Trim().Length > 60)
            throw new ArgumentException("El teléfono es demasiado largo.");
        if (contact.Address.Trim().Length > 500)
            throw new ArgumentException("La dirección es demasiado larga.");
        if (contact.Notes.Trim().Length > 1000)
            throw new ArgumentException("Las notas son demasiado largas.");

        return AppState.Db.InTransaction((connection, transaction) =>
        {
            long id;
            if (contact.Id == 0)
            {
                connection.Execute(
                    """
                    INSERT INTO contacts (type, name, rfc, email, phone, address, notes)
                    VALUES ($type, $name, $rfc, $email, $phone, $address, $notes);
                    """,
                    new
                    {
                        type = contact.Type,
                        name,
                        rfc = contact.Rfc.Trim().ToUpperInvariant(),
                        email = contact.Email.Trim(),
                        phone = contact.Phone.Trim(),
                        address = contact.Address.Trim(),
                        notes = contact.Notes.Trim(),
                    }, transaction);
                id = connection.ExecuteScalar<long>("SELECT last_insert_rowid();", transaction: transaction);
                Audit(connection, transaction, userId, "create", id, name);
            }
            else
            {
                id = contact.Id;
                var exists = connection.ExecuteScalar<long>(
                    "SELECT COUNT(*) FROM contacts WHERE id = $id;", new { id }, transaction);
                if (exists == 0) throw new InvalidOperationException("El contacto no existe.");

                connection.Execute(
                    """
                    UPDATE contacts SET type = $type, name = $name, rfc = $rfc, email = $email,
                      phone = $phone, address = $address, notes = $notes
                    WHERE id = $id;
                    """,
                    new
                    {
                        id,
                        type = contact.Type,
                        name,
                        rfc = contact.Rfc.Trim().ToUpperInvariant(),
                        email = contact.Email.Trim(),
                        phone = contact.Phone.Trim(),
                        address = contact.Address.Trim(),
                        notes = contact.Notes.Trim(),
                    }, transaction);
                Audit(connection, transaction, userId, "update", id, name);
            }

            return id;
        });
    }

    public static void SetArchived(long id, bool archived, long? userId)
    {
        if (!AppState.Can(Permissions.ContactsWrite))
            throw new UnauthorizedAccessException("No tienes permiso para modificar contactos.");

        AppState.Db.InTransaction((connection, transaction) =>
        {
            var exists = connection.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM contacts WHERE id = $id;", new { id }, transaction);
            if (exists == 0) throw new InvalidOperationException("El contacto no existe.");

            connection.Execute("UPDATE contacts SET archived = $archived WHERE id = $id;",
                new { archived = archived ? 1 : 0, id }, transaction);
            Audit(connection, transaction, userId, archived ? "archive" : "restore", id, "");
        });
    }

    private static void Audit(Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction, long? userId, string action, long id, string detail)
    {
        connection.Execute(
            "INSERT INTO audit_log (user_id, action, entity, entity_id, detail) VALUES ($userId, $action, 'contact', $id, $detail);",
            new { userId, action, id = id.ToString(), detail }, transaction);
    }
}
