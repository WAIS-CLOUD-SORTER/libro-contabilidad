---
name: cli-anything-libro
description: >-
  Operate Libro Contabilidad through its native Windows CLI. Use for Libro
  accounting tasks: setup/login, dashboard, contacts, products, inventory,
  transactions, internal invoices and payments, employees, payroll, reports,
  taxes, users/roles, audit, backups and restore. The CLI speaks JSON, uses
  integer cents, dates in yyyy-MM-dd, respects Libro permissions, and never
  replaces the SQLCipher/DPAPI backend. Invoices are internal documents and are
  not CFDI.
---

# Libro CLI — agent skill

Use the real Libro backend through `libro-cli`. Do not inspect or edit the
encrypted database directly, and do not reimplement accounting rules.

## Locate the harness

Preferred path in this workspace:

```text
C:\Proyectos\IDK\agent-harness\libro-cli.cmd
```

If the workspace moved, locate `agent-harness\libro-cli.cmd` or set:

```powershell
$env:LIBRO_CLI_DOTNET = "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
```

The launcher needs `agent-harness\Libro.Cli\bin\Release\net8.0-windows\libro-cli.dll`.
If it is missing, build first:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Proyectos\IDK\agent-harness\build.ps1
```

## First moves

Always diagnose before writing:

```powershell
$cli = "C:\Proyectos\IDK\agent-harness\libro-cli.cmd"

& $cli --json doctor
& $cli --json version
& $cli --json schema
```

`doctor` is safe on an empty folder: it does not create a database or key. If it
reports `keyExists=false` while `databaseExists=true`, stop. Do not create a new
key over an encrypted database; restore the correct data folder or backup.

## Authentication

A one-shot CLI process does not keep a session. Put global options **before**
the command group, or use environment variables:

```powershell
$env:LIBRO_EMAIL = "admin@ejemplo.mx"
$env:LIBRO_PASSWORD = "TuClaveSegura"
& $cli --json dashboard

# Equivalent, but avoid exposing passwords in process lists:
& $cli --json --email "admin@ejemplo.mx" --password "TuClaveSegura" dashboard
```

First-time setup on a **new** data directory:

```powershell
& $cli --data-dir "$env:TEMP\libro-agent" --json auth setup `
  --company "Negocio Demo" --name "Admin Demo" `
  --email "admin@demo.mx" --password "Secreta123!"
```

After setup, subsequent processes need the credentials again. `auth setup` only
works when there are no users. For an existing database use `auth login` or the
REPL.

Important parsing rule:

- `--email` / `--password` **before** the command group are login credentials.
- `--email` after `users` is the new user's email.
- `--password` after `users` is not a new password; use `--new-password`.
- `--json`, `--data-dir`, `--debug`, `--password-env` are global.

Use the REPL when you want the session to persist in memory:

```powershell
& $cli --data-dir "$env:TEMP\libro-agent"
# libro> auth login
# libro> dashboard
# libro> exit
```

## JSON contract

- Success: exit code `0`, JSON on stdout.
- Failure: non-zero exit, JSON object on **stderr**:

```json
{ "ok": false, "error": "Permiso insuficiente: se requiere `tx.write`." }
```

- Use `--debug` for a technical `detail`.
- Amounts are integer cents. `100000` means `$1,000.00`.
- Dates are `yyyy-MM-dd`.
- Do not parse human tables when `--json` is available.
- Write JSON inputs to a temporary file and use `--input`; `--stdin` also works.
- Discover input shapes with `schema <operation>`, for example:
  `schema contact.save`, `schema product.save`, `schema transaction.save`,
  `schema invoice.save`, `schema inventory.movement`, `schema employee.save`.

## Command map

| Group | Actions |
|---|---|
| `doctor` | Data/encryption/backup diagnosis. |
| `auth` | `setup`, `login`, `status`, `logout`, `change-password`, `whoami`. |
| `company` | `show`, `update`. |
| `dashboard` | Current-month panel. |
| `accounts` | `list`, `balances`, `save`, `archive`. |
| `categories` | `list`, `save`. |
| `contacts` | `list`, `show`, `stats`, `save`, `archive`. |
| `products` | `list`, `low-stock`, `show`, `summary`, `save`, `toggle`. |
| `transactions` | `list`, `show`, `totals`, `save`, `delete`. |
| `invoices` | `list`, `show`, `overview`, `save`, `emit`, `cancel`, `delete`, `pay`, `delete-payment`. |
| `inventory` | `list`, `summary`, `movements`, `movement`. |
| `employees` | `list`, `show`, `summary`, `save`, `toggle`. |
| `payroll` | `list`, `show`, `summary`, `create`, `recalc`, `update-item`, `delete-item`, `pay`, `revert`, `delete`. |
| `taxes` | `summary`, `annual`. |
| `reports` | `income`, `cashflow`, `aging`, `monthly`, `balances`. |
| `users` | `list`, `summary`, `save`, `activate`, `deactivate`, `reset-password`, `roles`. |
| `audit` | `list`. |
| `backup` | `create`, `list`, `restore`. |
| `schema`, `version`, `help`, `repl` | Utilities. |

## Common workflows

### Inspect before changing

```powershell
& $cli --json dashboard
& $cli --json accounts list
& $cli --json categories list
& $cli --json contacts list
& $cli --json products list
& $cli --json transactions list --from 2026-10-01 --to 2026-10-31
& $cli --json invoices list --status emitida
& $cli --json reports income --from 2026-10-01 --to 2026-10-31
```

Do not assume IDs. Read `accounts list`, `categories list`, `contacts list`, and
`products list` first; the user may have archived rows or changed names.

### Contact

```powershell
@'
{
  "type": "cliente",
  "name": "Cliente Demo",
  "rfc": "XAXX010101000",
  "email": "cliente@demo.mx",
  "phone": "5555555555"
}
'@ | Set-Content -Encoding utf8 "$env:TEMP\contact.json"

