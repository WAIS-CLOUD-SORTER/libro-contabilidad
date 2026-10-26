# 09 · Pruebas

> **Estado:** Vigente.
> **Corte:** 25 de octubre de 2026.
> **Ámbito:** autoprueba interna de la aplicación (`--self-test`), pruebas
> end-to-end del CLI (`agent-harness/tests/test_cli.py`), aislamiento de datos y
> evidencia de instalación/desinstalación.
> **Fuente de verdad:** `desktop/Libro.Desktop/SelfTest.cs`,
> `agent-harness/tests/test_cli.py`, `agent-harness/build.ps1`,
> `agent-harness/run-tests.cmd`, `agent-harness/TEST.md` y los resultados
> reproducidos en este corte.
> **No cubre:** pruebas manuales de la interfaz WPF, auditoría de seguridad
> formal ([04](04-seguridad.md)), reglas contables ([05](05-modulos-flujos.md))
> ni instalación paso a paso ([08](08-operacion-instalador.md)).

---

## 1. Resumen

Libro tiene **cuatro capas de verificación** y solo dos son automatizadas en el
repositorio:

| Capa | Qué prueba | Herramienta | Estado en este corte |
|---|---|---|---|
| Autoprueba de la app | Cifrado, llave, migración, respaldo, restauración, scrypt | `Libro.exe --self-test` | **35 comprobaciones OK**, `SELFTEST_OK` |
| E2E del CLI | Comandos reales contra una base temporal | Python `unittest` | **11/11**, `Ran 11 tests in 32.251s` (el tiempo varía) |
| Instalación/desinstalación | Setup, preservación de datos, limpieza legacy | Manual + evidencia de release | Verificado en la máquina de desarrollo; no automatizado |
| Interfaz WPF | Ventanas, diálogos, navegación, permisos visuales | Manual | No automatizado |

Resultado global del corte:

```text
Autoprueba:  35 OK, SELFTEST_OK
CLI E2E:     11/11 OK, 32.251s
Instalador:  SHA256 y tamaño verificados; ciclo de instalación documentado
Datos reales: no se usaron en ninguna prueba automatizada
```

### 1.1 Qué garantiza y qué no

**Garantiza:**

- La base se crea cifrada y se reabre con la llave correcta.
- Una base SQLite en texto plano se migra conservando las 16 tablas.
- Una llave incorrecta se rechaza.
- Un respaldo cifrado se crea, se verifica y se restaura.
- Un respaldo corrupto se rechaza sin dañar la base.
- El CLI habla con los servicios reales, no con mocks.
- Los permisos de `consulta` bloquean escrituras y lecturas administrativas.
- El contrato JSON de éxito/error se respeta en los casos cubiertos.
- El aislamiento por `--data-dir` funciona en todos los comandos de la suite.

**No garantiza:**

- Que todas las pantallas WPF funcionen.
- Que todos los comandos del CLI estén cubiertos.
- Que el instalador funcione en todas las configuraciones de Windows.
- Que DPAPI funcione entre cuentas, equipos o perfiles distintos.
- Que no haya condiciones de carrera entre la app y el CLI.
- Que una base legada con un esquema inesperado migre sin errores.
- Rendimiento, accesibilidad o localización.

---

## 2. Principios de aislamiento

### 2.1 Regla de oro

**Ninguna prueba automatizada debe tocar `%LOCALAPPDATA%\Libro\data`.**

La suite del CLI lo comprueba en `setUpClass`:

```python
cls.data_dir = Path(tempfile.mkdtemp(prefix="libro-cli-tests-"))
if cls.data_dir.resolve() == DEFAULT_DATA_DIR.resolve():
    raise RuntimeError("La carpeta de pruebas coincide con los datos reales de Libro.")
if not str(cls.data_dir.resolve()).lower().startswith(
    str(Path(tempfile.gettempdir()).resolve()).lower()
):
    raise RuntimeError(f"La carpeta de pruebas no está en TEMP: {cls.data_dir}")
```

`DEFAULT_DATA_DIR` es `%LOCALAPPDATA%\Libro\data`. La comprobación es doble:

1. la carpeta no puede ser exactamente la carpeta real;
2. la carpeta debe estar dentro de `%TEMP%`.

Además, cada invocación recibe `--data-dir <carpeta temporal>` de forma
explícita. No se depende de `LIBRO_DATA_DIR` ni de la carpeta por defecto.

### 2.2 Limpieza de variables de entorno

`setUpClass` copia el entorno y elimina:

```text
LIBRO_DATA_DIR
LIBRO_EMAIL
LIBRO_PASSWORD
```

También define:

```text
DOTNET_CLI_TELEMETRY_OPTOUT=1
DOTNET_NOLOGO=1
```

Así se evita que una variable del usuario cambie la carpeta de datos, las
credenciales o la salida de .NET durante la prueba.

`LIBRO_KEEP_TEST_DATA=1` se lee al final para conservar la carpeta. No se elimina
del entorno copiado; es intencional para poder inspeccionar el resultado.

### 2.3 Limpieza de la carpeta

`tearDownClass` ejecuta:

```python
if os.environ.get("LIBRO_KEEP_TEST_DATA") == "1":
    print(f"Se conserva la carpeta de pruebas: {cls.data_dir}", file=sys.stderr)
    return
shutil.rmtree(cls.data_dir, ignore_errors=True)
```

- Por defecto, la carpeta se borra al terminar.
- `ignore_errors=True` evita que un archivo bloqueado haga fallar la suite
  después de las pruebas.
- Con `LIBRO_KEEP_TEST_DATA=1`, la ruta se imprime en stderr para inspección.

### 2.4 Aislamiento de la autoprueba

`SelfTest` también usa solo directorios temporales:

```csharp
var path = Path.Combine(Path.GetTempPath(),
    "libro-test-" + Guid.NewGuid().ToString("N"));
```

Cada grupo crea su propio directorio, define `LIBRO_DATA_DIR` en el proceso y
lo elimina al terminar. No abre la carpeta real.

### 2.5 Qué nunca se debe hacer

- Ejecutar `python agent-harness/tests/test_cli.py` con `LIBRO_DATA_DIR`
  apuntando a datos reales.
- Quitar el `--data-dir` de `base`.
- Reutilizar una carpeta de pruebas para una demo con datos reales.
- Copiar `contabilidad.db` real a la carpeta temporal.
- Ejecutar `backup restore` contra la carpeta real.
- Probar `--self-test` con `LIBRO_DATA_DIR` real: no lo usa, pero la costumbre
  es peligrosa.

