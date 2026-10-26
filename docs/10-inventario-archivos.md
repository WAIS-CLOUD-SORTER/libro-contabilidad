# 10 · Inventario de archivos

> **Estado:** Vigente.
> **Corte:** 25 de octubre de 2026.
> **Ámbito:** todos los archivos del repositorio `C:\Proyectos\IDK`, clasificados
> por estado (vigente, legado, generado, datos), con tamaño, líneas y propósito.
> **Fuente de verdad:** el árbol de archivos del corte, los `.csproj`, los
> scripts de construcción, `docs/` y los artefactos de `dist/`.
> **No cubre:** el detalle funcional de cada módulo ([05](05-modulos-flujos.md)),
> el contrato del CLI ([07](07-cli.md)), el instalador paso a paso
> ([08](08-operacion-instalador.md)) ni la historia del legado
> ([11](11-legado-nextjs.md)).

---

## 1. Resumen

El repositorio contiene **dos productos en el mismo árbol**: la aplicación
nativa vigente (WPF + CLI) y la aplicación anterior de Next.js, archivada. Junto
a ellos conviven artefactos de construcción, datos locales y evidencia generada
por Graphify.

| Área | Archivos | Bytes | Estado | ¿Se distribuye? |
|---|---:|---:|---|---|
| `desktop/Libro.Desktop/` (fuente, sin `bin/obj`) | 100 | 766,067 | **Vigente** | Compilado dentro de `Libro.exe` |
| `agent-harness/` (fuente, sin `bin/obj`) | 11 | 197,052 | **Vigente** | No |
| `installer/` (scripts e icono, sin staging) | 8 | 43,828 | **Vigente + legado** | Solo el setup compilado |
| `installer/native-app/` | 265 | 155,754,703 | **Generado** | Empaquetado en el setup |
| `src/` | 68 | 477,101 | **Legado** | No |
| `docs/` | 16 | ≈670,900 | **Vigente** | No |
| `dist/` | 5 | 86,982,927 | **Artefactos** | Solo el setup 2.0.0 |
| `tools/Libro.DbTool/` (fuente) | 2 | 2,488 | **Soporte** | No |
| `scripts/` | 1 | 760 | **Legado** | No |
| Raíz (texto versionable) | 8 | 77,513 | **Mixto** | No |
| `graphify-out/` | ≈800 | ≈35 MB | **Generado** | No |
| `.next/` | 2,326 | 299,544,407 | **Generado (legado)** | No |
| `node_modules/` | 14,658 | 406,296,114 | **Generado (legado)** | No |
| `desktop/**/bin`, `obj` | 1,119 | 246,484,945 | **Generado** | No |
| `agent-harness/**/bin`, `obj` | 59 | 35,201,570 | **Generado** | No |
| `tools/**/bin`, `obj` | 59 | 34,769,987 | **Generado** | No |
| `data/` | 4 | 4,464,096 | **Datos reales (legado)** | **Nunca** |
| `backups/` | 2 | 156,116 | **Datos reales (legado)** | **Nunca** |

> Los conteos de `bin/obj`, `.next`, `node_modules` y `graphify-out` cambian
> con cada compilación o actualización del grafo. Los tamaños de fuente, `src/`
> y artefactos son los del corte; `docs/` y `graphify-out/` son aproximados
> porque cambian al editar o regenerar.

### 1.1 Totales útiles

| Métrica | Valor |
|---|---:|
| Archivos fuente versionables (sin generados ni datos) | 214 |
| Líneas de la app nativa | 14,826 |
| Líneas del harness CLI (código, pruebas y documentación) | 4,379 |
| Líneas del legado Next.js | 12,863 |
| Líneas de README + documentos 01–09 | 8,609 |
| Archivos publicados para el instalador | 265 |
| Setup vigente | 1 archivo, 50,513,464 bytes |

“Versionable” significa “que debería estar en control de versiones”. En este
corte **no hay repositorio Git inicializado**; la clasificación es conceptual.

---

## 2. Cómo leer este inventario

### 2.1 Estados

| Estado | Significado | Se puede borrar |
|---|---|---|
| **Vigente** | Código o documento que describe el producto 2.0.0. | No sin reemplazo. |
| **Soporte** | Herramienta de diagnóstico que no se instala. | Sí, con pérdida de capacidad de diagnóstico. |
| **Legado** | Código de la versión 1.0.0 (Next.js) o de etapas intermedias. | Solo si se decide abandonar la referencia histórica. |
| **Generado** | Salida de compilación, publicación o Graphify. | Sí; se reconstruye. |
| **Datos** | Base, llave, respaldos o bitácora de una instalación. | **No**. Contiene información del negocio. |
| **Artefacto** | Instalador compilado y su hash. | Solo archivando una copia. |

### 2.2 Advertencias

- `data/` y `backups/` en la raíz **no** son la carpeta de datos de la app
  nativa. Son restos de la app legada y están en **texto plano**. La app nativa
  usa `%LOCALAPPDATA%\Libro\data` (o `LIBRO_DATA_DIR`).
- `src/`, `package.json`, `next.config.ts`, `postcss.config.mjs`,
  `tsconfig.json`, `next-env.d.ts`, `scripts/` y `installer/libro.iss` son
  legado. No se ejecutan para operar Libro 2.0.0.
- `installer/native-app/` y `dist/` están en `.gitignore`; se regeneran.
- `graphify-out/` está en `.gitignore`; es evidencia derivada, no fuente.
- Las líneas de las tablas de este inventario son **no vacías**; los bytes son
  el tamaño en disco. El total puede diferir de un conteo que incluya líneas en
  blanco.
- Ningún archivo de este inventario contiene la contraseña real del
  administrador ni la llave DPAPI (`clave.bin` no está en el repositorio).

---

## 3. Árbol de primer nivel