& $cli --json contacts save --input "$env:TEMP\contact.json"
& $cli --json contacts show --id 1
& $cli --json contacts archive --id 1
& $cli --json contacts archive --id 1 --restore
```

### Product and stock

```powershell
@'
{
  "name": "Servicio Demo",
  "sku": "SERV-001",
  "unit": "servicio",
  "priceCents": 100000,
  "costCents": 20000,
  "ivaRate": 0.16,
  "minStock": 1,
  "trackStock": true,
  "initialStock": 10
}
'@ | Set-Content -Encoding utf8 "$env:TEMP\product.json"

& $cli --json products save --input "$env:TEMP\product.json"
& $cli --json products show --id 1
& $cli --json inventory movement --input "$env:TEMP\movement.json"
```

For an inventory purchase, include `accountId`; Libro creates the corresponding
expense. Use `type` `entrada`, `salida`, or `ajuste` according to the service's
accepted values.

### Income/expense transaction

```json
{
  "date": "2026-10-25",
  "type": "gasto",
  "categoryId": 4,
  "accountId": 1,
  "description": "Renta",
  "subtotalCents": 10000,
  "ivaRate": 0.16,
  "method": "Efectivo",
  "reference": ""
}
```

`type` is `ingreso` or `gasto`. `subtotalCents` is the amount before IVA. Libro
calculates `ivaCents` and `totalCents`.

### Internal invoice and payment

```powershell
@'
{
  "contactId": 1,
  "date": "2026-10-25",
  "dueDate": "2026-11-24",
  "notes": "Factura interna",
  "paymentTerms": "Contado",
  "lines": [
    {
      "productId": 1,
      "description": "Servicio Demo",
      "qty": 1,
      "unitPriceCents": 100000,
      "ivaRate": 0.16
    }
  ]
}
'@ | Set-Content -Encoding utf8 "$env:TEMP\invoice.json"