---

## 3. Cómo ejecutar

### 3.1 Requisitos

| Requisito | Versión / ruta |
|---|---|
| Windows | 10/11 x64. Verificado en Windows 11 IoT Enterprise LTSC 10.0.26100. |
| .NET SDK | 8.0.426 en la máquina de desarrollo. |
| Python | 3.12.10; se recomienda 3.10 o posterior. |
| CLI compilado | `agent-harness/Libro.Cli/bin/Release/net8.0-windows/libro-cli.dll`. |
| `dotnet` | `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe` o `PATH`. |
| Inno Setup | Solo para el instalador, no para las pruebas E2E. |

`pytest` **no está instalado** en la máquina de desarrollo. Las pruebas usan
`unittest` de la biblioteca estándar. El código es compatible con pytest en
cuanto a descubrimiento y aserciones, pero la suite depende del orden (véase
§12), así que ejecutarla con pytest seleccionando un solo test puede fallar.

### 3.2 Compilar y probar

```powershell
cd C:\Proyectos\IDK
powershell -ExecutionPolicy Bypass -File agent-harness\build.ps1 -Test
```

`build.ps1 -Test`:

1. resuelve `dotnet`;
2. compila `Libro.Cli.csproj` en Release;
3. ejecuta `python -m unittest discover -s agent-harness\tests -v`;
4. devuelve el código de salida de `unittest`.

Salida esperada:

```text
Compilando Libro CLI...
...
Listo: ...\agent-harness\libro-cli.cmd
Ejecutando pruebas end-to-end...
...
Ran 11 tests in 32.251s

OK
```

### 3.3 Solo probar (sin recompilar)

```powershell
cmd /c agent-harness\run-tests.cmd
```

El wrapper:

- define `PYTHONUTF8=1`;
- comprueba que exista `libro-cli.dll`;
- ejecuta `python -m unittest discover -s <harness>\tests -v`;
- propaga el código de salida.

Si falta la DLL, muestra:

```text
ERROR: No existe la compilacion Release.
Ejecuta: powershell -ExecutionPolicy Bypass -File "...\build.ps1"
```

### 3.4 Ejecución directa

```powershell
python -m unittest discover -s agent-harness\tests -v
```

o:

```powershell
python agent-harness\tests\test_cli.py -v
```

### 3.5 Conservar los datos de prueba

```powershell
$env:LIBRO_KEEP_TEST_DATA = "1"
python -m unittest discover -s agent-harness\tests -v
```

Al terminar, la ruta se imprime en stderr:

```text
Datos temporales de prueba: C:\Users\...\Temp\libro-cli-tests-xxxxxxxx
Se conserva la carpeta de pruebas: C:\Users\...\Temp\libro-cli-tests-xxxxxxxx
```

Úsala solo para inspección. No la conviertas en producción.

### 3.6 Limpiar y recompilar

```powershell
powershell -ExecutionPolicy Bypass -File agent-harness\build.ps1 -Clean -Test
```

`-Clean` ejecuta `dotnet clean` antes de compilar.

---

## 4. Arquitectura de la suite E2E

### 4.1 Archivo y clase

| Elemento | Valor |
|---|---|
| Archivo | `agent-harness/tests/test_cli.py` |
| Clase | `LibroCliTests(unittest.TestCase)` |
| Métodos | 11 (`test_00` … `test_09`, incluido `test_04b`) |
| Framework | `unittest` |
| Proceso | Un proceso `dotnet libro-cli.dll` por comando |
| Base | SQLCipher real en `%TEMP%\libro-cli-tests-*` |
| Mock | Ninguno; se usan los servicios reales de `Libro.Desktop` |
| Timeout por comando | 120 s |

### 4.2 Constantes

| Constante | Valor | Uso |
|---|---|---|
| `HARNESS` | `agent-harness/` | Raíz del harness. |
| `DLL` | `Libro.Cli/bin/Release/net8.0-windows/libro-cli.dll` | Ejecutable bajo prueba. |
| `DOTNET` | `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe` o `PATH` | Intérprete. |
| `ADMIN_EMAIL` | `admin@libro.test` | Administrador sintético. |
| `ADMIN_PASSWORD` | `AdminLibro123!` | Contraseña sintética. |
| `CONSULTA_EMAIL` | `consulta@libro.test` | Usuario de solo consulta. |
| `CONSULTA_PASSWORD` | `ConsultaLibro123!` | Contraseña inicial. |
| `CONSULTA_PASSWORD_2` | `ConsultaLibro456!` | Contraseña tras reset. |
| `DEFAULT_DATA_DIR` | `%LOCALAPPDATA%\Libro\data` | Ruta prohibida. |

Las credenciales son de prueba y solo existen en la base temporal. No son las
credenciales reales de ninguna instalación.

### 4.3 `setUpClass`

1. Verifica que exista la DLL; si no, lanza `SkipTest` con la instrucción de
   compilación.
2. Crea la carpeta temporal.
3. Comprueba que no sea la carpeta real y que esté en `%TEMP%`.
4. Copia el entorno y elimina las variables `LIBRO_*`.
5. Define `DOTNET_CLI_TELEMETRY_OPTOUT` y `DOTNET_NOLOGO`.
6. Construye las listas de argumentos:
   - `base = ["--data-dir", <tmp>, "--json"]`;
   - `admin = [*base, "--email", ADMIN_EMAIL, "--password", ADMIN_PASSWORD]`;
   - `human_admin` = igual, pero sin `--json`.
7. Inicializa los atributos de estado (`contact_id`, `product_id`, etc.).
8. Escribe un JSON inválido para las pruebas de error.
9. Imprime la carpeta temporal en stderr.

### 4.4 Helpers

#### `_run(args, expect=0, input_text=None)`

- Construye `[DOTNET, DLL, *args]`.
- `capture_output=True`, `text=True`, `encoding="utf-8"`, `errors="replace"`.
- `timeout=120`.
- Pasa el entorno limpio.
- Si `input_text` es `None`, usa `stdin=DEVNULL`; así un comando que
  accidentalmente pida datos no se queda esperando.
- Si el código de salida no es `expect`, falla con stdout, stderr y la ruta de
  datos.
- `_display` enmascara los valores de `--password` y `--new-password`.

#### `_json_ok(args, input_text=None)`

- Ejecuta con código esperado 0.
- Parsea `stdout` como JSON.
- Si no es JSON, falla mostrando stdout/stderr.

