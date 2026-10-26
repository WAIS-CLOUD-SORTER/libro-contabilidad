# Libro CLI — harness agent-native

CLI en **C#/.NET 8** para operar Libro Contabilidad desde agentes, scripts y terminal. Es un envoltorio sobre los servicios reales de la aplicación WPF: no reimplementa contabilidad, cifrado, permisos ni reglas de negocio.

- **Backend real:** referencia `desktop\Libro.Desktop\Libro.Desktop.csproj` y usa `AppState`, `AuthService`, `CompanyService`, `TransactionService`, `InvoiceService`, `InventoryService`, `EmployeeService`, `PayrollService`, `ReportService`, `TaxService`, `UserService` y `AuditService`.
- **Nativo:** Windows, .NET 8, SQLCipher y DPAPI. No usa Node.js, navegador, HTTP ni WebView.
- **Agent-native:** salida JSON estable, errores JSON en stderr, códigos de salida, entrada JSON por archivo o stdin, REPL, `schema` y skill generada.
- **Facturas internas:** el flujo de facturas sigue siendo un documento interno. **No es CFDI**, no timbra y no se integra con el SAT.

## 1. Requisitos y compilación

Requisitos:

- Windows 10/11 x64.
- .NET 8 SDK para compilar y runtime para ejecutar.
- La aplicación nativa Libro ya compilada o su código fuente (este repo).
- Python 3.9+ solo para las pruebas automatizadas.

El `dotnet` que está en el `PATH` de esta máquina es solo runtime; el SDK está en:

```text
%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe
```

Compilar:

```powershell
cd C:\Proyectos\IDK
powershell -ExecutionPolicy Bypass -File agent-harness\build.ps1
```

El script prefiere el SDK local y deja el ejecutable en:

```text
agent-harness\Libro.Cli\bin\Release\net8.0-windows\libro-cli.dll
```

Launcher listo para usar:

```text
agent-harness\libro-cli.cmd
```

El launcher busca `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe`, luego `dotnet` en el `PATH`; también acepta `LIBRO_CLI_DOTNET` para indicar un ejecutable explícito.

## 2. Primer contacto

```powershell
cd C:\Proyectos\IDK

# Diagnóstico sin abrir sesión. No crea base ni llave si no existen.
.\agent-harness\libro-cli.cmd --json doctor

# Ayuda y versión
.\agent-harness\libro-cli.cmd help
.\agent-harness\libro-cli.cmd --json version

# Ver la forma exacta de una entrada JSON
.\agent-harness\libro-cli.cmd --json schema invoice.save
```

`doctor` informa la carpeta de datos, el estado de la base y la llave. Si existe una base cifrada pero falta `clave.bin`, no crea una llave nueva incompatible: lo reporta para que se restaure la carpeta de datos correcta.

## 3. Datos, credenciales y sintaxis

### Carpeta de datos

Por defecto se usa la carpeta real de Libro:

```text
%LOCALAPPDATA%\Libro\data
```

Para pruebas, demos o agentes que no deben tocar datos reales, usa una carpeta temporal y **siempre** pásala antes del comando:

```powershell
$demo = Join-Path $env:TEMP "libro-demo"
.\agent-harness\libro-cli.cmd --data-dir $demo --json doctor
```

La carpeta contiene:

| Archivo | Contenido |
| --- | --- |
| `contabilidad.db` | Base SQLCipher cifrada. |
| `clave.bin` | Llave aleatoria protegida con DPAPI del usuario de Windows. |
| `respaldos\*.libro` | Respaldos cifrados. |
| `libro.log` | Bitácora técnica local. |

**No copies una base cifrada sin su `clave.bin`.** La llave está ligada a la cuenta de Windows y al perfil del equipo; los respaldos `.libro` están pensados para restaurarse con la misma cuenta.

### Credenciales

Cada invocación del CLI es un proceso nuevo. La sesión no se conserva entre procesos (el REPL sí la conserva en memoria).

Opción recomendada para agentes, sin exponer la contraseña en la línea de comandos:

```powershell
$env:LIBRO_EMAIL = "admin@ejemplo.mx"
$env:LIBRO_PASSWORD = "TuClaveSegura"
.\agent-harness\libro-cli.cmd --json dashboard
```

También puedes usar opciones globales **antes** del comando:

```powershell
.\agent-harness\libro-cli.cmd --json --email "admin@ejemplo.mx" --password "TuClaveSegura" dashboard
```

Reglas de parsing:

- Las opciones globales van **antes** del grupo de comando: `--data-dir`, `--json`, `--email`, `--password`, `--password-env`, `--debug`.
- Las opciones después del comando pertenecen a la operación. Por ejemplo, en `users save --email nuevo@ejemplo.mx`, ese `--email` es el correo del usuario nuevo; las credenciales de la sesión se toman de las opciones globales o del entorno.
- Para crear o cambiar contraseñas de usuarios usa `--new-password`; nunca reutilices `--password`, que es la credencial de login.
- `--password-env NOMBRE` lee la contraseña de una variable de entorno; es preferible a `--password` en scripts.

### Formato de datos

- **Importes:** enteros en **centavos** (`long`). `10000` = `$100.00`. No se usan `double` para dinero.
- **Fechas:** `yyyy-MM-dd`.
- **JSON de entrada:** `--input archivo.json` o `--stdin` con un objeto JSON.
- **JSON de salida:** stdout, UTF-8, camelCase, importes en centavos.
- **Éxito:** código `0` y datos en stdout.
- **Error:** código distinto de `0` y un objeto JSON en **stderr**:

```json
{
  "ok": false,
  "error": "Falta --input <archivo.json> o --stdin para contact.save. Consulta `schema contact.save` para ver la forma esperada."
}
```

Con `--debug` se añade `detail` con la traza técnica. No se imprimen hashes de contraseña ni la llave.

## 4. Crear una instalación de prueba

Si la carpeta no tiene usuarios:

```powershell
$demo = Join-Path $env:TEMP "libro-demo-agent"
.\agent-harness\libro-cli.cmd --data-dir $demo --json auth setup `
  --company "Negocio Demo" `
  --name "Admin Demo" `
  --email "admin@demo.mx" `
  --password "Secreta123!"
```

Después, en procesos posteriores:

```powershell
$env:LIBRO_EMAIL = "admin@demo.mx"
$env:LIBRO_PASSWORD = "Secreta123!"
$cli = ".\agent-harness\libro-cli.cmd"

& $cli --data-dir $demo --json dashboard
& $cli --data-dir $demo --json accounts list
& $cli --data-dir $demo --json categories list
```

> `auth setup` solo funciona cuando no hay usuarios. Si ya existe uno, usa `auth login` o el REPL.

## 5. Comandos

| Grupo | Acciones |
| --- | --- |
| `doctor` | Diagnóstico de base, llave, cifrado, respaldos y conteos. |
| `auth` | `setup`, `login`, `status`, `logout`, `change-password`, `whoami`. |
| `company` | `show`, `update`. |
| `dashboard` | Panel del mes actual. |
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
| `schema` | Ejemplos JSON de entrada. |
| `version`, `help`, `repl` | Utilidades. |

### Descubrimiento de esquemas

```powershell
& $cli --json schema
& $cli --json schema contact.save
& $cli --json schema invoice.save
& $cli --json schema transaction.save
```

También puedes consultar `help` para la lista completa de opciones globales.

## 6. Flujos típicos

### Contactos y productos

```powershell
$contactJson = @'
{
  "type": "cliente",
  "name": "Cliente Demo",
  "rfc": "XAXX010101000",
  "email": "cliente@demo.mx",
  "phone": "5555555555"
}
'@
$contactJson | Set-Content -Encoding utf8 "$env:TEMP\contact.json"

& $cli --json contacts save --input "$env:TEMP\contact.json"

$productJson = @'
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
'@
$productJson | Set-Content -Encoding utf8 "$env:TEMP\product.json"

& $cli --json products save --input "$env:TEMP\product.json"
& $cli --json products list
& $cli --json products show --id 1
```

### Movimiento de ingreso o gasto

```json
{
  "date": "2026-10-25",
  "type": "gasto",
  "categoryId": 4,
  "accountId": 1,
  "description": "Renta de prueba",
  "subtotalCents": 10000,
  "ivaRate": 0.16,
  "method": "Efectivo",
  "reference": "TX-001"
}
```