& $cli --json invoices save --input "$env:TEMP\invoice.json" --emit
& $cli --json invoices show --id 1
& $cli --json invoices pay --id 1 --amount-cents 116000 --account 1 --method Transferencia
```

- `--emit` changes the invoice from draft to emitted and applies stock effects.
- A payment amount must be exact cents and cannot exceed the balance.
- `invoices cancel` is for emitted invoices; `invoices delete` only for drafts.
- **Never call this CFDI, never claim SAT filing, and never promise XML.**
  Libro invoices are internal accounting documents.

### Payroll

```powershell
& $cli --json employees save --input "$env:TEMP\employee.json"
& $cli --json payroll create --year 2026 --month 10 --type mensual
& $cli --json payroll show --id 1
& $cli --json payroll pay --id 1 --account 1 --date 2026-10-25
& $cli --json payroll summary
```

`payroll create` includes active employees and estimates ISR/IMSS according to
Libro's existing rules. `payroll pay` generates the accounting transaction.
`payroll revert` reverses the payment; `payroll delete` removes a draft period.

### Reports and taxes

```powershell
& $cli --json reports income --from 2026-10-01 --to 2026-10-31
& $cli --json reports cashflow --from 2026-10-01 --to 2026-10-31
& $cli --json reports aging
& $cli --json reports monthly
& $cli --json reports balances
& $cli --json taxes summary --from 2026-10-01 --to 2026-10-31
& $cli --json taxes annual --year 2026
```

If dates are omitted, income/cashflow/taxes use the current month and monthly
uses the last 12 months.

### Users and permissions

```powershell
& $cli --json users list
& $cli --json users roles
& $cli --json users save `
  --name "Consulta Demo" --email "consulta@demo.mx" `
  --role consulta --new-password "ConsultaLibro123!"
& $cli --json users reset-password --id 2 --new-password "OtraClave123!"
```

Roles: `admin`, `contador`, `capturista`, `consulta`. A `consulta` can read
reports, taxes, and operational data but cannot write. The CLI pre-checks
permissions and the services re-check them.

### Backup and restore

Before a risky batch of writes:

```powershell
& $cli --json backup create
& $cli --json backup list --hash
```

Restore only with explicit user intent:

```powershell
& $cli --json backup restore --file "C:\ruta\Libro-20261025-161141.libro" --yes
```

A restore replaces the current database. It verifies the backup is readable with
the current key and creates a safety backup first. Backups are DPAPI-bound: do not
restore another user's/machine's backup without the matching `clave.bin`.

## Safety rules for agents

1. **Never use the real data directory for tests or demos.** Use a unique
   `%TEMP%\libro-agent-*` or `%TEMP%\libro-cli-tests-*` folder.
2. **Never delete or overwrite `clave.bin`.** It is the DPAPI-protected key.
3. **Back up before batch updates**, imports, payroll runs, or restores.
4. **Read before writing.** Resolve IDs by listing accounts, categories,
   contacts, products, employees, and invoices.
5. **Check exit codes and stderr.** Do not treat empty stdout as success.
6. **Do not print passwords** or include them in logs. Prefer
   `LIBRO_PASSWORD` or the REPL password prompt.
7. **Do not bypass the CLI** by opening the SQLCipher database with other tools.
8. **Do not claim CFDI/SAT compliance.** Internal invoices only.
9. **Do not restore over a live write.** Close other Libro/CLI write operations.
10. **Use cents.** Never send decimal pesos in fields ending in `Cents`.

## Error handling

| Error | Meaning | Action |
|---|---|---|
| `Falta --input` | JSON input missing. | Write a temp JSON file or use `--stdin`; check `schema`. |
| `JSON inválido` | Malformed input. | Validate JSON; check property types. |
| `Permiso insuficiente` | Role cannot perform the write. | Use an authorized role or a read-only command. |
| `Correo o contraseña incorrectos` | Login failed. | Check env/global credentials; remember command-specific `--email` is not login. |
| `Falta --new-password` | Creating/resetting a user without a new password. | Add `--new-password`. |
| `La restauración reemplaza la base actual` | Restore guard. | Add `--yes` only with explicit consent. |
| `El archivo de respaldo no existe` | Wrong path. | Use `backup list` to find the file. |
| `Falta clave.bin` | Encrypted DB without its DPAPI key. | Restore the full data folder/backup; do not create a new key. |
| `database is locked` | Another process is writing. | Close the other process and retry. |

## Verification after writes

```powershell
& $cli --json transactions show --id <id>
& $cli --json invoices show --id <id>
& $cli --json payroll show --id <id>
& $cli --json products show --id <id>
& $cli --json reports income --from <from> --to <to>
```

Run `doctor` after changing credentials, restoring a backup, or moving data
folders.

## Reference files

- `C:\Proyectos\IDK\agent-harness\README.md` — installation and command reference.
- `C:\Proyectos\IDK\agent-harness\TEST.md` — test plan and results.
- `C:\Proyectos\IDK\agent-harness\Libro.Cli\Commands.cs` — exact command behavior.
- `C:\Proyectos\IDK\desktop\Libro.Desktop\Services\` — backend services.