#### `_json_error(args, contains=None, input_text=None)`

- Ejecuta con código esperado 1.
- Exige `stdout` vacío.
- Parsea `stderr` como JSON.
- Exige `ok == False` y `error` no vacío.
- Si `contains`, exige que el mensaje contenga esa subcadena sin distinguir
  mayúsculas.

#### `_write_json(name, value)`

- Escribe un archivo UTF-8 en la carpeta temporal.
- Se usa para `--input`.

#### `_month_range()`

- Calcula el primer y último día del mes actual.
- Evita hardcodear fechas y hace que las pruebas sigan funcionando en otro mes.
- Riesgo bajo: si la suite cruza la medianoche del último día del mes, una
  factura creada con `date.today()` podría caer en el mes siguiente mientras el
  rango se calculó antes. Es poco probable y no se ha observado.

### 4.5 Estado compartido y orden

La suite **no es independiente**. `setUpClass` crea una sola base y los métodos
comparten estado:

| Método | Depende de | Deja para el siguiente |
|---|---|---|
| `test_00` | Nada | Verifica que la ayuda no crea base. |
| `test_01` | `test_00` no debe crear base | Confirma base/llave ausentes. |
| `test_02` | `test_01` | Crea admin, empresa, base y llave. |
| `test_03` | `test_02` | IDs de cuenta y categoría. |
| `test_04` | `test_03` | Contacto, producto, factura, empleado, nómina, respaldo. |
| `test_04b` | `test_04` | Producto activo. |
| `test_05` | `test_04` (`tx_path`) y admin | Usuario `consulta`, contraseña nueva. |
| `test_06` | `test_04`/`test_05` | Restaura la base y deja solo el contacto original. |
| `test_07` | `test_02` y admin | Errores esperados. |
| `test_08` | `test_06` (contacto original) y admin | Contacto `Repl Path`. |
| `test_09` | `test_06` (base restaurada) | Copia huérfana sin llave. |

`unittest` ordena los métodos lexicográficamente por nombre. Por eso los
nombres `test_00`, `test_01`, …, `test_04b`, `test_05` funcionan en ese orden.
**Ejecutar un método aislado puede fallar** por falta de datos previos. Es una
decisión consciente para mantener la suite rápida y lineal, no una suite de
fixtures independientes. Véase TST-01.

### 4.6 Qué se considera éxito

- Código de salida 0 en cada comando esperado.
- JSON válido en stdout.
- Para errores: código 1, stdout vacío, JSON en stderr con `ok=false`.
- Valores concretos: totales, estados, folios, stock, conteos, permisos.
- Ausencia de `passwordHash` en la salida de usuarios.
- Que `--help` no cambie el estado.

No se valida el esquema JSON completo con un JSON Schema, ni se comparan todos
los campos. Se validan los campos relevantes para cada caso.

---

## 5. Catálogo prueba por prueba

### 5.1 `test_00_help_and_version`

**Objetivo:** verificar ayuda, versión y esquema sin ejecutar operaciones.

Pasos:

1. `help` (humano) y comprueba que contiene “Libro CLI” y “transactions”.
2. `--help` global y comprueba “Reglas:”.
3. `accounts --help` y comprueba “accounts list|balances”.
4. `invoices pay --help` y comprueba “--amount-cents”.
5. `--json --help` y comprueba que `help` contiene “Libro CLI”.
6. `version` y comprueba `cliVersion` y `appVersion`.
7. `schema transaction.save` y comprueba `subtotalCents` y `categoryId`.

Lo que prueba:

- La ayuda global, de grupo y de acción responde.
- `--help` se intercepta antes de abrir la base.
- `version` no requiere base.
- `schema` devuelve el ejemplo esperado.

Lo que no prueba:

- Que `--help` de un grupo desconocido devuelva 1.
- Que la ayuda de acción sea específica (no lo es; reutiliza el grupo).
- Que `--help` no cree archivos en una instalación nueva (se infiere por
  `test_01`).

### 5.2 `test_01_doctor_before_setup`

**Objetivo:** verificar el estado inicial de una carpeta vacía.

Pasos:

1. `doctor --json`.

Asserts:

- `databaseExists == False`.
- `keyExists == False`.
- `databaseKind == "missing"`.
- `opened == False`.

Lo que prueba:

- `doctor` no crea base ni llave.
- El diagnóstico distingue “no existe” de “existe pero no se puede abrir”.

Dependencia implícita: si `test_00` hubiera creado la base, este test fallaría.
Es la evidencia de que `help`, `version` y `schema` no inicializan datos.

### 5.3 `test_02_setup_and_auth`

**Objetivo:** alta inicial, login explícito, auto-login y estado de la base.

Pasos:

1. `auth setup --company "Libro CLI Test" --name "Admin de Prueba"
   --email admin@libro.test --password AdminLibro123!`.
2. `auth status` sin credenciales.
3. `auth login` con credenciales.
4. `auth status` con credenciales globales (auto-login).
5. `doctor` con credenciales.

Asserts:

- `setup.authenticated == True`.
- `setup.user.email == ADMIN_EMAIL`.
- `setup.user.role == "admin"`.
- `setup.user.lastLoginAt` no vacío.
- `setup.company == "Libro CLI Test"`.
- `status` anónimo: `authenticated == False`.
- `login`: autenticado, correo correcto, `lastLoginAt`.
- `status` autenticado: `lastLoginAt`.
- `doctor.opened == True`.
- `doctor.databaseKind == "encrypted"`.
- `doctor.keyExists == True`.
- `doctor.counts.users == 1`.
- `doctor.counts.accounts >= 2`.
- `doctor.counts.categories >= 1`.

Lo que prueba:

- `auth setup` crea admin y empresa.
- El login actualiza `last_login_at`.
- El auto-login global funciona.
- La base resultante es cifrada y tiene semillas.

### 5.4 `test_03_seed_data`

**Objetivo:** verificar los catálogos iniciales.

Pasos:

1. `accounts list`.
2. `categories list`.
3. `contacts list`.

Asserts:

- Al menos 2 cuentas.
- Al menos una cuenta activa; guarda su `id`.
- Al menos una categoría de gasto no archivada; guarda su `id`.
- Contactos vacío (`[]`).

Lo que prueba:

- Las semillas `Caja` y `Banco` existen.
- Las categorías por defecto existen.
- Una base nueva no tiene contactos.

### 5.5 `test_04_write_workflow`