```text
C:\Proyectos\IDK\
├── .gitignore                     reglas de exclusión
├── README.md                      documentación de producto
├── RELEASE-NOTES-2.0.0.md         notas de la versión 2.0.0
├── package.json                   LEGADO Next.js
├── package-lock.json              LEGADO (lockfile npm)
├── next.config.ts                 LEGADO
├── postcss.config.mjs             LEGADO
├── tsconfig.json                  LEGADO
├── next-env.d.ts                  LEGADO (generado por Next)
├── tsconfig.tsbuildinfo           GENERADO (TypeScript)
├── agent-harness/                 CLI para agentes + pruebas
├── backups/                       DATOS legados en texto plano
├── data/                          DATOS legados en texto plano
├── desktop/                       aplicación WPF vigente
├── dist/                          instaladores compilados
├── docs/                          documentación curada
├── graphify-out/                  GENERADO (grafo, wiki, reportes)
├── installer/                     instalador vigente + legado + staging
├── node_modules/                  GENERADO (dependencias Next.js)
├── scripts/                       LEGADO (limpieza del standalone)
├── src/                           LEGADO Next.js
├── tools/                         herramienta de diagnóstico
└── .next/                         GENERADO (build Next.js)
```

No hay `.sln`, `Directory.Build.props`, `global.json`, `NuGet.config`,
`.editorconfig`, pipelines de CI ni contenedores. Cada proyecto .NET se compila
por separado.

---

## 4. Archivos de la raíz

| Archivo | Líneas | Bytes | Estado | Propósito |
|---|---:|---:|---|---|
| `.gitignore` | 20 | 245 | Vigente | Excluye generados, datos, `dist/`, `backups/` y `graphify-out/`. |
| `README.md` | 168 | 11,643 | Vigente | Presentación, instalación, compilación, estructura, CLI y legado. |
| `RELEASE-NOTES-2.0.0.md` | 88 | 6,115 | Histórico | Evidencia del ciclo de migración, instalación y desinstalación. |
| `package.json` | 29 | 734 | **Legado** | Manifiesto Next.js 15.5.28 / React 19.1.1. |
| `package-lock.json` | 1,801 | 57,772 | **Legado** | Árbol de dependencias npm. |
| `next.config.ts` | 10 | 334 | **Legado** | `output: "standalone"` para el instalador 1.0.0. |
| `postcss.config.mjs` | 6 | 94 | **Legado** | Tailwind 4 vía PostCSS. |
| `tsconfig.json` | 23 | 576 | **Legado** | Configuración TypeScript de Next.js. |
| `next-env.d.ts` | 5 | 268 | **Legado/generado** | Tipos de Next.js. |
| `tsconfig.tsbuildinfo` | 1 | 111,289 | **Generado** | Caché incremental de TypeScript. |

### 4.1 Lo que la raíz no tiene

- No hay solución `.sln`; `desktop`, `agent-harness` y `tools` se compilan con
  `dotnet build <csproj>`.
- No hay `global.json`, así que se usa el SDK .NET 8 disponible.
- No hay `LICENSE`, `CONTRIBUTING.md`, `CHANGELOG.md` ni `.env.example`.
- No hay configuración de firma de código.
- No hay repositorio Git; `.gitignore` existe, pero no hay `.git/`.

---

## 5. Aplicación nativa `desktop/Libro.Desktop/`

**100 archivos, 14,826 líneas, 766,067 bytes.** Es el corazón del producto
2.0.0. Todo el código es C# / XAML sobre `net8.0-windows`.

### 5.1 Proyecto y arranque

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `Libro.Desktop.csproj` | 23 | 1,024 | `WinExe`, WPF, .NET 8, versión 2.0.0, icono, Dapper, SQLCipher, DPAPI. |
| `App.xaml` | 22 | 1,168 | Recursos globales, convertidores y estilos. |
| `App.xaml.cs` | 111 | 4,013 | Arranque, `--self-test`, modo `--dev`, setup/login, cierre y errores. |
| `AssemblyInfo.cs` | 9 | 643 | Metadatos WPF (tema). |
| `MainWindow.xaml` | 67 | 4,030 | Shell: barra lateral, cabecera, contenido, usuario y cierre de sesión. |
| `MainWindow.xaml.cs` | 107 | 4,067 | Navegación por permisos, refresco de vistas y logout. |
| `SelfTest.cs` | 294 | 13,438 | Autoprueba de cifrado, migración, respaldos y scrypt. |

### 5.2 Acceso a datos

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `Data/AppPaths.cs` | 25 | 915 | Resuelve `LIBRO_DATA_DIR` o `%LOCALAPPDATA%\Libro\data`; base, respaldos y llave. |
| `Data/Database.cs` | 470 | 18,551 | Conexión SQLCipher, esquema, semillas, migración de texto plano, respaldo/restauración. |
| `Data/DpapiKeyStore.cs` | 48 | 2,272 | Genera/lee la llave de 32 bytes protegida con DPAPI `CurrentUser`. |
| `Data/Models.cs` | 296 | 11,765 | Entidades y DTO de todas las tablas y reportes. |
| `Data/Schema.cs` | 228 | 9,136 | DDL de las 16 tablas, índices, `CHECK`, claves foráneas y semillas. |
| `Data/Scrypt.cs` | 176 | 7,503 | Hash y verificación scrypt con sal, parámetros y formato almacenado. |

### 5.3 Servicios de dominio

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `Services/AppState.cs` | 44 | 1,480 | Estado global: `Db`, `Company`, `CurrentUser`, `Initialize`, `SignOut`, `Can`. |
| `Services/AuthService.cs` | 115 | 5,572 | Alta inicial, login, cambio de contraseña, bloqueo y auditoría. |
| `Services/CompanyService.cs` | 124 | 5,881 | Empresa, catálogos, cuentas, categorías y saldos; usado por UI y CLI. |
| `Services/ContactService.cs` | 158 | 7,303 | Contactos: filtros, alta/edición, archivo y estadísticas. |
| `Services/DashboardService.cs` | 157 | 6,832 | KPIs del panel, series mensuales y actividad reciente. |
| `Services/EmployeeService.cs` | 124 | 5,832 | Empleados: alta/edición, baja lógica y resumen. |
| `Services/InventoryService.cs` | 375 | 18,496 | Productos, stock, movimientos, ajustes, mínimos y resumen. |
| `Services/InvoiceService.cs` | 620 | 30,384 | Facturas internas: borrador, emisión, cancelación, pagos, saldos y folios. |
| `Services/PayrollService.cs` | 451 | 21,231 | Periodos, cálculo de nómina, ISR/IMSS/Infonavit, pago, reversión y borrado. |
| `Services/Permissions.cs` | 69 | 2,918 | 14 permisos, 4 roles, etiquetas y descripciones. |
| `Services/ReportService.cs` | 260 | 11,716 | Ingresos/egresos, flujo, antigüedad de saldos, mensual y balances. |
| `Services/TaxService.cs` | 130 | 5,568 | Resumen de IVA por periodo y anual; base para CSV. |
| `Services/TransactionService.cs` | 280 | 13,334 | Movimientos: filtros, alta/edición, borrado, totales y etiquetas. |
| `Services/UserService.cs` | 174 | 7,984 | Usuarios, roles, reset de contraseña y consulta de auditoría. |

