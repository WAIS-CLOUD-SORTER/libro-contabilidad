# Plan y registro de pruebas — Libro CLI

## 1. Alcance

Validar que el harness CLI:

- use el backend real de Libro (servicios C#/SQLCipher/DPAPI);
- permita operar la contabilidad desde agentes con JSON estable;
- respete roles y permisos;
- cubra los flujos principales de negocio;
- no toque la base real del usuario durante las pruebas;
- conserve las restricciones del producto: Windows nativo, sin Node/HTTP/WebView, sin CFDI/PAC, facturas internas.

Fuera de alcance de esta entrega:

- cambiar la aplicación WPF o el instalador;
- generar CFDI, timbrar o integrar un PAC;
- probar restauración de respaldos en otra cuenta de Windows (dependencia DPAPI);
- pruebas de carga, concurrencia intensiva o recuperación ante fallos de disco.

## 2. Regla de seguridad de datos

Todas las pruebas automatizadas crean una carpeta única con `tempfile.mkdtemp(prefix="libro-cli-tests-")` y pasan `--data-dir` en **cada** invocación.

El propio test verifica que:

1. la carpeta esté dentro de `%TEMP%`;
2. no sea `%LOCALAPPDATA%\Libro\data`;
3. no se usen variables `LIBRO_DATA_DIR`, `LIBRO_EMAIL` o `LIBRO_PASSWORD` heredadas del entorno.

La prueba de `doctor` con base huérfana copia la base a una subcarpeta temporal y comprueba que **no** se cree una `clave.bin` nueva.

## 3. Entorno de ejecución

| Elemento | Valor |
| --- | --- |
| Sistema | Windows (x64) |
| Aplicación | Libro 2.0.0, WPF/.NET 8 |
| CLI | `agent-harness\Libro.Cli`, `net8.0-windows` |
| SDK usado | `%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe` (8.0.426) |
| Runtime | .NET 8.0.32 |
| Python | 3.12.10 |
| Framework de pruebas | `unittest` (biblioteca estándar) |
| Datos | Carpeta temporal, SQLCipher + DPAPI reales |
| Fecha de ejecución | 2026-10-25 |

`pytest` no está instalado en este entorno. Las pruebas se ejecutan con `unittest`; la clase también es compatible con `pytest` si se instala.

## 4. Estrategia

### 4.1 Pruebas automatizadas

Un solo archivo, `tests/test_cli.py`, con 11 métodos ordenados. Cada método usa el CLI compilado como proceso real, no mocks:

- el proceso arranca .NET y carga los servicios reales;
- la base se cifra con SQLCipher y la llave se protege con DPAPI;
- se validan códigos de salida, stdout JSON, stderr JSON y efectos en la base;
- se usan fechas del mes actual para que los reportes sean deterministas;
- los IDs de cuentas y categorías se descubren en ejecución, no se codifican.

### 4.2 Pruebas manuales/exploratorias

La sección 10 lista comandos secundarios que no tienen un caso automatizado dedicado. Se recomienda ejecutarlos en una carpeta temporal antes de una entrega que los use.

### 4.3 Criterios de aceptación

- `build.ps1` compila con 0 errores y 0 advertencias.
- Las 11 pruebas automatizadas pasan.
- El CLI devuelve JSON válido y códigos de salida correctos.
- `consulta` no puede escribir.
- La restauración de respaldo revierte datos y exige `--yes`.
- Los errores no escriben datos de negocio en stdout.
- Ninguna prueba apunta a `%LOCALAPPDATA%\Libro\data`.

## 5. Matriz de casos

### 5.1 Entorno y diagnóstico

| ID | Caso | Precondición / pasos | Resultado esperado | Automatizado |
| --- | --- | --- | --- | --- |
| ENV-01 | Compilación Release | `dotnet build ... -c Release` | 0 errores, 0 advertencias, `libro-cli.dll` generado | Sí (script de build) |
| ENV-02 | `doctor` antes de inicializar | Carpeta temporal vacía; `doctor` | `databaseExists=false`, `keyExists=false`, `databaseKind=missing`, `opened=false`; no crea archivos | Sí, `test_01` |
| ENV-03 | `doctor` con base cifrada | Tras `auth setup`; `doctor` con credenciales | `databaseKind=encrypted`, `keyExists=true`, `opened=true`, conteos | Sí, `test_02` |
| ENV-04 | Base huérfana sin llave | Copiar `contabilidad.db` a carpeta sin `clave.bin`; `doctor` | Reporta `keyExists=false`, `opened=false`, nota sobre `clave.bin`; no crea llave | Sí, `test_09` |
| ENV-05 | Versión, ayuda y esquema | `version`, `help`, `schema transaction.save` | JSON/texto válido; el esquema contiene campos esperados | Sí, `test_00` |

### 5.2 Autenticación y usuarios

| ID | Caso | Pasos | Resultado esperado | Automatizado |
| --- | --- | --- | --- | --- |
| AUTH-01 | Crear administrador inicial | `auth setup` con empresa, nombre, correo y contraseña | Usuario admin, empresa creada, sesión activa | Sí, `test_02` |
| AUTH-02 | Login explícito | `auth login --email ... --password ...` | Sesión activa; usuario y empresa | Sí, `test_02` |
| AUTH-03 | Status sin credenciales | `auth status` en proceso nuevo | `authenticated=false`; no inventa sesión | Sí, `test_02` |
| AUTH-04 | Status autenticado | Credenciales globales; `auth status` | `authenticated=true`; usuario, rol y `lastLoginAt` | Sí, `test_02`/`test_05` |
| AUTH-05 | Credenciales incorrectas | `auth login` con contraseña errónea | Error JSON en stderr, salida 1 | Sí, `test_07` |
| AUTH-06 | Segundo setup | `auth setup` cuando ya hay usuarios | Error “Ya existe un administrador”; no modifica datos | Sí, `test_07` |
| USER-01 | Crear usuario `consulta` | `users save` con `--new-password`; credenciales admin globales | Usuario creado; el `--email` del comando no reemplaza la sesión admin | Sí, `test_05` |
| USER-02 | Listar usuarios | `users list` | No aparece `passwordHash` ni `password_hash` | Sí, `test_05` |
| USER-03 | Roles | `users roles` | Incluye `admin` y `consulta` con descripción | Sí, `test_05` |
| USER-04 | Reset de contraseña | `users reset-password --new-password`; login con la nueva | Login correcto | Sí, `test_05` |
| USER-05 | Validación de contraseña nueva | `users save` sin `--new-password` | Error que menciona `--new-password` | Sí, `test_05` |

### 5.3 Permisos

| ID | Caso | Pasos | Resultado esperado | Automatizado |
| --- | --- | --- | --- | --- |
| PERM-01 | Lectura de reportes e impuestos | Login `consulta`; `reports income`, `taxes summary` | Permitido; datos del periodo | Sí, `test_05` |
| PERM-02 | Lectura de catálogos/operación | `consulta`; `products list` | Permitido | Sí, `test_05` |
| PERM-03 | Escritura de movimientos | `consulta`; `transactions save` | Error de permiso `tx.write`, salida 1 | Sí, `test_05` |
| PERM-04 | Administración de usuarios | `consulta`; `users list` | Error de permiso `users.manage` | Sí, `test_05` |
| PERM-05 | Nómina | `consulta`; `payroll list` | Error de permiso `payroll.view` | Sí, `test_05` |
| PERM-06 | Respaldos | `consulta`; `backup create` | Error de permiso `backup.manage` | Sí, `test_05` |
| PERM-07 | Contactos | `consulta`; `contacts save` | Error de permiso `contacts.write` | Sí, `test_05` |

### 5.4 Operación contable

| ID | Caso | Pasos | Resultado esperado | Automatizado |
| --- | --- | --- | --- | --- |
| DATA-01 | Contacto | `contacts save` con JSON; `contacts list` | Contacto creado con ID, RFC y datos | Sí, `test_04` |
| DATA-02 | Producto y stock inicial | `products save` con `initialStock=10` | Stock 10; precio/costo/IVA correctos | Sí, `test_04` |
| DATA-03 | Movimiento de gasto | `transactions save` con categoría/cuenta; totales | Subtotal 10000, IVA 1600, total 11600 | Sí, `test_04` |
| DATA-04 | Factura interna | `invoices save --emit` con una línea | Folio `A-0001`, total 116000, saldo 116000 | Sí, `test_04` |
| DATA-05 | Pago de factura | `invoices pay --amount-cents 116000` | Estado `pagada`, pagado 116000, saldo 0 | Sí, `test_04` |
| DATA-06 | Detalle de factura | `invoices show` | Un concepto y un pago; importes correctos | Sí, `test_04` |
| DATA-07 | Movimiento de inventario | `inventory movement` entrada de 5 | Stock pasa de 9 a 14; gasto asociado | Sí, `test_04` |
| DATA-08 | Detalle de producto | `products show` | Stock 14 y al menos dos movimientos | Sí, `test_04` |
| DATA-09 | Resumen de inventario | `inventory summary` | Productos ≥ 1, valor ≥ 1 | Sí, `test_04` |
| DATA-10 | Total de movimientos | `transactions list`, `transactions totals` | Página y conteo ≥ 1 | Sí, `test_04` |
| DATA-11 | Resumen de facturas | `invoices overview` | Facturas del mes ≥ 1 | Sí, `test_04` |

### 5.5 Nómina

| ID | Caso | Pasos | Resultado esperado | Automatizado |
| --- | --- | --- | --- | --- |
| PAY-01 | Empleado | `employees save` con sueldo y fecha de inicio | Empleado activo creado | Sí, `test_04` |
| PAY-02 | Periodo | `payroll create` mes actual | Estado `borrador`, empleados ≥ 1, neto > 0 | Sí, `test_04` |
| PAY-03 | Detalle | `payroll show` | Al menos un concepto de nómina | Sí, `test_04` |
| PAY-04 | Pago | `payroll pay` con cuenta y fecha | Estado `pagada`, `transactionId` > 0 | Sí, `test_04` |
| PAY-05 | Resumen | `payroll summary` | `paidCount` ≥ 1 | Sí, `test_04` |

### 5.6 Reportes e impuestos

| ID | Caso | Pasos | Resultado esperado | Automatizado |
| --- | --- | --- | --- | --- |
| REP-01 | Estado de resultados | `reports income` con rango del mes | Ingresos ≥ 100000, gastos > 0, categorías | Sí, `test_04` |
| REP-02 | Flujo de efectivo | `reports cashflow` | `netCents` y filas mensuales | Sí, `test_04` |
| REP-03 | Antigüedad | `reports aging` | `buckets` e `invoices` | Sí, `test_04` |
| REP-04 | Mensual | `reports monthly` | Lista de meses | Sí, `test_04` |
| REP-05 | Saldos | `reports balances` | Lista de cuentas | Sí, `test_04` |
| TAX-01 | Resumen de IVA | `taxes summary` con rango | Facturas ≥ 1, IVA facturado ≥ 1600, filas | Sí, `test_04` |
| TAX-02 | Anual | `taxes annual --year actual` | 12 filas (o al menos 1) | Sí, `test_04` |

### 5.7 Respaldos

| ID | Caso | Pasos | Resultado esperado | Automatizado |
| --- | --- | --- | --- | --- |
| BAK-01 | Crear respaldo | `backup create` | Archivo `.libro`, SHA256 de 64 hex | Sí, `test_06` |
| BAK-02 | Listar respaldos | `backup list` | Incluye el respaldo recién creado | Sí, `test_06` |
| BAK-03 | Guardia de restauración | `backup restore` sin `--yes` | Error; no reemplaza la base | Sí, `test_06` |
| BAK-04 | Restaurar | Crear marcador, restaurar con `--yes` | Marcador desaparece; respaldo de seguridad existe | Sí, `test_06` |
| BAK-05 | Diagnóstico posterior | `doctor` | Base abierta; contactos vuelven a 1; respaldos ≥ 2 | Sí, `test_06` |
| BAK-06 | Respaldo inexistente | `backup restore --file no-existe --yes` | Error; base actual intacta | Sí, `test_07` |

### 5.8 Errores, JSON y REPL

| ID | Caso | Pasos | Resultado esperado | Automatizado |
| --- | --- | --- | --- | --- |
| ERR-01 | Falta `--input` | `contacts save` sin entrada | Error JSON en stderr; stdout vacío; salida 1 | Sí, `test_07` |
| ERR-02 | JSON inválido | Archivo con JSON roto | Error “JSON inválido”; salida 1 | Sí, `test_07` |
| ERR-03 | Esquema desconocido | `schema no-existe` | Error con claves disponibles | Sí, `test_07` |
| ERR-04 | Recurso inexistente | `transactions show --id 999999` | Error “No existe”; salida 1 | Sí, `test_07` |
| REPL-01 | REPL humano | `contacts save --input "C:\...\repl-contact.json"`, `contacts list` y `exit` por stdin | Banner, prompt, ruta con backslashes intacta y contacto visible | Sí, `test_08` |
| REPL-02 | REPL JSON | `version` y `exit` con `--json` | Un objeto JSON válido, sin banner humano | Sí, `test_08` |
| REPL-03 | Status en REPL | `auth status` con sesión inicial | “Sesión activa” | Sí, `test_08` |

## 6. Datos de prueba

Se usan datos ficticios:

| Dato | Valor |
| --- | --- |
| Empresa | `Libro CLI Test` |
| Admin | `admin@libro.test` / `AdminLibro123!` |
| Consulta | `consulta@libro.test` / `ConsultaLibro123!` |
| Consulta tras reset | `ConsultaLibro456!` |
| Contacto | `Cliente Demo`, RFC `XAXX010101000` |
| Producto | `Servicio Demo`, SKU `SERV-001`, precio `100000` centavos |
| Factura | 1 × `100000` + IVA 16% = `116000` centavos |
| Nómina | Sueldo `800000` centavos, periodo mensual |

Los IDs de cuenta y categoría se obtienen de `accounts list` y `categories list`; no se asumen IDs fijos.

## 7. Ejecución

Compilar:

```powershell
cd C:\Proyectos\IDK
powershell -ExecutionPolicy Bypass -File agent-harness\build.ps1
```

Pruebas:

```powershell
cd C:\Proyectos\IDK
agent-harness\run-tests.cmd
```

o:

```powershell
cd C:\Proyectos\IDK\agent-harness
python -m unittest discover -s tests -v
```

Conservar datos temporales:

```powershell
$env:LIBRO_KEEP_TEST_DATA = "1"
python -m unittest discover -s tests -v
```

## 8. Resultado de la ejecución final

### 8.1 Compilación

```text
Libro.Desktop -> ...\desktop\Libro.Desktop\bin\Release\net8.0-windows\Libro.dll
Libro.Cli -> ...\agent-harness\Libro.Cli\bin\Release\net8.0-windows\libro-cli.dll

Compilación correcta.
    0 Advertencia(s)
    0 Errores
```

### 8.2 Pruebas

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

La carpeta temporal se elimina al terminar salvo que `LIBRO_KEEP_TEST_DATA=1` esté definido. También se validó el wrapper `run-tests.cmd` con el mismo resultado 11/11.

### 8.3 Evidencia funcional adicional

- El launcher `libro-cli.cmd` ejecutó `auth status` contra la base temporal de humo y devolvió JSON válido con `lastLoginAt`.
- `doctor` sobre la base temporal reportó `databaseKind=encrypted`, `opened=true` y conteos.
- La restauración de respaldo se probó también manualmente con un contacto marcador: desapareció tras restaurar y se creó `Libro-antes-restaurar-*.libro`.
- Un intento de restauración sin `--yes` devolvió error y no reemplazó la base.

## 9. Trazabilidad con los requisitos

| Requisito | Evidencia |
| --- | --- |
| Usar servicios reales, no reimplementar contabilidad | `Libro.Cli.csproj` referencia `Libro.Desktop`; `Commands.cs` llama servicios; tests E2E. |
| JSON para agentes | `test_00`, `test_04`–`test_07`; errores JSON en stderr. |
| REPL | `test_08`; `Program.RunRepl`. |
| Tests con carpeta temporal | `setUpClass` con `tempfile`; guardas de ruta; `test_09`. |
| Permisos/roles | `test_05`; matriz de `Permissions`. |
| Facturas internas, no CFDI | Modelo y UI existentes; CLI solo usa `InvoiceService`; no hay XML/PAC. |
| SQLCipher/DPAPI | Base real cifrada y `clave.bin`; `doctor`; restauración con la misma llave. |
| Windows nativo | `net8.0-windows`, referencia WPF, sin Node/HTTP/WebView. |
| Instalador/datos reales intactos | No se recompiló el instalador ni se abrió la carpeta real; tests usan TEMP. |
| Skill generada | `agent-harness/skills/cli-anything-libro/SKILL.md`; copia instalada en `%USERPROFILE%\.config\opencode\skills\cli-anything-libro\SKILL.md`. |

## 10. Cobertura manual pendiente / comandos secundarios

Estos comandos existen y comparten los mismos servicios, pero no tienen un caso automatizado dedicado en esta entrega. Antes de usarlos en producción, conviene una prueba exploratoria en una carpeta temporal:

- `company update` y `company show` (actualización de empresa).
- `accounts save`, `accounts archive`, `accounts balances`.
- `categories save`.
- `contacts show`, `contacts stats`, `contacts archive`.
- `products toggle`, `products low-stock`.
- `transactions show`, `transactions delete`.
- `invoices cancel`, `invoices delete`, `invoices delete-payment`.
- `employees show`, `employees summary`, `employees toggle`.
- `payroll recalc`, `payroll update-item`, `payroll delete-item`, `payroll revert`, `payroll delete`.
- `users activate`, `users deactivate`, `users summary`.
- `audit list`.
- `auth logout`, `auth change-password`.
- `backup list --hash`.

Riesgos conocidos:

- **CFDI/SAT:** no implementado por diseño; no debe prometerse a un usuario.
- **DPAPI:** un respaldo no es portable a otra cuenta de Windows sin la llave correcta.
- **Concurrencia:** no se probaron restauraciones mientras la app WPF está escribiendo; evitar operaciones destructivas simultáneas.
- **Instalador:** no se re-ejecutó en esta sesión; el harness no modifica el instalador ni los datos reales.
- **Interfaz WPF:** no se modificó; el CLI referencia el mismo backend. No se hizo una regresión visual de la app en esta sesión.

## 11. Criterios de salida

- [x] Compilación Release sin errores ni advertencias.
- [x] 11/11 pruebas E2E.
- [x] Cobertura de backup/restore, permisos, errores JSON y REPL.
- [x] Launcher, README, TEST.md y skill generada.
- [x] Datos de prueba en carpeta temporal y eliminados al terminar.
- [x] Sin cambios en la base real ni en el instalador.
- [ ] Regresión visual de la app WPF (fuera del alcance del harness).
- [ ] Pruebas de concurrencia/estrés (fuera del alcance).