```powershell
& $cli --json transactions save --input "$env:TEMP\transaction.json"
& $cli --json transactions list --from 2026-10-01 --to 2026-10-31
& $cli --json transactions totals --from 2026-10-01 --to 2026-10-31
```

Los IDs de cuentas y categorías se obtienen con `accounts list` y `categories list`; los datos iniciales incluyen cuentas y categorías, pero sus IDs pueden variar si el usuario las edita.

### Factura interna, emisión y cobro

```json
{
  "contactId": 1,
  "date": "2026-10-25",
  "dueDate": "2026-11-24",
  "notes": "Factura interna de prueba",
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
```

```powershell
# Guardar y emitir en una sola operación
& $cli --json invoices save --input "$env:TEMP\invoice.json" --emit

# Consultar y cobrar el total (1000.00 + IVA 16% = 116000 centavos)
& $cli --json invoices show --id 1
& $cli --json invoices pay --id 1 --date 2026-10-25 --amount-cents 116000 --account 1 --method Transferencia

# Cancelar una factura emitida que aún no tenga pagos (usa su propio ID):
# & $cli --json invoices cancel --id <id> --reason "Error de captura"
```

La factura es un documento interno. El folio, los pagos y el saldo viven en la base local; no se genera XML CFDI ni se envía a un PAC.

### Inventario

```json
{
  "productId": 1,
  "date": "2026-10-25",
  "type": "entrada",
  "qty": 5,
  "unitCostCents": 20000,
  "ivaRate": 0.16,
  "reason": "Compra de prueba",
  "accountId": 1
}
```

```powershell
& $cli --json inventory movement --input "$env:TEMP\movement.json"
& $cli --json inventory list --low
& $cli --json inventory summary
& $cli --json inventory movements --id 1
```

Una entrada con `accountId` genera el gasto de compra correspondiente; una salida o una factura emitida ajusta el stock según las reglas de la aplicación.

### Empleados y nómina

```json
{
  "name": "Empleado Demo",
  "rfc": "XAXX010101000",
  "position": "Auxiliar",
  "department": "Administración",
  "salaryCents": 800000,
  "startDate": "2026-01-01",
  "status": "activo"
}
```

```powershell
& $cli --json employees save --input "$env:TEMP\employee.json"
& $cli --json payroll create --year 2026 --month 10 --type mensual
& $cli --json payroll show --id 1
& $cli --json payroll pay --id 1 --account 1 --date 2026-10-25
& $cli --json payroll summary
```

### Reportes e impuestos

```powershell
& $cli --json reports income --from 2026-10-01 --to 2026-10-31
& $cli --json reports cashflow --from 2026-10-01 --to 2026-10-31
& $cli --json reports aging
& $cli --json reports monthly
& $cli --json reports balances
& $cli --json taxes summary --from 2026-10-01 --to 2026-10-31
& $cli --json taxes annual --year 2026
```

Si se omiten `--from` y `--to`, los reportes de ingresos, flujo e impuestos usan el mes actual. `reports monthly` usa los últimos 12 meses.

### Usuarios y permisos

```powershell
# La sesión se autentica con las credenciales globales; --email y --new-password
# pertenecen al usuario que se está creando.
& $cli --json users save `
  --name "Consulta Demo" `
  --email "consulta@demo.mx" `
  --role consulta `
  --new-password "ConsultaLibro123!"

& $cli --json users list
& $cli --json users roles
& $cli --json users reset-password --id 2 --new-password "OtraClave123!"
```

Roles:

| Rol | Lectura | Escritura |
| --- | --- | --- |
| `admin` | Todo | Todo, usuarios, configuración y respaldos. |
| `contador` | Reportes, impuestos, nómina, auditoría | Operación, facturas, nómina y ajustes. |
| `capturista` | Operación y catálogos | Movimientos, facturas, contactos, productos e inventario. |
| `consulta` | Reportes, impuestos y operación | Ninguna escritura. |

El CLI comprueba permisos antes de escribir y los servicios vuelven a comprobarlos. Un `consulta` puede leer reportes e impuestos, pero recibe un error de permiso al intentar crear movimientos, usuarios, nóminas o respaldos.

### Respaldos

```powershell
& $cli --json backup create
& $cli --json backup list
& $cli --json backup list --hash
& $cli --json backup restore --file "C:\ruta\Libro-20261025-161141.libro" --yes
```