### 5.4 Nómina, utilidades y presentación

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `Payroll/Isr.cs` | 65 | 3,044 | Parseo de tramos JSON y cálculo de ISR por periodo. |
| `Util/AppLog.cs` | 38 | 1,037 | Bitácora local `libro.log`, tolerante a fallos. |
| `Util/Dates.cs` | 94 | 4,574 | Fechas ISO, formato es-MX, meses, periodos y diferencias. |
| `Util/Money.cs` | 58 | 2,813 | Formato/parseo de centavos, cantidades y porcentajes. |
| `Util/Validation.cs` | 24 | 1,049 | Correo, contraseña y recorte de texto. |
| `Controls/DateField.xaml` | 35 | 2,050 | Control compuesto de fecha con caja de texto y calendario. |
| `Controls/DateField.xaml.cs` | 92 | 3,042 | Acepta `dd/MM/yyyy`, `d/M/yyyy`, `yyyy-MM-dd`; Enter/blur. |
| `Converters/UiConverters.cs` | 115 | 4,935 | Convertidores de visibilidad, dinero, estado, color y nulos. |
| `Resources/Styles.xaml` | 477 | 28,385 | Sistema visual: colores, tipografía, botones, tarjetas, tablas y focos. |

### 5.5 Ventanas de shell y acceso

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `Views/LoginWindow.xaml` | 63 | 4,180 | Formulario de inicio de sesión. |
| `Views/LoginWindow.xaml.cs` | 59 | 1,766 | Login, errores y transición a `MainWindow`. |
| `Views/SetupWindow.xaml` | 95 | 6,801 | Alta inicial de empresa y administrador. |
| `Views/SetupWindow.xaml.cs` | 67 | 1,973 | Validación, `AuthService.Setup` y sesión inicial. |
| `Views/IRefreshable.cs` | 5 | 86 | Contrato `Refresh()` para vistas recargables. |
| `Views/PlaceholderPages.cs` | 11 | 295 | Fábrica de páginas vacías para secciones no implementadas. |
| `Views/PlaceholderView.xaml` | 22 | 1,504 | Estado vacío reutilizable. |

`MainWindow` está en §5.1 porque pertenece al arranque, no a una sección de
negocio.

### 5.6 Vistas principales

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `Views/DashboardView.xaml` | 295 | 21,116 | Panel: KPIs, gráfica, cobros, inventario y actividad. |
| `Views/DashboardView.xaml.cs` | 55 | 2,480 | Carga el `DashboardData` y abre atajos. |
| `Views/TransactionsView.xaml` | 247 | 17,823 | Lista de ingresos/egresos, filtros, totales y acciones. |
| `Views/TransactionsView.xaml.cs` | 170 | 7,686 | Filtros, paginación, alta/edición, borrado y exportación. |
| `Views/InvoicesView.xaml` | 235 | 17,199 | Facturas: indicadores, pestañas, lista y acciones. |
| `Views/InvoicesView.xaml.cs` | 103 | 4,286 | Filtros, navegación, alta y apertura de detalle. |
| `Views/ContactsView.xaml` | 233 | 16,985 | Contactos: búsqueda, tipo, tabla y estadísticas. |
| `Views/ContactsView.xaml.cs` | 114 | 4,890 | Filtros, alta/edición, archivo y detalle. |
| `Views/InventoryView.xaml` | 232 | 17,092 | Inventario: productos, stock, mínimos y movimientos. |
| `Views/InventoryView.xaml.cs` | 191 | 8,227 | Filtros, alta/edición, activación, movimiento y CSV. |
| `Views/PayrollView.xaml` | 150 | 10,210 | Nómina: periodos, totales y acciones. |
| `Views/PayrollView.xaml.cs` | 102 | 4,032 | Crear periodo, recalcular, pagar y abrir detalle. |
| `Views/PayrollDetailWindow.xaml` | 205 | 14,211 | Detalle de periodo: ítems, conceptos y totales. |
| `Views/PayrollDetailWindow.xaml.cs` | 145 | 6,438 | Edición de ítems, recálculo, pago y reversión. |
| `Views/EmployeesView.xaml` | 173 | 12,257 | Empleados: listado, estado, salario y alta. |
| `Views/EmployeesView.xaml.cs` | 125 | 5,378 | Alta/edición, baja/reactivación y resumen. |
| `Views/TaxesView.xaml` | 196 | 12,796 | Impuestos: IVA del periodo, anual y exportación. |
| `Views/TaxesView.xaml.cs` | 152 | 6,571 | Consulta, cambio de periodo y CSV. |
| `Views/ReportsView.xaml` | 380 | 25,032 | Reportes: ingresos, flujo, antigüedad, mensual y balances. |
| `Views/ReportsView.xaml.cs` | 243 | 10,744 | Periodos, cálculos, gráficas y exportaciones. |
| `Views/UsersView.xaml` | 210 | 13,940 | Usuarios y auditoría: pestañas, filtros y tabla. |
| `Views/UsersView.xaml.cs` | 254 | 10,481 | Alta/edición, activación, reset y consulta de bitácora. |
| `Views/SettingsView.xaml` | 350 | 25,514 | Configuración: empresa, cuentas, categorías, ISR y respaldos. |
| `Views/SettingsView.xaml.cs` | 526 | 21,658 | Guardado, catálogos, validación de ISR, respaldo/restauración. |