**Objetivo:** recorrer el flujo principal de escritura y consulta.

Pasos y asserts:

| Paso | Comando | Asserts |
|---|---|---|
| Contacto | `contacts save --input contact.json` | `name == "Cliente Demo"`, guarda `id`. |
| Producto | `products save --input product.json` | `sku == "SERV-001"`, `stock == 10`, guarda `id`. |
| Gasto | `transactions save --input transaction.json` | `totalCents == 11600`, descripción. |
| Factura | `invoices save --input invoice.json --emit` | `status == "emitida"`, `totalCents == 116000`, `balanceCents == 116000`, `displayFolio` empieza con `A-`. |
| Pago | `invoices pay --amount-cents 116000` | `status == "pagada"`, `paidCents == 116000`, `balanceCents == 0`. |
| Detalle | `invoices show` | 1 ítem, 1 pago, importe 116000. |
| Entrada inventario | `inventory movement --qty 5` | stock 14. |
| Producto | `products show` | stock 14, al menos 2 movimientos. |
| Inventario | `inventory summary` | `productCount >= 1`, `valueCents >= 1`. |
| Empleado | `employees save` | nombre, guarda `id`. |
| Nómina | `payroll create --type mensual` | `status == "borrador"`, `employeeCount >= 1`, `totalNetCents > 0`. |
| Nómina detalle | `payroll show` | al menos 1 ítem. |
| Pago nómina | `payroll pay` | `status == "pagada"`, `transactionId > 0`. |
| Resumen nómina | `payroll summary` | `paidCount >= 1`. |
| Ingresos | `reports income` | `invoicedRevenueCents >= 100000`, gastos > 0, `expensesByCategory`. |
| Flujo | `reports cashflow` | `netCents` presente, `monthly` no vacío. |
| Antigüedad | `reports aging` | `buckets` y `invoices`. |
| Mensual | `reports monthly` | no vacío. |
| Saldos | `reports balances` | al menos 1. |
| Impuestos | `taxes summary` | `invoiceCount >= 1`, `invoiceIvaCents >= 1600`, `incomeRows`. |
| Anual | `taxes annual` | al menos 1 fila. |
| Panel | `dashboard` | `inventoryCents >= 1`, `recentInvoices`. |
| Facturas | `invoices overview` | `monthCount >= 1`. |
| Movimientos | `transactions list` | `total >= 1`, `rows >= 1`. |
| Totales | `transactions totals` | `count >= 1`. |

Lo que prueba:

- El flujo completo contacto → producto → gasto → factura → pago → inventario
  → nómina → reportes funciona con la base temporal.
- Los importes en centavos y el IVA se calculan.
- La factura emitida obtiene folio y saldo.
- El pago total deja saldo 0 y estado `pagada`.
- La entrada de inventario con cuenta genera el movimiento y actualiza stock.
- La nómina calcula y paga con un gasto vinculado.
- Los reportes e impuestos leen los datos creados.

Lo que no prueba:

- Cancelación, borrado de factura, borrado de pago.
- Edición de factura, transacción o producto.
- Ajustes de inventario y salidas.
- Nómina quincenal, recálculo, edición de conceptos, reversión, borrado.
- Casos límite de IVA 0/8/16.
- Conteos exactos de categorías o cuentas.

### 5.6 `test_04b_help_is_side_effect_free`

**Objetivo:** regresión de `--help` después de un comando.

Pasos:

1. `products show --id <producto>` y comprueba `active == 1`.
2. `products toggle --id <producto> --help` sin credenciales.
3. `products show` de nuevo.

Asserts:

- La ayuda contiene “products list|low-stock”.
- `active` sigue en 1.
- El `id` no cambió.

Lo que prueba:

- `--help` no ejecuta `toggle`.
- No requiere credenciales ni abre la base.
- El producto no se desactiva por accidente.

Este test se añadió tras corregir el bug que ejecutaba la operación al pasar
`--help` después de una acción.

### 5.7 `test_05_permissions_and_users`

**Objetivo:** verificar roles, permisos y manejo de contraseñas.

Pasos:

1. Admin crea usuario `consulta`.
2. Admin lista usuarios.
3. Admin consulta roles.
4. `consulta` hace `auth status`.
5. `consulta` lee reportes, impuestos y productos.
6. `consulta` intenta escribir transacción, listar usuarios, listar nómina,
   crear respaldo y guardar contacto.
7. Admin resetea la contraseña de `consulta`.
8. `consulta` inicia sesión con la nueva contraseña.
9. Admin intenta crear usuario sin `--new-password`.

Asserts:

- Usuario creado con correo y rol `consulta`.
- Lista de usuarios >= 2.
- La serialización no contiene `passwordHash` ni `password_hash`.
- Roles incluyen `admin` y `consulta`.
- `consulta` autenticado con rol `consulta`.
- `consulta` puede leer `reports income`, `taxes summary` y `products list`.
- Las cinco operaciones denegadas devuelven error con “permiso”.
- Reset devuelve `changed == True`.
- Login con la contraseña nueva funciona.
- Crear usuario sin contraseña devuelve error con “new-password”.

Lo que prueba:

- La matriz de permisos de `consulta` bloquea escrituras y administración.
- `consulta` **sí** puede leer productos y reportes, aunque su descripción diga
  “solo reportes e impuestos”. Es un comportamiento real, no un fallo de la
  prueba.
- El hash de contraseña nunca se serializa.
- `users save` exige `--new-password` al crear.

Lo que no prueba:

- Roles `contador` y `capturista`.
- `users activate/deactivate/summary`.
- `auth change-password`.
- Que un admin no pueda quedarse sin administradores.

### 5.8 `test_06_backup_restore`

**Objetivo:** respaldo, guardia de restauración y restauración real.

Pasos:

1. `backup create`.
2. `backup list`.
3. Crear contacto marcador.
4. Listar contactos (2).
5. Intentar `backup restore` sin `--yes`.
6. Restaurar con `--yes`.
7. Listar contactos.
8. `doctor`.

Asserts:

- El archivo del respaldo existe.
- `sha256` tiene 64 caracteres.
- `backup list` incluye la ruta.
- Sin `--yes`, error que contiene `--yes`; no restaura.
- Con `--yes`, `restoredFrom` coincide y `safetyBackup` existe.
- Después de restaurar, solo queda “Cliente Demo”; el marcador desaparece.
- `doctor.opened == True`, `contacts == 1`, `backupCount >= 2`.

Lo que prueba:

- El respaldo se crea cifrado y con hash.
- La guardia de `--yes` evita una restauración accidental.
- La restauración reemplaza la base y crea un respaldo de seguridad.
- La base restaurada sigue siendo utilizable.

Lo que no prueba:

- Respaldo con `--output` externo.
- `backup list --hash`.
- Restauración de un archivo corrupto (lo cubre `SelfTest`).
- Restauración con la app abierta o concurrencia.
- Archivos `.db` legados.

### 5.9 `test_07_json_errors`

**Objetivo:** verificar errores JSON y códigos de salida.

Casos:

| Caso | Comando | Asserts |
|---|---|---|
| Sin entrada | `contacts save` | Error contiene `--input`. |
| JSON inválido | `contacts save --input invalid.json` | Error contiene `json`. |
| Esquema inexistente | `schema no-existe` | Error contiene `esquema`. |
| Movimiento inexistente | `transactions show --id 999999` | Error contiene `no existe`. |
| Login incorrecto | `auth login` con contraseña mala | Error contiene `incorrect`. |
| Setup repetido | `auth setup` con otro admin | Error contiene `administrador`. |
| Respaldo inexistente | `backup restore --file no-existe.libro --yes` | Error contiene `no`. |

Además, `_json_error` exige:

- código de salida 1;
- stdout vacío;
- stderr JSON;
- `ok == False`;
- `error` no vacío.

Lo que prueba:

- El contrato de errores se respeta en los casos cubiertos.
- No se mezcla salida de éxito con errores.

Lo que no prueba:

- Todos los mensajes de error.
- `--debug` y el campo `detail`.
- Códigos distintos de 1.
- Errores de E/S, permisos de archivo o disco lleno.

**Debilidad:** el caso del respaldo usa `contains="no"`, una subcadena muy
común. Podría pasar con un mensaje distinto que también contenga “no”. Véase
TST-08.

### 5.10 `test_08_repl`

**Objetivo:** verificar el modo interactivo y el tokenizer de rutas Windows.

Pasos:

1. Crea `repl-contact.json` en la carpeta temporal.
2. Ejecuta la REPL humana con:

   ```text
   contacts save --input "<ruta con backslashes>"
   contacts list
   exit
   ```

3. Ejecuta la REPL JSON con:

   ```text
   version
   exit
   ```

4. Ejecuta la REPL humana con:

   ```text
   auth status
   exit
   ```

Asserts:

- La salida humana contiene “modo interactivo”.
- Contiene “Cliente Demo” (contacto previo).
- Contiene “Repl Path” (contacto creado por la ruta entre comillas).
- Contiene “libro” en minúsculas.
- La REPL JSON produce un JSON parseable con `cliVersion`.
- `auth status` humano contiene “Sesión activa”.

Lo que prueba:

- La REPL conserva los backslashes de una ruta Windows entre comillas.
- La REPL humana y JSON funcionan.
- La sesión de admin se mantiene en memoria.

Lo que no prueba:

- Prompts de correo/contraseña.
- `auth logout` y re-login.
- Cambio de `--data-dir` dentro de la REPL (debe fallar).
- Comandos multilínea, historial o autocompletado.
- Errores que no terminan la REPL.

### 5.11 `test_09_doctor_orphan_database`

**Objetivo:** verificar que `doctor` no crea una llave nueva cuando falta.

Pasos:

1. Crea `orphan/`.
2. Copia `contabilidad.db` a `orphan/contabilidad.db`.
3. Comprueba que no hay `clave.bin`.
4. Ejecuta `doctor --data-dir orphan --json`.
5. Comprueba que sigue sin haber `clave.bin`.

Asserts:

- `databaseExists == True`.
- `keyExists == False`.
- `opened == False`.
- `note` contiene `clave.bin`.
- No se creó `clave.bin`.

Lo que prueba:

- `doctor` diagnostica una base huérfana sin inutilizarla.
- No genera una llave nueva que haría imposible abrir la base original.

Lo que no prueba:

- El arranque de la app en esa misma condición (la app sí crea una llave nueva
  y luego falla; es un hallazgo de [04](04-seguridad.md)).
- La recuperación con la llave correcta.

---

## 6. Matriz de cobertura del CLI

### 6.1 Por grupo

| Grupo | Cubierto | No cubierto |
|---|---|---|
| `help`, `version`, `schema`, `doctor` | Global, grupo, acción, JSON, esquema, estado inicial y huérfano. | Grupo desconocido, `--debug`, `schema` sin argumento. |
| `auth` | `setup`, `login`, `status`, auto-login. | `logout`, `change-password`, prompts REPL. |
| `company` | Solo el nombre devuelto por `auth setup`. | `show`, `update`, validación. |
| `dashboard` | Mes actual. | Fechas distintas, datos vacíos. |
| `accounts` | `list`. | `balances`, `save`, `archive`. |
| `categories` | `list`. | `save`, archivado. |
| `contacts` | `list`, `save`. | `show`, `stats`, `archive`. |
| `products` | `list`, `save`, `show`; `toggle --help`. | `low-stock`, `summary`, `toggle` real. |
| `transactions` | `list`, `save`, `totals`. | `show` (solo error), `delete`, edición. |
| `invoices` | `save --emit`, `show`, `pay`, `overview`. | `list`, `cancel`, `delete`, `delete-payment`, edición. |
| `inventory` | `movement` (entrada), `summary`; movimientos vía `products show`. | `list`, `movements` directo, salida, ajuste. |
| `employees` | `save`. | `list`, `show`, `summary`, `toggle`. |
| `payroll` | `create`, `show`, `pay`, `summary`. | `list`, `recalc`, `update-item`, `delete-item`, `revert`, `delete`. |
| `taxes` | `summary`, `annual`. | Fechas fuera del mes, IVA 0/8. |
| `reports` | `income`, `cashflow`, `aging`, `monthly`, `balances`. | Datos vacíos, periodos largos. |
| `users` | `save` (crear), `list`, `roles`, `reset-password`. | `summary`, `activate`, `deactivate`, edición. |
| `audit` | Ninguno. | `list`, filtros, `--limit`. |
| `backup` | `create`, `list`, `restore` con guardia. | `--output`, `list --hash`, corrupción. |
| REPL | Ruta citada, `version`, `auth status`, `exit`. | Prompts, `logout`, cambio de `--data-dir`, errores. |
| Entrada JSON | `--input` con archivo. | `--stdin`, precedencia, `--`. |
| Salida humana | Subcadenas en REPL. | Tablas, importes, truncado, colores. |