`backup create` devuelve ruta, tamaño y SHA256. `backup restore`:

- requiere `--yes` porque reemplaza la base actual;
- verifica que el respaldo sea una base SQLCipher legible con la llave actual;
- crea un respaldo de seguridad `Libro-antes-restaurar-*.libro` antes de reemplazar;
- si la verificación falla, conserva la base actual.

No restaures un respaldo de otro usuario de Windows o de otro equipo sin la `clave.bin` correspondiente.

## 7. REPL

Sin comando, el CLI entra en modo interactivo y conserva la sesión en memoria:

```powershell
.\agent-harness\libro-cli.cmd --data-dir "$env:TEMP\libro-demo-agent"
```

Dentro:

```text
libro> auth login
Correo: admin@demo.mx
Contraseña: ********
libro> dashboard
libro> accounts list
libro> schema invoice.save
libro> exit
```

`auth setup` y `auth login` piden la contraseña sin mostrarla cuando se ejecutan en el REPL. `help` muestra la referencia y `exit` sale.

## 8. Pruebas

Las pruebas son end-to-end contra el CLI compilado y usan una carpeta temporal única. **Nunca** usan `%LOCALAPPDATA%\Libro\data`.

```powershell
cd C:\Proyectos\IDK
agent-harness\run-tests.cmd
```

o:

```powershell
cd C:\Proyectos\IDK\agent-harness
python -m unittest discover -s tests -v
```

Cobertura automatizada:

- `doctor` antes y después de inicializar, base cifrada y llave.
- `auth setup`, `login`, `status`, usuarios y roles.
- Permisos de lectura y denegación de escritura para `consulta`.
- Contactos, productos, stock, movimientos, facturas, emisión, pago y cancelación.
- Nómina: empleado, periodo, pago.
- Reportes, impuestos, panel y totales.
- Respaldos: crear, listar, restaurar, guardia `--yes` y reversión de datos.
- Errores JSON en stderr, códigos de salida y entradas inválidas.
- REPL humano y REPL JSON.
- `doctor` con base huérfana sin llave: no crea una llave nueva.

Para conservar la carpeta temporal al terminar:

```powershell
$env:LIBRO_KEEP_TEST_DATA = "1"
agent-harness\run-tests.cmd
```

El plan y el registro de resultados están en [`TEST.md`](TEST.md). Las pruebas usan `unittest` de la biblioteca estándar; si tienes `pytest`, también puede ejecutarlas:

```powershell
python -m pytest agent-harness\tests -v
```

## 9. Skill para agentes

La skill generada está en:

```text
agent-harness\skills\cli-anything-libro\SKILL.md
```

Para instalarla o actualizarla en el OpenCode de este usuario:

```powershell
$target = "$env:USERPROFILE\.config\opencode\skills\cli-anything-libro"
New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item `
  -Force `
  "C:\Proyectos\IDK\agent-harness\skills\cli-anything-libro\SKILL.md" `
  (Join-Path $target "SKILL.md")