### 5.7 Diálogos y ventanas de detalle

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `Views/Dialogs/AccountDialog.xaml` | 69 | 3,855 | Alta/edición de cuenta (efectivo, banco, tarjeta, “Otro”). |
| `Views/Dialogs/AccountDialog.xaml.cs` | 60 | 1,824 | Validación y devolución del modelo. |
| `Views/Dialogs/CategoryDialog.xaml` | 55 | 2,882 | Alta/edición de categoría de ingreso/egreso. |
| `Views/Dialogs/CategoryDialog.xaml.cs` | 47 | 1,509 | Validación y devolución. |
| `Views/Dialogs/ContactDialog.xaml` | 94 | 5,639 | Contacto: tipo, RFC, datos de contacto y notas. |
| `Views/Dialogs/ContactDialog.xaml.cs` | 62 | 2,202 | Validación y guardado. |
| `Views/Dialogs/EmployeeDialog.xaml` | 143 | 8,565 | Empleado: puesto, salario, IMSS/Infonavit y fechas. |
| `Views/Dialogs/EmployeeDialog.xaml.cs` | 83 | 3,098 | Validación y guardado. |
| `Views/Dialogs/InvoiceDialog.xaml` | 210 | 14,993 | Factura: cliente, conceptos, IVA, totales y borrador/emisión. |
| `Views/Dialogs/InvoiceDialog.xaml.cs` | 176 | 7,148 | Líneas, autocompletado de producto, totales y guardado. |
| `Views/Dialogs/InvoiceDetailWindow.xaml` | 254 | 17,946 | Detalle: ítems, pagos, saldo, cancelación y acciones. |
| `Views/Dialogs/InvoiceDetailWindow.xaml.cs` | 204 | 8,929 | Pago, cancelación, borrado de pago y refresco. |
| `Views/Dialogs/InvoiceLineRow.cs` | 116 | 3,569 | Fila editable de concepto con cálculos y notificaciones. |
| `Views/Dialogs/PasswordResetDialog.xaml` | 50 | 2,656 | Reset administrativo de contraseña. |
| `Views/Dialogs/PasswordResetDialog.xaml.cs` | 41 | 1,292 | Validación y devolución. |
| `Views/Dialogs/PaymentDialog.xaml` | 89 | 4,981 | Registro de pago: fecha, importe, cuenta y método. |
| `Views/Dialogs/PaymentDialog.xaml.cs` | 64 | 2,217 | Validación y guardado. |
| `Views/Dialogs/PayrollItemDialog.xaml` | 108 | 6,862 | Concepto de nómina: días, base, extra, ISR, IMSS, Infonavit. |
| `Views/Dialogs/PayrollItemDialog.xaml.cs` | 84 | 3,264 | Cálculo y edición del ítem. |
| `Views/Dialogs/PayrollPaymentDialog.xaml` | 59 | 3,362 | Pago del periodo: cuenta y fecha. |
| `Views/Dialogs/PayrollPaymentDialog.xaml.cs` | 54 | 1,766 | Validación y devolución. |
| `Views/Dialogs/PayrollPeriodDialog.xaml` | 85 | 4,812 | Creación de periodo: año, mes, tipo y quincena. |
| `Views/Dialogs/PayrollPeriodDialog.xaml.cs` | 82 | 3,296 | Validación y guardado. |
| `Views/Dialogs/ProductDialog.xaml` | 128 | 7,750 | Producto: SKU, precio, costo, IVA, stock y mínimo. |
| `Views/Dialogs/ProductDialog.xaml.cs` | 90 | 3,398 | Validación y guardado. |
| `Views/Dialogs/ProductDetailWindow.xaml` | 220 | 15,283 | Detalle: stock, movimientos, edición y ajustes. |
| `Views/Dialogs/ProductDetailWindow.xaml.cs` | 187 | 7,861 | Movimientos, activación y refresco. |
| `Views/Dialogs/TextPromptDialog.xaml` | 37 | 2,062 | Entrada de texto genérica (motivo de cancelación). |
| `Views/Dialogs/TextPromptDialog.xaml.cs` | 22 | 606 | Devuelve el texto o `null`. |
| `Views/Dialogs/TransactionDialog.xaml` | 147 | 9,338 | Movimiento: tipo, fecha, importe, IVA, cuenta y categoría. |
| `Views/Dialogs/TransactionDialog.xaml.cs` | 181 | 7,895 | Cálculo de IVA, validación y guardado. |
| `Views/Dialogs/UserDialog.xaml` | 86 | 5,075 | Usuario: nombre, correo, rol, estado y contraseña. |
| `Views/Dialogs/UserDialog.xaml.cs` | 76 | 2,496 | Validación y guardado. |

**Total de diálogos:** 17 ventanas/clases, 33 archivos contando XAML y code-behind.

### 5.8 Archivos que no se deben tocar sin leer otro documento

| Archivo | Riesgo | Documento |
|---|---|---|
| `Data/Database.cs` | Migración, cifrado, respaldo y restauración. | [03](03-modelo-datos.md), [04](04-seguridad.md) |
| `Data/Schema.cs` | Las 16 tablas y sus `CHECK`; no hay migraciones incrementales. | [03](03-modelo-datos.md) |
| `Services/AuthService.cs` | Roles, bloqueo y auditoría. | [04](04-seguridad.md) |
| `Services/InvoiceService.cs` | Folios, estados, stock y pagos. | [05](05-modulos-flujos.md) |
| `Services/PayrollService.cs` | ISR, IMSS, Infonavit y asientos. | [05](05-modulos-flujos.md) |
| `Services/Permissions.cs` | Matriz de 14 permisos. | [04](04-seguridad.md) |
| `Views/SettingsView.xaml.cs` | Respaldos y configuración destructiva. | [06](06-mapa-ui.md), [08](08-operacion-instalador.md) |
| `SelfTest.cs` | Contrato de `SELFTEST_OK`. | [09](09-pruebas.md) |

---

## 6. CLI para agentes `agent-harness/`

**11 archivos fuente, 4,379 líneas, 197,052 bytes** (sin `bin/`, `obj/` ni
`__pycache__/`).

### 6.1 Código

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `Libro.Cli/Libro.Cli.csproj` | 18 | 662 | `Exe` .NET 8, referencia a `Libro.Desktop.csproj`, versión 1.0.0. |
| `Libro.Cli/Program.cs` | 223 | 7,302 | Despacho one-shot/REPL, intercepción segura de `--help`, códigos de salida. |
| `Libro.Cli/Cli.cs` | 478 | 17,806 | Parser, opciones globales, credenciales, salida humana/JSON y tokenizer. |
| `Libro.Cli/Commands.cs` | 2,021 | 91,754 | Catálogo de comandos, ayuda, esquemas, respaldos y auditoría. |
| `libro-cli.cmd` | 25 | 687 | Wrapper que resuelve `dotnet` y ejecuta la DLL. |
| `build.ps1` | 30 | 1,012 | Compila en Release y, con `-Test`, ejecuta `unittest`. |
| `run-tests.cmd` | 12 | 370 | Ejecuta solo las pruebas, con `PYTHONUTF8=1`. |
| `tests/test_cli.py` | 600 | 27,843 | 11 pruebas E2E contra una base temporal. |