### 6.2 Por rol

| Rol | Probado |
|---|---|
| `admin` | Flujo completo, backup, usuarios. |
| `consulta` | Lecturas permitidas y escrituras/administración denegadas. |
| `contador` | No probado. |
| `capturista` | No probado. |
| Sin sesión | `help`, `version`, `schema`, `doctor`, `auth status`, `auth setup`, `auth login`. |

### 6.3 Por tipo de operación

| Tipo | Cobertura |
|---|---|
| Lectura | Alta en reportes, impuestos, panel, listados. |
| Escritura | Alta en el flujo principal; baja en edición/archivado/cancelación. |
| Dinero | Centavos, IVA 16 %, totales, pagos, nómina. |
| Fechas | Fechas actuales y rango del mes; no hay fechas históricas. |
| Permisos | `consulta` versus `admin`; faltan otros roles. |
| Errores | JSON, código 1, stdout vacío; faltan casos de E/S y concurrencia. |
| Cifrado | SelfTest, no E2E (el CLI no expone la llave). |
| Migración | SelfTest, no E2E. |
| Instalador | Manual, no automatizado. |

---

## 7. Contratos verificados

### 7.1 Salida

| Contrato | Verificado por |
|---|---|
| Éxito JSON en stdout | `_json_ok` en todos los comandos. |
| Error JSON en stderr | `_json_error` en 7 casos. |
| stdout vacío en error | `_json_error`. |
| Código 0 en éxito | `_run(expect=0)`. |
| Código 1 en error | `_run(expect=1)`. |
| UTF-8 | `encoding="utf-8"` y datos con acentos. |
| Sin `passwordHash` | `test_05`. |
| Folio local `A-` | `test_04`. |
| Importes en centavos | `test_04` (11600, 116000, 100000, 800000). |
| Fechas ISO | `test_04` (`date.today().isoformat()`). |

### 7.2 Reglas de negocio

| Regla | Verificado por |
|---|---|
| IVA 16 % en gasto | `totalCents == 11600` para subtotal 10000. |
| IVA 16 % en factura | `totalCents == 116000` para subtotal 100000. |
| Pago total → `pagada` | `test_04`. |
| Saldo 0 tras pago total | `test_04`. |
| Stock inicial y entrada | 10 → 14. |
| Movimiento de inventario con cuenta | `inventory movement` y `products show`. |
| Nómina genera gasto | `transactionId > 0`. |
| Respaldo restaura y elimina el marcador | `test_06`. |
| Guardia `--yes` | `test_06`. |
| Permisos de `consulta` | `test_05`. |

### 7.3 Lo que no se verifica

- Que todos los campos JSON tengan el tipo esperado.
- Que los mensajes de error sean estables.
- Que `--debug` agregue `detail`.
- Que `--pretty`, `--no-color`, `--quiet` o `--all` hagan algo (no lo hacen).
- Que las opciones desconocidas fallen (no fallan).
- Que `--stdin` funcione.
- Que `--password-env` funcione.
- Que `--` literal funcione.
- Que un valor que empieza con `--` se pueda pasar sin `=`.
- Que el bloqueo de login acumule entre procesos (no acumula).

---

## 8. Datos de prueba

### 8.1 Entidades creadas

| Entidad | Valores |
|---|---|
| Empresa | `Libro CLI Test` |
| Admin | `Admin de Prueba`, `admin@libro.test`, rol `admin`. |
| Consulta | `Consulta Demo`, `consulta@libro.test`, rol `consulta`. |
| Contacto | `Cliente Demo`, RFC `XAXX010101000`, correo `cliente@demo.mx`. |
| Producto | `Servicio Demo`, SKU `SERV-001`, precio 100000, costo 20000, IVA 16 %, stock inicial 10. |
| Gasto | Renta de prueba, subtotal 10000, IVA 16 %, total 11600. |
| Factura | 1 línea de 100000 + IVA, total 116000, folio `A-*`. |
| Pago | 116000, cuenta activa, método Transferencia. |
| Movimiento | Entrada de 5, costo 20000, IVA 16 %, cuenta activa. |
| Empleado | `Empleado Demo`, salario 800000, inicio 1 de enero del año actual. |
| Nómina | Mes actual, tipo mensual, pagada. |
| Respaldo | `Libro-*.libro` en la carpeta temporal. |

### 8.2 Características

- **Sintéticos:** nombres, correos y RFC de ejemplo. No hay PII real.
- **Fechas dinámicas:** mes y año actuales; la suite no caduca.
- **Importes enteros:** todos en centavos.
- **Una sola base:** todas las entidades viven en la misma base temporal.
- **Sin secretos reales:** las contraseñas son de prueba y se enmascaran en los
  mensajes de error.
- **Backup incluido:** el respaldo contiene solo datos de prueba.

### 8.3 Rutas

```text
%TEMP%\libro-cli-tests-<aleatorio>\
├── contabilidad.db
├── clave.bin
├── respaldos\
│   ├── Libro-*.libro
│   └── Libro-antes-restaurar-*.libro
├── libro.log
├── contact.json
├── product.json
├── transaction.json
├── invoice.json
├── movement.json
├── employee.json
├── marker.json
├── repl-contact.json
├── denied-contact.json
└── invalid.json
```

La carpeta se elimina al terminar salvo `LIBRO_KEEP_TEST_DATA=1`.

---

## 9. Resultados del corte

### 9.1 Suite E2E

Comando:

```powershell
cmd /c agent-harness\run-tests.cmd
```

Salida real:

```text
Datos temporales de prueba: %TEMP%\libro-cli-tests-0vwa7dhm
test_00_help_and_version ... ok
test_01_doctor_before_setup ... ok
test_02_setup_and_auth ... ok
test_03_seed_data ... ok
test_04_write_workflow ... ok
test_04b_help_is_side_effect_free ... ok
test_05_permissions_and_users ... ok
test_06_backup_restore ... ok
test_07_json_errors ... ok
test_08_repl ... ok
test_09_doctor_orphan_database ... ok

----------------------------------------------------------------------
Ran 11 tests in 32.251s

OK
```

### 9.2 Entorno

| Elemento | Valor |
|---|---|
| Sistema | Microsoft Windows 11 IoT Enterprise LTSC |
| Versión | 10.0.26100 |
| Python | 3.12.10 |
| .NET SDK | 8.0.426 |
| Runtime | .NET 8 |
| CLI | Release `net8.0-windows` |
| Framework de prueba | `unittest` |
| `pytest` | No instalado |
| Carpeta de datos | `%TEMP%\libro-cli-tests-*` |
| Datos reales | No usados |