```

Contiene la guía operativa: cuándo usar el CLI, cómo descubrir esquemas, cómo manejar centavos, permisos, respaldos y errores. En esta instalación ya existe una copia en `%USERPROFILE%\.config\opencode\skills\cli-anything-libro\SKILL.md`.

## 10. Arquitectura

```text
agent-harness/
├─ Libro.Cli/
│  ├─ Libro.Cli.csproj      # consola net8.0-windows; referencia el proyecto WPF
│  ├─ Program.cs            # entrada, REPL, manejo de errores
│  ├─ Cli.cs                # parser, contexto, JSON/tablas, credenciales
│  └─ Commands.cs           # comandos y permisos; llama servicios reales
├─ tests/
│  └─ test_cli.py           # E2E con unittest y carpeta temporal
├─ skills/
│  └─ cli-anything-libro/
│     └─ SKILL.md
├─ libro-cli.cmd            # launcher
├─ build.ps1                # compilación y pruebas opcionales
├─ run-tests.cmd            # pruebas
├─ README.md
└─ TEST.md
```

El proyecto de consola referencia `desktop\Libro.Desktop\Libro.Desktop.csproj`. No hay un segundo modelo de datos ni una copia de la lógica contable: los comandos construyen los mismos DTOs y llaman a los mismos servicios que usa la interfaz WPF.

### Método CLI-Anything aplicado

| Fase | Resultado en Libro |
| --- | --- |
| 1. Análisis | Se identificaron los servicios y DTOs reales de la app WPF, SQLCipher/DPAPI y los permisos existentes. |
| 2. Diseño | Comandos por dominio, opciones globales vs. de comando, JSON para agentes, centavos, fechas ISO y errores en stderr. |
| 3. Implementación | Consola C# que referencia el backend; sin lógica contable duplicada ni servidor HTTP. |
| 4. Pruebas | `tests/test_cli.py`: 11 casos E2E con base temporal, permisos, respaldos, errores, ayuda sin efectos y REPL. |
| 5. Documentación | Este `README.md` y `TEST.md` con plan, matriz, resultados y limitaciones. |
| 6. Skill | `skills/cli-anything-libro/SKILL.md`, generada e instalada en OpenCode. |
| 7. Instalación/validación | Launcher, build/test scripts, compilación 0/0 y ejecución 11/11 en Windows. |

Consecuencias:

- La base sigue siendo SQLCipher y la llave sigue gestionada por DPAPI.
- La migración de bases planas antiguas se conserva: el primer comando que abra una base plana la migra.
- Los permisos y la bitácora se aplican igual que en la aplicación.
- Las facturas siguen siendo internas y no CFDI.
- El CLI es Windows-only porque el backend es WPF/.NET 8/SQLCipher/DPAPI. No se añadió un servidor HTTP ni una capa web.

## 11. Limitaciones y notas

- **Windows únicamente.** No es un CLI multiplataforma.
- **No es CFDI/PAC.** No genera XML fiscal ni timbra.
- **Los importes son centavos.** No redondees a pesos antes de enviarlos.
- **La sesión no persiste entre procesos.** Usa variables de entorno, opciones globales o el REPL.
- **No ejecutes dos escrituras destructivas a la vez** (por ejemplo, restaurar un respaldo mientras la app está escribiendo). SQLite WAL permite concurrencia, pero la restauración reemplaza archivos.
- **Los respaldos dependen de DPAPI.** Un `.libro` no es portable a otra cuenta de Windows sin la llave correspondiente.
- **No edites la base directamente.** Usa el CLI o la app para respetar auditoría, stock, saldos y cifrado.
- **`doctor` no repara una llave faltante.** Si la base existe y `clave.bin` no, restaura la carpeta de datos correcta; no generes una llave nueva sobre una base cifrada existente.
- **La contraseña en `--password` puede quedar en el historial o en la lista de procesos.** Prefiere `LIBRO_PASSWORD`, `--password-env` o el REPL.

## 12. Solución de problemas

| Síntoma | Causa probable | Acción |
| --- | --- | --- |
| `No se encontro libro-cli.dll` | No se compiló Release. | Ejecuta `build.ps1`. |
| `No se encontro dotnet` | SDK/runtime no está en el `PATH`. | Usa el launcher o define `LIBRO_CLI_DOTNET`. |
| `Correo o contraseña incorrectos` | Credenciales globales ausentes o se puso `--email` después del comando. | Usa `LIBRO_EMAIL`/`LIBRO_PASSWORD` o pon las credenciales **antes** del grupo. |
| `Permiso insuficiente` | El rol no tiene esa escritura. | Usa un rol con permiso o limita la operación a lectura. |
| `Falta --input` | El comando de escritura espera un JSON. | Usa `--input` o `--stdin`; consulta `schema`. |
| `La restauración reemplaza la base actual` | Falta confirmar. | Añade `--yes` solo si estás seguro. |
| `Falta clave.bin` | Se copió la base sin la llave DPAPI. | Restaura la carpeta completa o el respaldo correcto; no crees una llave nueva. |
| `database is locked` | Otro proceso está escribiendo. | Cierra la app u otro CLI y reintenta. |
| `No hay esquema para ...` | Nombre de operación incorrecto. | Ejecuta `schema` sin argumentos para ver las claves. |

## 13. Estado de entrega

- Aplicación nativa WPF ya entregada y conservada.
- CLI compilado sin advertencias ni errores.
- Pruebas E2E: 11/11 correctas con base temporal.
- Instalador y datos reales intactos; el harness no los modifica durante las pruebas.
- Skill `cli-anything-libro` generada e instalable.