`Commands.cs` es el archivo más grande del harness y concentra la mayor parte
de la superficie pública. No contiene lógica contable: llama a los servicios de
`Libro.Desktop`.

### 6.2 Documentación y skill

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `README.md` | 434 | 20,430 | Guía de instalación, uso, arquitectura y mantenimiento del harness. |
| `TEST.md` | 246 | 16,888 | Plan, catálogo y resultados de prueba. |
| `skills/cli-anything-libro/SKILL.md` | 292 | 12,298 | Skill generada para agentes; se copia a OpenCode. |

La skill instalada vive fuera del repositorio:

```text
%USERPROFILE%\.config\opencode\skills\cli-anything-libro\SKILL.md
```

El repositorio conserva la fuente. Si se edita una, hay que sincronizar la otra.

### 6.3 Generados del harness

| Ruta | Archivos | Bytes | Nota |
|---|---:|---:|---|
| `Libro.Cli/bin/`, `obj/` | 59 | 35,201,570 | Incluye copia del runtime y de `Libro.dll`. |
| `tests/__pycache__/` | 1 | 39,934 | Caché de Python; se puede borrar. |

---

## 7. Instalador `installer/`

### 7.1 Instalador vigente

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `libro-native.iss` | 107 | 4,081 | Inno Setup 2.0.0: por usuario, datos preservados, limpieza legacy. |
| `build-native.ps1` | 76 | 3,396 | Publica, poda culturas, autoprueba, compila Inno y calcula SHA256. |
| `libro.ico` | — | 9,508 | Icono del setup y de la app (`ApplicationIcon`). |

### 7.2 Instalador legado y artefactos intermedios

| Archivo | Líneas | Bytes | Estado | Propósito |
|---|---:|---:|---|---|
| `libro.iss` | 100 | 3,841 | **Legado** | Instalador Inno de Next.js 1.0.0. |
| `build.ps1` | 153 | 6,268 | **Legado** | Build Next.js + launcher + Inno. |
| `Launcher.cs` | 271 | 11,933 | **Legado** | Lanzador WinForms que inicia Node y abre el navegador. |
| `start-server.cmd` | 12 | 397 | **Legado** | Arranca `node.exe server.js` en el puerto 3000. |
| `IconBuilder.cs` | 130 | 4,404 | **Soporte histórico** | Genera `libro.ico` con `System.Drawing`. |

`IconBuilder.cs` no se usa en el build vigente; el `.ico` ya está versionado.

### 7.3 Staging `installer/native-app/`

| Métrica | Valor |
|---|---:|
| Archivos totales | 265 |
| Archivos en la raíz | 248 |
| Archivos en `es\` | 17 |
| Bytes totales | 155,754,703 |
| Bytes en la raíz | 154,521,735 |
| Bytes en `es\` | 1,232,968 |
| Archivos `*.db` | 0 |
| `libro.ico` | **Ausente** (INS-02) |

Archivos propios de la aplicación:

| Archivo | Bytes |
|---|---:|
| `Libro.exe` | 161,792 |
| `Libro.dll` | 638,976 |
| `Libro.deps.json` | — |
| `Libro.runtimeconfig.json` | — |
| `e_sqlcipher.dll` | 1,852,928 |

El resto es el runtime .NET 8 self-contained y dependencias de WPF/SQLite. La
carpeta se regenera con `build-native.ps1`; no se edita a mano.

`installer/app/` (staging legado de Next.js) **no existe** en este corte. El
instalador legado no se puede reconstruir sin `npm install` y `npm run build`.

---

## 8. Pruebas

### 8.1 Automatizadas

| Elemento | Ubicación | Cobertura |
|---|---|---|
| E2E del CLI | `agent-harness/tests/test_cli.py` | 11 métodos, 4,379 líneas de harness, base temporal. |
| Autoprueba de la app | `desktop/Libro.Desktop/SelfTest.cs` | 35 comprobaciones, `SELFTEST_OK`. |

Los 11 métodos son:

```text
test_00_help_and_version
test_01_doctor_before_setup
test_02_setup_and_auth
test_03_seed_data
test_04_write_workflow
test_04b_help_is_side_effect_free
test_05_permissions_and_users
test_06_backup_restore
test_07_json_errors
test_08_repl
test_09_doctor_orphan_database
```

### 8.2 Manuales

No hay archivos de prueba de UI, instalador ni accesibilidad. Los checklists
están en [09](09-pruebas.md) y [08](08-operacion-instalador.md).

---

## 9. Documentación `docs/`

| Archivo | Líneas (no vacías) | Bytes | Contenido |
|---|---:|---:|---|
| `README.md` | 161 | 9,620 | Índice, rutas de lectura, convenciones y mantenimiento. |
| `01-vision-alcance.md` | 184 | 11,031 | Problema, alcance, invariantes, roles y métricas. |
| `02-arquitectura.md` | 895 | 44,980 | Capas, proyectos, arranque, publicación y artefactos. |
| `03-modelo-datos.md` | 867 | 43,956 | 16 tablas, ER, enums, DTO, semillas y migración. |
| `04-seguridad.md` | 742 | 43,032 | SQLCipher, DPAPI, scrypt, permisos, amenazas y auditoría. |
| `05-modulos-flujos.md` | 897 | 42,233 | Fórmulas, estados, casos límite y errores funcionales. |
| `06-mapa-ui.md` | 1,038 | 61,680 | Pantallas, diálogos, permisos visuales y 20 defectos UI. |
| `07-cli.md` | 1,544 | 72,488 | Comandos, JSON, REPL, permisos y 43 hallazgos. |
| `08-operacion-instalador.md` | 1,335 | 73,248 | Instalación, actualización, desinstalación y 14 hallazgos. |
| `09-pruebas.md` | 946 | 44,515 | Capas, aislamiento, catálogo y 18 brechas. |
| `10-inventario-archivos.md` | — | — | Este documento. |
| `11-legado-nextjs.md` | 723 | 33,357 | Aplicación e instalador 1.0.0. |
| `12-glosario.md` | 981 | 41,220 | Términos contables, técnicos y fiscales. |
| `13-hallazgos-riesgos.md` | 642 | 59,635 | Registro consolidado `H-nn`. |
| `14-decisiones.md` | 577 | 33,068 | Decisiones de arquitectura y operación. |
| `15-evidencia-graphify.md` | 287 | 15,397 | Wiki, grafo, reportes y límites. |

La documentación curada es la entrega principal. `graphify-out/wiki/` es
evidencia derivada y puede contener rutas, nombres de símbolos y ruido de
extracción; no sustituye a estos tomos.

---

## 10. Artefactos `dist/`

| Archivo | Bytes | SHA256 | Estado |
|---|---:|---|---|
| `Libro-Setup-2.0.0.exe` | 50,513,464 | `A7B78EE1B22405E696A5B7ADD764DD2E9C5E5F5C04641ACA5E10D790AAFDB525` | **Vigente** |
| `Libro-Setup-2.0.0.exe.sha256` | 89 | — | **Vigente** |
| `Libro-Setup-1.0.0.exe` | 36,468,726 | `028BE5C570E98E60B3333C71DEA0CFE70F6C68EB2C530127A159D1F674032EFD` | Archivado |
| `Libro-Setup-1.0.0.exe.sha256` | 89 | — | Archivado |
| `LEEME.txt` | 559 | — | Vigente |

El detalle del ciclo de vida está en
[08](08-operacion-instalador.md). No se debe reconstruir el setup 2.0.0 “para
corregir” un detalle cosmético sin volver a probar todo el ciclo: el hash
cambiaría y el artefacto dejaría de coincidir con el documentado.

---

## 11. Herramientas `tools/Libro.DbTool/`

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `Libro.DbTool.csproj` | 14 | 387 | Consola .NET 8 que referencia la app. |
| `Program.cs` | 58 | 2,101 | Resumen de base y reset de contraseña en una **copia**. |

No se instala. Su uso previsto es diagnóstico sobre una carpeta temporal:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" run `
  --project tools\Libro.DbTool -- "C:\ruta\a\copia" admin@ejemplo.mx "NuevaClave123!"