### 9.3 Autoprueba de la app

Comando:

```powershell
$env:LIBRO_SELFTEST_LOG = "$env:TEMP\libro-selftest.log"
& "installer\native-app\Libro.exe" --self-test
Get-Content $env:LIBRO_SELFTEST_LOG -Encoding utf8
```

Resultado:

```text
EXIT=0
35 comprobaciones OK
SELFTEST_OK
```

La autoprueba se ejecutó sobre la publicación `installer/native-app` del corte.

### 9.4 Instalador

| Comprobación | Resultado |
|---|---|
| SHA256 | `A7B78EE1B22405E696A5B7ADD764DD2E9C5E5F5C04641ACA5E10D790AAFDB525` |
| Tamaño | 50,513,464 bytes |
| Publicación | 265 archivos, 155,754,703 bytes |
| `*.db` en publicación | 0 |
| Autoprueba | `SELFTEST_OK` |
| Ciclo de instalación | Verificado en la máquina de desarrollo; no automatizado |
| Desinstalación conserva datos | Verificado; no automatizado |

El detalle del instalador está en [08](08-operacion-instalador.md).

### 9.5 Nota sobre conteos anteriores

Documentos y notas de una revisión anterior mencionan **10/10** pruebas. El
corte actual tiene **11/11** porque se añadió
`test_04b_help_is_side_effect_free`. Los conteos de `docs/README.md`,
`agent-harness/README.md` y `agent-harness/TEST.md` ya se actualizaron a 11/11.

---

## 10. Autoprueba interna en detalle

### 10.1 Grupos y comprobaciones

La autoprueba escribe una línea `OK <nombre>` por comprobación. En el corte se
registraron 35:

| Grupo | Comprobaciones |
|---|---|
| scrypt | Vector conocido; rechazo de contraseña incorrecta. |
| Base nueva | Empresa insertada; usuario y scrypt; archivo de llave; base cifrada; reapertura con llave. |
| Migración | Conserva datos; usuarios y contraseñas; 16 tablas (`company`, `users`, `sessions`, `audit_log`, `accounts`, `categories`, `contacts`, `products`, `transactions`, `inventory_movements`, `invoices`, `invoice_items`, `payments`, `employees`, `payroll_periods`, `payroll_items`); importes; queda cifrada; no queda copia plana. |
| Llave incorrecta | Rechazo. |
| Respaldo | Creado; cifrado; corrupto rechazado; base intacta tras rechazo; restauración conserva datos; restauración queda cifrada. |

### 10.2 Garantías

- No usa la base real.
- No requiere credenciales.
- No abre ventanas.
- No depende de la red.
- Deja la carpeta temporal limpia.
- Falla en la primera comprobación incorrecta y escribe `SELFTEST_FAIL` con la
  excepción.

### 10.3 Límites

- El código de salida es 0 incluso en fallo; hay que leer el log.
- No prueba la interfaz.
- No prueba DPAPI entre cuentas.
- No prueba el instalador.
- No prueba concurrencia.
- No prueba un esquema legado con columnas extra o faltantes.
- No prueba una base real grande.

---

## 11. Pruebas manuales y de instalación

### 11.1 Checklist de instalación

- [ ] Verificar SHA256 y tamaño del setup.
- [ ] Instalar en una VM limpia Windows 10/11 x64.
- [ ] Confirmar que no pide administrador.
- [ ] Confirmar la ruta `%LOCALAPPDATA%\Programs\Libro`.
- [ ] Confirmar que no hay Node/Next.js.
- [ ] Abrir Libro; completar `SetupWindow`.
- [ ] Verificar `contabilidad.db`, `clave.bin`, `respaldos\`, `libro.log`.
- [ ] Crear un respaldo y verificar que aparece.
- [ ] Cerrar, desinstalar.
- [ ] Confirmar que `data` sigue intacta.
- [ ] Reinstalar, iniciar sesión y verificar datos.
- [ ] Confirmar que los accesos directos y el menú Inicio funcionan.

### 11.2 Checklist de actualización desde 1.0.0

- [ ] Copiar una base legada de prueba (nunca la real) a una VM.
- [ ] Instalar 1.0.0 y crear datos.
- [ ] Crear respaldo legado.
- [ ] Copiar `%LOCALAPPDATA%\Libro\data` completa.
- [ ] Ejecutar 2.0.0.
- [ ] Verificar que el servidor Node se detiene.
- [ ] Verificar que `.next`, `node_modules`, `node.exe`, `server.js` se eliminan.
- [ ] Abrir la app nativa.
- [ ] Verificar la migración y los conteos.
- [ ] Verificar que los respaldos `.db` legados siguen ahí pero no se listan.
- [ ] Crear un respaldo `.libro`.

### 11.3 Evidencia existente

`RELEASE-NOTES-2.0.0.md` documenta:

- migración de la base real de la máquina de desarrollo conservando empresa y
  administrador;
- instalación silenciosa sobre la versión anterior con hashes de datos y
  `clave.bin` iguales antes/después;
- eliminación de archivos legacy;
- desinstalación sin bloqueo, con `contabilidad.db` y `clave.bin` intactos.

No se repitió el ciclo completo en esta sesión porque la máquina no tiene una
instalación de Libro (`NO INSTALADO`). La evidencia se cita como histórica, no
como una ejecución nueva.

---

## 12. Brechas y riesgos de prueba

| ID | Severidad | Brecha | Consecuencia | Recomendación |
|---|---|---|---|---|
| TST-01 | Media | La suite depende del orden y comparte estado. | Un test aislado falla; el orden alfabético es implícito. | Refactorizar a fixtures independientes o documentar el orden explícitamente. |
| TST-02 | Media | No hay `setUp`/`tearDown` por test. | Un fallo temprano contamina los siguientes. | Aislar datos por test o usar transacciones. |
| TST-03 | Alta | No hay pruebas automatizadas de la UI WPF. | Regresiones visuales y de navegación no se detectan. | Añadir UI Automation o pruebas de humo con capturas. |
| TST-04 | Media | No hay pruebas automatizadas del instalador. | Un cambio en Inno puede romper la instalación sin que la suite falle. | Automatizar en una VM: instalar, abrir, desinstalar, verificar datos. |
| TST-05 | Media | No hay pruebas de concurrencia app/CLI. | `database is locked` o pérdida de escrituras pueden pasar inadvertidos. | Prueba de dos procesos escribiendo y de restauración concurrente. |
| TST-06 | Baja | No se mide cobertura de código. | No se sabe qué porcentaje de servicios se ejecuta. | Integrar cobertura de .NET o al menos un mapa de comandos. |
| TST-07 | Media | Muchos comandos no tienen prueba feliz. | Errores en `cancel`, `delete-payment`, `recalc`, `update-item`, `audit`, etc. | Añadir casos por comando y por regla. |
| TST-08 | Baja | `test_07` usa `contains="no"`, una subcadena ambigua. | Podría pasar con un mensaje incorrecto. | Usar una subcadena más específica (“no existe”). |
| TST-09 | Baja | La prueba de que `--help` no crea base es implícita (`test_01`). | Si se reordena, se pierde la evidencia. | Añadir una comprobación explícita de ausencia de `contabilidad.db`. |
| TST-10 | Baja | No se prueban `--stdin`, `--password-env` ni `--`. | Parsing no cubierto. | Añadir casos de entrada y opciones. |
| TST-11 | Baja | No se prueba `auth logout` con re-auto-login. | Comportamiento contraintuitivo no verificado. | Añadir caso. |
| TST-12 | Media | No se prueba una migración de esquema real (solo la plana del SelfTest). | Una base con columnas extra/faltantes puede fallar. | Probar bases legadas con variantes de esquema. |
| TST-13 | Media | No se prueba DPAPI en otra cuenta/equipo. | La portabilidad de respaldos no está verificada. | Probar en una cuenta distinta y documentar el resultado. |
| TST-14 | Baja | No hay pruebas de rendimiento ni de bases grandes. | Reportes lentos o bloqueos de UI no se detectan. | Medir con 10k–100k movimientos. |
| TST-15 | Baja | No hay pruebas de accesibilidad. | La UI puede tener barreras no detectadas. | Revisar teclado, foco, lectores de pantalla. |
| TST-16 | Baja | `pytest` no está instalado. | Solo se ejecuta con `unittest`. | Mantener compatibilidad o declarar `unittest` como oficial. |
| TST-17 | Baja | La suite usa la fecha actual. | Un cambio de mes a medianoche podría alterar rangos. | Fijar una fecha de prueba inyectable. |
| TST-18 | Baja | No se prueba el bloqueo de login entre procesos. | El contador estático no acumula; no hay evidencia automatizada. | Documentar y probar la REPL. |

---

## 13. Mantenimiento de las pruebas

### 13.1 Añadir una prueba E2E

1. Añade un método `test_NN_descripcion` en `LibroCliTests`.
2. Usa `self.base` o `self.admin`; no escribas rutas reales.
3. Usa `self._json_ok` para éxito y `self._json_error` para error.
4. Escribe entradas con `self._write_json`.
5. Usa fechas dinámicas (`date.today()`, `_month_range`).
6. No dependas de una base existente salvo que el test sea de orden posterior.
7. Actualiza la matriz de cobertura y los conteos.
8. Ejecuta `build.ps1 -Test`.

### 13.2 Añadir una comprobación al SelfTest

1. Añade un método privado en `SelfTest`.
2. Créalo con `NewTempDirectory()`.
3. Define `LIBRO_DATA_DIR` al directorio temporal.
4. Usa `Check(condición, "nombre")`.
5. Limpia el directorio al final.
6. Añade la llamada en `Run()` antes de `SELFTEST_OK`.
7. Compila y ejecuta `--self-test`.
8. Verifica que el log tenga `SELFTEST_OK` y las nuevas líneas `OK`.

### 13.3 Actualizar documentación

Cuando cambie el número o el resultado de las pruebas, actualiza:

- `docs/README.md` (§7, resultados);
- `docs/09-pruebas.md` (este documento);
- `agent-harness/README.md`;
- `agent-harness/TEST.md`;
- `README.md` de la raíz si menciona conteos.

### 13.4 Reglas para no romper el aislamiento

- No quites `--data-dir`.
- No uses `%LOCALAPPDATA%\Libro\data`.
- No copies datos reales a `%TEMP%`.
- No añadas una prueba que dependa de una credencial real.
- No desactives la comprobación de ruta.
- No dejes procesos `libro-cli` colgados.
- No ejecutes `backup restore` contra la carpeta real.

### 13.5 Comando de verificación final

```powershell
powershell -ExecutionPolicy Bypass -File agent-harness\build.ps1 -Test
```

Debe terminar con:

```text
0 Advertencia(s)
0 Errores
Ran 11 tests in ...
OK
```

Si el número cambió, actualiza los conteos. No publiques un resultado de prueba
sin la salida real.

---

## 14. Documentos relacionados

- [02 · Arquitectura](02-arquitectura.md): proyectos y flujo de compilación.
- [03 · Modelo de datos](03-modelo-datos.md): tablas que la autoprueba migra.
- [04 · Seguridad](04-seguridad.md): DPAPI, SQLCipher, permisos y amenazas.
- [05 · Módulos y flujos](05-modulos-flujos.md): reglas que las pruebas ejercitan.
- [06 · Mapa de UI](06-mapa-ui.md): lo que no está automatizado.
- [07 · CLI](07-cli.md): comandos y contrato JSON.
- [08 · Operación e instalador](08-operacion-instalador.md): verificación manual.
- [10 · Inventario de archivos](10-inventario-archivos.md): ubicación de cada
  prueba.
- [13 · Hallazgos y riesgos](13-hallazgos-riesgos.md): registro consolidado.
- [14 · Decisiones](14-decisiones.md): por qué `unittest` y por qué no hay mocks.

---

## 15. Resumen para QA

1. Ejecuta `cmd /c agent-harness\run-tests.cmd` o
   `build.ps1 -Test`.
2. Espera **11/11** y `OK`.
3. Verifica que la carpeta de prueba esté en `%TEMP%` y se elimine.
4. No uses datos reales; no quites `--data-dir`.
5. La autoprueba de la app debe terminar en `SELFTEST_OK`; no confíes solo en
   el código de salida.
6. La UI y el instalador se prueban a mano; no están automatizados.
7. Si añades un comando, añade su prueba y actualiza la matriz.
8. Si un test falla, revisa stdout/stderr y la ruta temporal impresa.
9. `pytest` no es el runner oficial; usa `unittest`.
10. No publiques un instalador sin haber ejecutado la autoprueba y las pruebas
    E2E.