```

`tools/**/bin` y `obj` ocupan 34,769,987 bytes porque la referencia al proyecto
copia la app y su runtime.

---

## 12. Legado Next.js `src/`

**68 archivos, 12,863 líneas, 477,101 bytes.** Se conserva como referencia
histórica. No se ejecuta para operar Libro 2.0.0.

### 12.1 Rutas de interfaz

| Ruta | Archivo | Líneas | Propósito |
|---|---|---:|---|
| `/` | `src/app/page.tsx` | 9 | Redirección inicial. |
| `/login` | `src/app/login/page.tsx` | 55 | Inicio de sesión web. |
| `/setup` | `src/app/setup/page.tsx` | 82 | Alta inicial web. |
| `/panel` | `src/app/(app)/panel/page.tsx` | 380 | Panel. |
| `/movimientos` | `src/app/(app)/movimientos/page.tsx` | 272 | Lista de movimientos. |
| `/movimientos/nuevo` | `src/app/(app)/movimientos/nuevo/page.tsx` | 45 | Alta. |
| `/movimientos/[id]/editar` | `src/app/(app)/movimientos/[id]/editar/page.tsx` | 69 | Edición. |
| `/facturas` | `src/app/(app)/facturas/page.tsx` | 207 | Lista de facturas. |
| `/facturas/nueva` | `src/app/(app)/facturas/nueva/page.tsx` | 45 | Alta. |
| `/facturas/[id]` | `src/app/(app)/facturas/[id]/page.tsx` | 422 | Detalle. |
| `/facturas/[id]/editar` | `src/app/(app)/facturas/[id]/editar/page.tsx` | 70 | Edición. |
| `/contactos` | `src/app/(app)/contactos/page.tsx` | 211 | Contactos. |
| `/inventario` | `src/app/(app)/inventario/page.tsx` | 254 | Inventario. |
| `/inventario/[id]` | `src/app/(app)/inventario/[id]/page.tsx` | 222 | Detalle de producto. |
| `/nomina` | `src/app/(app)/nomina/page.tsx` | 191 | Nómina. |
| `/nomina/[id]` | `src/app/(app)/nomina/[id]/page.tsx` | 390 | Detalle de periodo. |
| `/empleados` | `src/app/(app)/empleados/page.tsx` | 192 | Empleados. |
| `/impuestos` | `src/app/(app)/impuestos/page.tsx` | 291 | IVA e impuestos. |
| `/reportes` | `src/app/(app)/reportes/page.tsx` | 375 | Reportes. |
| `/usuarios` | `src/app/(app)/usuarios/page.tsx` | 245 | Usuarios y auditoría. |
| `/configuracion` | `src/app/(app)/configuracion/page.tsx` | 585 | Configuración y respaldos. |
| `/cuenta` | `src/app/(app)/cuenta/page.tsx` | 105 | Cuenta y cambio de contraseña. |
| `/imprimir/factura/[id]` | `src/app/(print)/imprimir/factura/[id]/page.tsx` | 210 | Impresión de factura en el navegador. |

### 12.2 Server Actions

| Archivo | Líneas | Propósito |
|---|---:|---|
| `src/app/actions/auth.ts` | 131 | Login, logout, setup y contraseña. |
| `src/app/actions/contacts.ts` | 99 | Contactos. |
| `src/app/actions/employees.ts` | 139 | Empleados. |
| `src/app/actions/invoices.ts` | 198 | Facturas y pagos. |
| `src/app/actions/payroll.ts` | 393 | Nómina. |
| `src/app/actions/products.ts` | 290 | Productos e inventario. |
| `src/app/actions/settings.ts` | 276 | Empresa, cuentas, categorías y respaldos. |
| `src/app/actions/transactions.ts` | 195 | Movimientos. |
| `src/app/actions/users.ts` | 150 | Usuarios. |

### 12.3 API HTTP

| Archivo | Líneas | Propósito |
|---|---:|---|
| `src/app/api/exportar/route.ts` | 252 | Exportación CSV. |
| `src/app/api/respaldo/[archivo]/route.ts` | 34 | Descarga de respaldos. |
| `src/app/api/salud/route.ts` | 9 | Health check. |

### 12.4 Librerías

| Archivo | Líneas | Propósito |
|---|---:|---|
| `src/lib/audit.ts` | 17 | Registro de auditoría. |
| `src/lib/auth.ts` | 121 | Sesiones por cookie. |
| `src/lib/backups.ts` | 73 | Respaldo SQLite. |
| `src/lib/cn.ts` | 5 | Utilidad de clases CSS. |
| `src/lib/data.ts` | 836 | Consultas y agregados. |
| `src/lib/dates.ts` | 126 | Fechas y zona horaria. |
| `src/lib/db.ts` | 92 | SQLite plano con `node:sqlite`. |
| `src/lib/invoices.ts` | 373 | Reglas de facturas. |
| `src/lib/money.ts` | 80 | Dinero en centavos. |
| `src/lib/payroll.ts` | 139 | Nómina e ISR. |
| `src/lib/permissions.ts` | 62 | Permisos y roles. |
| `src/lib/schema.ts` | 226 | Esquema SQL. |
| `src/lib/types.ts` | 146 | Tipos. |
| `src/lib/validation.ts` | 44 | Validaciones. |

### 12.5 Componentes y estilos

| Archivo | Líneas | Propósito |
|---|---:|---|
| `src/components/action-form.tsx` | 75 | Formulario de acción genérico. |
| `src/components/app-shell.tsx` | 260 | Shell de la app web. |
| `src/components/auth-layout.tsx` | 97 | Layout de login/setup. |
| `src/components/bar-chart.tsx` | 83 | Gráfica de barras. |
| `src/components/brand.tsx` | 47 | Marca. |
| `src/components/contact-form.tsx` | 110 | Formulario de contacto. |
| `src/components/employee-form.tsx` | 150 | Formulario de empleado. |
| `src/components/inventory-movement-form.tsx` | 238 | Movimiento de inventario. |
| `src/components/invoice-form.tsx` | 421 | Formulario de factura. |
| `src/components/print-button.tsx` | 8 | Botón de impresión. |
| `src/components/product-form.tsx` | 153 | Formulario de producto. |
| `src/components/theme-toggle.tsx` | 34 | Tema claro/oscuro. |
| `src/components/transaction-form.tsx` | 310 | Formulario de movimiento. |
| `src/components/ui.tsx` | 195 | Componentes UI. |
| `src/app/globals.css` | — | Tailwind 4 y variables visuales. |
| `src/app/layout.tsx` | 25 | Layout raíz. |
| `src/app/(app)/layout.tsx` | 12 | Layout autenticado. |
| `src/app/(print)/layout.tsx` | 10 | Layout de impresión. |
| `src/app/icon.svg` | — | Icono web. |

### 12.6 Diferencias que impiden confundirlos

| Aspecto | `src/` (legado) | `desktop/` (vigente) |
|---|---|---|
| Runtime | Node.js / Next.js | .NET 8 / WPF |
| Base | SQLite plano con `node:sqlite` | SQLCipher |
| Sesión | Cookie | Memoria del proceso |
| Interfaz | Navegador | Ventanas nativas |
| Impresión | Ruta `/imprimir/factura/[id]` | **No hay impresión** |
| Exportación | API `/api/exportar` | CSV desde la UI |
| Servidor | Puerto 3000 | No hay servidor |
| Instalador | `libro.iss` | `libro-native.iss` |

El legado se documenta en [11](11-legado-nextjs.md). No se debe presentar como
comportamiento actual.

---

## 13. Scripts legados

| Archivo | Líneas | Bytes | Propósito |
|---|---:|---:|---|
| `scripts/clean-standalone.mjs` | 28 | 760 | Borra `data/` y `*.db*` del standalone de Next antes de empaquetar. |

Se ejecuta con `npm run postbuild`. No participa en el build nativo.

---

## 14. Artefactos generados y de construcción

### 14.1 `.next/`

| Métrica | Valor |
|---|---:|
| Archivos | 2,326 |
| Bytes | 299,544,407 |

Build de Next.js, incluido el standalone que usó el instalador 1.0.0. No se
necesita para la app nativa. Si se borra, el legado deja de poder arrancar
hasta reconstruirlo.

### 14.2 `node_modules/`

| Métrica | Valor |
|---|---:|
| Archivos | 14,658 |
| Bytes | 406,296,114 |

Dependencias npm del legado. No las usa ningún proyecto .NET.

### 14.3 `bin/` y `obj/`

| Ruta | Archivos | Bytes |
|---|---:|---:|
| `desktop/**/bin`, `obj` | 1,119 | 246,484,945 |
| `agent-harness/**/bin`, `obj` | 59 | 35,201,570 |
| `tools/**/bin`, `obj` | 59 | 34,769,987 |

Incluyen copias del runtime, de `Libro.dll` y de `e_sqlcipher.dll`. La DLL del
CLI que ejecutan las pruebas es:

```text
agent-harness\Libro.Cli\bin\Release\net8.0-windows\libro-cli.dll
```

### 14.4 `graphify-out/`

| Elemento | Bytes | Naturaleza |
|---|---:|---|
| `graph.json` | 4,831,665 | Grafo de nodos/aristas. |
| `graph.html` | 3,573,230 | Visor interactivo. |
| `GRAPH_REPORT.md` | 41,484 | Reporte de comunidades y nodos. |
| `ARQUITECTURA.html` | 334,113 | 17 secciones, 16 diagramas Mermaid, 15 tablas de llamadas. |
| `ARBOL.html` | 157,008 | Árbol colapsable del código. |
| `manifest.json` | 44,611 | Manifiesto de archivos indexados. |
| `wiki/` | 351,118 | 195 archivos Markdown. |
| `query-*.txt` | ~44,000 | Evidencia de consultas de arquitectura, contabilidad, seguridad y CLI. |
| `.graphify_analysis.json` | 125,961 | Análisis auxiliar (puede estar desactualizado). |
| `autostart.log` | 182,947 | Log del plugin de actualización. |

El grafo incluye `src/` y `docs/`; por eso no debe interpretarse como un mapa
exclusivo de la app vigente. La evidencia y sus límites están en
[15](15-evidencia-graphify.md).

---

## 15. Datos locales y archivos sensibles

### 15.1 `data/` en la raíz (legado)

| Archivo | Bytes | Contenido |
|---|---:|---|
| `data/contabilidad.db` | 155,648 | SQLite **en texto plano**, con cabecera `SQLite format 3`. |
| `data/contabilidad.db-wal` | 4,120,032 | WAL pendiente. |
| `data/contabilidad.db-shm` | 32,768 | Memoria compartida de SQLite. |
| `data/respaldos/contabilidad-2026-10-22_203523.db` | 155,648 | Respaldo legado en texto plano. |

### 15.2 `backups/legacy-pre-native-20261023/`

| Archivo | Bytes | Contenido |
|---|---:|---|
| `contabilidad.db` | 155,648 | Copia **en texto plano** previa a la migración. |
| `LEEME.txt` | 468 | Advertencia de datos reales y conservación temporal. |

### 15.3 La carpeta real de la app nativa

No está en el repositorio. Vive en:

```text
%LOCALAPPDATA%\Libro\data\
├── contabilidad.db        SQLCipher
├── contabilidad.db-wal
├── contabilidad.db-shm
├── clave.bin              DPAPI
├── respaldos\*.libro
└── libro.log
```

**Nunca** copies esa carpeta a `data/`, `backups/`, `dist/` ni al staging del
instalador. El build solo rechaza `*.db`; no detecta `clave.bin`, `*.libro`,
`*.db-wal` ni `libro.log` (INS-09).

### 15.4 Reglas de manejo

1. No incluir `data/` ni `backups/` en un ZIP, correo o repositorio.
2. No compartir `clave.bin`: sin la cuenta de Windows no sirve, pero con ella
   descifra la base.
3. No publicar `libro.log` sin sanear rutas y mensajes.
4. Borrar `backups/legacy-pre-native-20261023` solo después de verificar la
   app nativa y conforme a la política del negocio.
5. Los respaldos `.libro` están cifrados con la misma llave DPAPI; tampoco son
   portables por sí solos.

---

## 16. Archivos que nunca deben distribuirse

| Patrón | Motivo |
|---|---|
| `data/**` | Base legada en texto plano con datos reales. |
| `backups/**` | Copia de seguridad en texto plano con datos reales. |
| `**/clave.bin` | Llave DPAPI. |
| `**/*.libro` | Respaldo cifrado completo. |
| `**/contabilidad.db*` | Base y WAL/SHM. |
| `**/libro.log` | Rutas, errores y actividad. |
| `installer/native-app/**` | Staging; se distribuye solo dentro del setup. |
| `agent-harness/**/bin`, `obj` | Binarios de desarrollo. |
| `.next/**`, `node_modules/**` | Legado y dependencias. |
| `graphify-out/**` | Evidencia derivada con rutas internas. |
| `dist/Libro-Setup-1.0.0.exe` | Instalador legado; no debe instalarse sobre 2.0.0. |

---

## 17. Cómo mantener este inventario

### 17.1 Al agregar un archivo

1. Decide su estado: vigente, soporte, legado o generado.
2. Si es fuente, agrégalo a la tabla de su área con líneas y bytes.
3. Si es generado, no lo enumeres archivo por archivo: describe la carpeta y su
   origen.
4. Si es dato, no lo inventaríes: documenta la ruta y la regla de manejo.
5. Actualiza los totales de §1.
6. Revisa si el archivo debe entrar en `.gitignore`.
7. Si cambia el instalador, actualiza [08](08-operacion-instalador.md).

### 17.2 Al borrar un archivo

1. Confirma que no es la única copia de un dato o evidencia.
2. Busca referencias en `docs/`, scripts y `.csproj`.
3. Si era legado, decide si la referencia histórica sigue siendo útil.
4. Actualiza las tablas y los conteos.
5. No borres `data/` ni `backups/` como “limpieza” sin autorización.

### 17.3 Comandos de verificación

```powershell
# Conteo y tamaño por área
Get-ChildItem desktop\Libro.Desktop -Recurse -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Measure-Object Length -Sum

# Buscar archivos sensibles fuera de las carpetas de datos
Get-ChildItem . -Recurse -File -Force |
    Where-Object { $_.Name -in @('clave.bin','libro.log') -or
                   $_.Extension -in @('.libro','.db','.db-wal','.db-shm') } |
    Select-Object FullName, Length

# Confirmar que el staging no lleva bases
(Get-ChildItem installer\native-app -Recurse -File -Filter *.db |
    Measure-Object).Count
```

El último comando debe devolver `0`.

### 17.4 Señales de que el inventario quedó obsoleto

- El número de archivos de `desktop/` ya no es 100.
- Aparece un archivo nuevo en `Services/` o `Views/`.
- Cambia la versión de `Libro.Desktop.csproj` o `Libro.Cli.csproj`.
- Cambia el SHA256 o el tamaño del setup.
- Se agrega una tabla, comando o permiso.
- Se crea o elimina una carpeta de primer nivel.

---

## 18. Documentos relacionados

- [01 · Visión y alcance](01-vision-alcance.md): qué es el producto.
- [02 · Arquitectura](02-arquitectura.md): cómo encajan los proyectos.
- [03 · Modelo de datos](03-modelo-datos.md): qué guarda cada tabla.
- [04 · Seguridad](04-seguridad.md): qué protege cada archivo sensible.
- [05 · Módulos y flujos](05-modulos-flujos.md): qué hace cada servicio.
- [06 · Mapa de UI](06-mapa-ui.md): qué hace cada vista y diálogo.
- [07 · CLI](07-cli.md): qué hace cada archivo del harness.
- [08 · Operación e instalador](08-operacion-instalador.md): qué se empaqueta.
- [09 · Pruebas](09-pruebas.md): qué se verifica.
- [11 · Legado Next.js](11-legado-nextjs.md): el árbol `src/` en detalle.
- [13 · Hallazgos y riesgos](13-hallazgos-riesgos.md): riesgos de los datos y
  del staging.
- [15 · Evidencia Graphify](15-evidencia-graphify.md): artefactos derivados.

---

## 19. Resumen para orientación rápida

1. Si buscas la app vigente, empieza en `desktop/Libro.Desktop/`.
2. Si buscas automatización, empieza en `agent-harness/Libro.Cli/Commands.cs`.
3. Si buscas el instalador, empieza en `installer/libro-native.iss`.
4. Si buscas el legado, empieza en `src/` y `installer/libro.iss`.
5. Si buscas datos reales, **no** están en `data/`: están en
   `%LOCALAPPDATA%\Libro\data`.
6. Si buscas evidencia generada, está en `graphify-out/`; no es fuente.
7. Si buscas qué se distribuye, solo `dist/Libro-Setup-2.0.0.exe`.
