# 11 · Legado Next.js (1.0.0)

> **Estado:** Archivado. **No es el producto vigente.**
> **Corte:** 25 de octubre de 2026.
> **Ámbito:** la aplicación Next.js que vivió en `src/`, el instalador
> `installer/libro.iss`, el build `installer/build.ps1`, el lanzador
> `installer/Launcher.cs` y el artefacto `dist\Libro-Setup-1.0.0.exe`.
> **Fuente de verdad:** `src/`, `installer/libro.iss`, `installer/build.ps1`,
> `installer/Launcher.cs`, `installer/start-server.cmd`,
> `scripts/clean-standalone.mjs`, `package.json` y `dist/`.
> **No cubre:** la aplicación nativa ([02](02-arquitectura.md),
> [05](05-modulos-flujos.md), [06](06-mapa-ui.md)) ni el instalador vigente
> ([08](08-operacion-instalador.md)).

---

## 1. Resumen ejecutivo

Libro 1.0.0 fue una aplicación **web local**:

```text
Next.js 15.5.28 + React 19.1.1
        ↓
Servidor HTTP en 127.0.0.1:3000
        ↓
Navegador del usuario
        ↓
SQLite en texto plano (node:sqlite)
```

Libro 2.0.0 la reemplazó por una aplicación **nativa WPF**:

```text
C# / WPF sobre .NET 8
        ↓
Ventanas nativas, sin navegador
        ↓
SQLCipher + DPAPI
```

La versión 1.0.0 **se conserva como referencia histórica**, no como respaldo
operativo. Comparte el esquema de 16 tablas con la versión nativa, lo que hizo
posible la migración de datos, pero cambia por completo el runtime, la
seguridad y la forma de distribuirse.

| Pregunta | 1.0.0 (legado) |
|---|---|
| Runtime | Node.js + Next.js |
| Interfaz | Navegador |
| Servidor | HTTP local, puerto 3000 |
| Base | SQLite **sin cifrar** |
| Llave | No existe |
| Sesión | Cookie `contabilidad_sesion` |
| Respaldos | `.db` en texto plano |
| Impresión | Ruta web `/imprimir/factura/[id]` |
| Exportación | API `/api/exportar` |
| Instalador | `Libro-Setup-1.0.0.exe` |
| Firma | No |
| Datos | `%LOCALAPPDATA%\Libro\data` (instalada) o `./data` (desarrollo) |
| Facturación | Interna. **No es CFDI.** |

**No instales 1.0.0 sobre 2.0.0.** Comparten `AppId`, carpeta y entrada de
desinstalación; el instalador legado puede dejar la app nativa inservible y su
desinstalador puede borrar todos los datos. `dist/LEEME.txt` lo advierte.

---

## 2. Por qué se conserva

1. **Migración:** la base plana de 1.0.0 es la entrada del migrador de
   `Database.Initialize`. Entender su esquema explica qué se copia.
2. **Compatibilidad de datos:** los respaldos `contabilidad-*.db` siguen en
   `%LOCALAPPDATA%\Libro\data\respaldos`, pero la app nativa no los lista ni los
   restaura.
3. **Auditoría:** permite comparar reglas contables, permisos y fórmulas entre
   versiones.
4. **Soporte histórico:** si un cliente tiene una instalación 1.0.0, se puede
   leer el código para explicar su comportamiento.
5. **Referencia de diseño:** la app nativa reutilizó conceptos (roles, folios,
   estados, reportes) y corrigió otros (cifrado, impresión, empaquetado).

**No se conserva** para ejecutarla en producción ni para instalarla sobre la
versión nativa.

---

## 3. Stack y dependencias

### 3.1 `package.json`

| Elemento | Valor |
|---|---|
| Nombre | `contabilidad` |
| Versión | `0.1.0` (el instalador se etiquetó 1.0.0) |
| Privado | Sí |
| Next.js | `15.5.28` |
| React / React DOM | `19.1.1` |
| TypeScript | `5.9.3` |
| Tailwind CSS | `4.3.3` |
| Zod | `3.25.76` |
| Iconos | `lucide-react 1.61.0` |
| Runtime de base | `node:sqlite` (módulo nativo de Node) |

### 3.2 Scripts npm

| Script | Comando | Uso |
|---|---|---|
| `dev` | `next dev` | Desarrollo. |
| `build` | `next build` | Build de producción. |
| `postbuild` | `node scripts/clean-standalone.mjs` | Limpia `data/` y `*.db*` del standalone. |
| `start` | `next start` | Servidor local. |
| `start:lan` | `next start -H 0.0.0.0` | Expone en la red local. **No recomendado.** |
| `typecheck` | `tsc --noEmit` | Verificación de tipos. |

`next.config.ts` define `output: "standalone"` para que el instalador copie un
servidor autocontenido. `reactStrictMode` está activo y `poweredByHeader`
desactivado.

### 3.3 Lo que no tiene

- No hay pruebas automatizadas.
- No hay ESLint configurado.
- No hay `middleware.ts`.
- No hay autenticación externa (OAuth, SSO).
- No hay API pública versionada.
- No hay WebSocket ni tiempo real.
- No hay CFDI, PAC ni timbrado.

---

## 4. Cómo se ejecutaba

### 4.1 En desarrollo

```powershell
npm install
npm run dev
```

- Next.js escucha en `http://localhost:3000`.
- La base se crea en `./data/contabilidad.db` salvo que se definan `DATA_DIR` o
  `DB_PATH`.
- `src/lib/db.ts` crea `data/` y `data/respaldos/` si no existen.

### 4.2 Instalada

El acceso directo **Libro** ejecutaba `Launcher.cs` (compilado como
`Libro.exe`). El lanzador:

1. crea `%LOCALAPPDATA%\Libro\data`;
2. comprueba `GET http://127.0.0.1:3000/api/salud`;
3. si no hay servidor, toma un mutex `Local\LibroContabilidadLauncher`;
4. ejecuta `start-server.cmd`, que arranca `node.exe server.js` con
   `DATA_DIR=%LOCALAPPDATA%\Libro\data`;
5. espera hasta 60 segundos a que `/api/salud` responda con `"libro"`;
6. abre el navegador en `http://127.0.0.1:3000`;
7. con `--startup`, no abre el navegador (inicio automático);
8. con `--stop`, detiene el servidor por PID y por línea de comandos;
9. con `--silent`, suprime los cuadros de mensaje.

El PID se guarda en `%LOCALAPPDATA%\Libro\server.pid` y el log del servidor en
`%LOCALAPPDATA%\Libro\server.log`.

### 4.3 `start-server.cmd`

```bat
set "DATA_DIR=%LOCALAPPDATA%\Libro\data"
set "PORT=3000"
set "HOSTNAME=127.0.0.1"
set "NODE_ENV=production"
"%~dp0node.exe" "%~dp0server.js" >> "%LOCALAPPDATA%\Libro\server.log" 2>&1
```

- `HOSTNAME=127.0.0.1` limita el acceso a la máquina local.
- Cambiar a `0.0.0.0` expone la app a la red; el README legado lo describía como
  opción manual para LAN.
- No hay TLS: el tráfico es HTTP.

### 4.4 Detención

- Acceso **Detener Libro** → `Libro.exe --stop`.
- El lanzador mata el árbol del `cmd.exe` si el PID sigue siendo el suyo.
- Además busca procesos `node.exe` cuyo comando contenga `server.js` y la
  carpeta de la app; así evita matar un Node ajeno.
- Si el servidor no responde, sugiere reiniciar.

### 4.5 `/api/salud`

```json
{ "ok": true, "app": "libro", "name": "Libro · Contabilidad" }
```

Se usa como sonda de arranque. No requiere autenticación y no expone datos.

---

## 5. Estructura del código

### 5.1 Routas de interfaz

| Ruta | Tipo | Módulo |
|---|---|---|
| `/` | Redirección | Decide login, setup o panel. |
| `/login` | Pública | Inicio de sesión. |
| `/setup` | Pública si no hay usuarios | Alta inicial. |
| `/panel` | Privada | Panel. |
| `/movimientos` | Privada | Ingresos y egresos. |
| `/movimientos/nuevo` | Privada | Alta. |
| `/movimientos/[id]/editar` | Privada | Edición. |
| `/facturas` | Privada | Facturas. |
| `/facturas/nueva` | Privada | Alta. |
| `/facturas/[id]` | Privada | Detalle. |
| `/facturas/[id]/editar` | Privada | Edición de borrador. |
| `/contactos` | Privada | Contactos. |
| `/inventario` | Privada | Inventario. |
| `/inventario/[id]` | Privada | Producto y movimientos. |
| `/nomina` | Privada | Nómina. |
| `/nomina/[id]` | Privada | Detalle de periodo. |
| `/empleados` | Privada | Empleados. |
| `/impuestos` | Privada | IVA. |
| `/reportes` | Privada | Reportes. |
| `/usuarios` | Privada | Usuarios y auditoría. |
| `/configuracion` | Privada | Configuración y respaldos. |
| `/cuenta` | Privada | Cuenta y contraseña. |
| `/imprimir/factura/[id]` | Privada | Impresión. |

### 5.2 Layouts

| Archivo | Propósito |
|---|---|
| `src/app/layout.tsx` | HTML, fuentes, tema, metadatos. |
| `src/app/(app)/layout.tsx` | Exige usuario y monta el shell. |
| `src/app/(print)/layout.tsx` | Layout limpio para imprimir. |

### 5.3 Server Actions

| Archivo | Dominio |
|---|---|
| `actions/auth.ts` | Setup, login, logout, cambio de contraseña. |
| `actions/contacts.ts` | Alta, edición y archivo de contactos. |
| `actions/employees.ts` | Empleados. |
| `actions/invoices.ts` | Facturas, emisión, cancelación y pagos. |
| `actions/payroll.ts` | Periodos, ítems, pago, reversión y borrado. |
| `actions/products.ts` | Productos y movimientos de inventario. |
| `actions/settings.ts` | Empresa, cuentas, categorías, ISR y respaldos. |
| `actions/transactions.ts` | Movimientos. |
| `actions/users.ts` | Usuarios y contraseñas. |

Las acciones validan con Zod, comprueban permisos con `requirePermission` y
registran auditoría. Algunas validaciones delegan en `src/lib`.

### 5.4 API HTTP

| Ruta | Método | Función |
|---|---|---|
| `/api/exportar` | GET | CSV de movimientos, facturas, inventario, empleados, nómina, IVA o resumen. |
| `/api/respaldo/[archivo]` | GET | Descarga un respaldo `.db`; exige `backup.manage`. |
| `/api/salud` | GET | Sonda de salud. |

#### `/api/exportar`

- Tipos: `movimientos`, `facturas`, `inventario`, `empleados`, `nomina`, `iva`,
  `resumen`.
- Filtros: `desde`, `hasta`, `tipo_mov`, `q`.
- Permisos por tipo:
  - movimientos: `reports.view` o `tx.write`;
  - facturas: `reports.view` o `invoice.write`;
  - inventario: `reports.view`, `inventory.write` o `products.write`;
  - empleados y nómina: `payroll.view`;
  - IVA: `tax.view`;
  - resumen: `reports.view`.
- Salida: CSV UTF-8 con BOM, separador coma, fin de línea CRLF.
- Importes: decimales con dos cifras (`100.00`), no centavos.
- No hay descarga autenticada por token: usa la cookie de sesión.

### 5.5 Librerías

| Archivo | Responsabilidad |
|---|---|
| `lib/db.ts` | Abre SQLite, aplica PRAGMAs, crea esquema y semillas, helpers `all/one/run/tx`. |
| `lib/schema.ts` | DDL de las 16 tablas. |
| `lib/data.ts` | Consultas y agregados de todos los módulos. |
| `lib/invoices.ts` | Reglas de facturas, folios, stock y pagos. |
| `lib/payroll.ts` | ISR y cálculo de nómina. |
| `lib/auth.ts` | scrypt, sesiones y guardias. |
| `lib/permissions.ts` | Matriz de roles. |
| `lib/backups.ts` | Crear, listar y resolver respaldos. |
| `lib/dates.ts` | Fechas y zona horaria. |
| `lib/money.ts` | Centavos y formato. |
| `lib/validation.ts` | Esquemas Zod. |
| `lib/audit.ts` | Registro de auditoría. |
| `lib/types.ts` | Tipos compartidos. |

### 5.6 Componentes

| Componente | Uso |
|---|---|
| `app-shell.tsx` | Navegación lateral, cabecera y cierre de sesión. |
| `auth-layout.tsx` | Marco de login/setup. |
| `action-form.tsx` | Formulario con estado de acción. |
| `transaction-form.tsx` | Movimiento. |
| `invoice-form.tsx` | Factura y líneas. |
| `product-form.tsx` | Producto. |
| `inventory-movement-form.tsx` | Movimiento de inventario. |
| `contact-form.tsx` | Contacto. |
| `employee-form.tsx` | Empleado. |
| `bar-chart.tsx` | Gráfica del panel/reportes. |
| `ui.tsx` | Botones, campos, tarjetas, tablas y estados. |
| `theme-toggle.tsx` | Tema claro/oscuro. |
| `print-button.tsx` | `window.print()`. |
| `brand.tsx` | Marca. |

---

## 6. Modelo de datos

### 6.1 Las 16 tablas

`src/lib/schema.ts` define exactamente las mismas 16 tablas que la app nativa:

```text
company          users            sessions         audit_log
accounts         categories       contacts         products
transactions     inventory_movements
invoices         invoice_items    payments
employees        payroll_periods  payroll_items
```

El esquema es compatible columna a columna con `desktop/Libro.Desktop/Data/Schema.cs`
para las columnas que existían en 1.0.0. Esto es lo que permite que el migrador
copie las tablas sin transformaciones.

### 6.2 Detalles del esquema legado

- `company` tiene una sola fila (`CHECK id = 1`).
- `users.email` es `UNIQUE COLLATE NOCASE`.
- `sessions.token_hash` es la clave primaria; se guarda SHA-256 del token, no el
  token.
- `accounts.type` acepta `efectivo`, `banco`, `tarjeta`.
- `categories.kind` acepta `ingreso`, `gasto`, `costo`.
- `products.sku` es único solo cuando no está vacío (índice parcial).
- `invoices.status` acepta `borrador`, `emitida`, `pagada`, `cancelada`.
- `(series, number)` es único; `number` puede ser `NULL` en borrador.
- `payroll_periods` es único por `(year, month, period_type, quincena)`.
- `payroll_items` es único por `(period_id, employee_id)`.
- No hay `user_version` leído; se escribe `1`.
- No hay migraciones incrementales: `CREATE TABLE IF NOT EXISTS`.

### 6.3 Semillas

- 13 categorías: Ventas, Servicios, Otros ingresos, Renta, Servicios (luz, agua,
  internet), Papelería y oficina, Publicidad, Transporte, Nómina, Impuestos y
  cuotas, Compras de inventario, Mantenimiento, Otros gastos.
- 2 cuentas: Caja (efectivo) y Banco (banco).
- No se crea empresa ni usuario hasta el setup.

### 6.4 PRAGMAs

```text
journal_mode = WAL
foreign_keys = ON
synchronous = NORMAL
timeout = 5000 ms
```

No hay `PRAGMA key`, porque no hay cifrado.

---

## 7. Autenticación y permisos

### 7.1 Contraseñas

- scrypt con `N=16384`, `r=8`, `p=1`, sal aleatoria de 16 bytes y hash de 64
  bytes.
- Formato: `scrypt$16384$8$1$<sal-hex>$<hash-hex>`.
- Verificación con `timingSafeEqual`.
- La misma función se usa en setup, login, cambio y reset.

### 7.2 Sesiones

| Aspecto | Valor |
|---|---|
| Cookie | `contabilidad_sesion` |
| Contenido | Token aleatorio de 32 bytes en base64url |
| Almacenamiento | SHA-256 del token en `sessions.token_hash` |
| Duración | 30 días |
| `HttpOnly` | Sí |
| `SameSite` | `Lax` |
| `Secure` | Solo si `COOKIE_SECURE=1` |
| `Path` | `/` |
| Limpieza | Borra sesiones expiradas al crear una nueva |
| Logout | Borra la fila y la cookie |

`Secure=1` no funciona sobre HTTP en `127.0.0.1`; por eso el valor por defecto es
`0`. En producción local esto es aceptable solo porque el servidor escucha en
loopback. Si se expone en LAN sin TLS, la cookie viaja sin `Secure`.

### 7.3 Bloqueo de login

- 10 intentos por correo en 10 minutos.
- El contador vive en un `Map` en memoria del proceso.
- Al reiniciar el servidor, el contador se pierde.
- No hay bloqueo de cuenta persistente.
- Los intentos fallidos se auditan como `login_failed`.

### 7.4 Matriz de permisos

Idéntica a la nativa en nombres y asignación:

| Rol | Permisos |
|---|---|
| `admin` | Los 14. |
| `contador` | Todos menos `users.manage`, `settings.manage`, `backup.manage`. |
| `capturista` | `tx.write`, `invoice.write`, `inventory.write`, `contacts.write`, `products.write`. |
| `consulta` | `tax.view`, `reports.view`. |

Las páginas y acciones usan `requireUser` / `requirePermission`. Algunas
lecturas solo exigen sesión, igual que en la app nativa.

### 7.5 Auditoría

`sessions` y `audit_log` se escriben en login, logout, setup, cambios y
operaciones de negocio. La bitácora se muestra en `/configuracion` (últimos 20
eventos) y en `/usuarios`.

---

## 8. Módulos y flujos

### 8.1 Panel

- Ingresos y egresos del mes.
- Saldo por cobrar.
- Valor de inventario.
- Nómina pagada.
- Gráfica mensual.
- Facturas recientes.
- Atajos a movimientos, facturas y reportes.

Comparte fórmulas con la app nativa, salvo diferencias de presentación. Para el
detalle exacto, véase [05](05-modulos-flujos.md); las fórmulas no cambiaron al
migrar.

### 8.2 Movimientos

- Ingreso o gasto.
- Fecha, categoría, contacto, cuenta.
- Subtotal, IVA y total en centavos.
- `iva_rate` configurable (0, 0.08, 0.16 u otro).
- Método y referencia.
- `source` (`manual`, `payroll`, `invoice`, etc.) y `source_id`.
- Borrado con auditoría.

### 8.3 Facturas

- Borrador → emitida → pagada.
- Cancelación con motivo.
- Folio `serie-número` de 4 dígitos.
- Conceptos con producto opcional, cantidad, precio, IVA y costo.
- Al emitir:
  - valida al menos un concepto;
  - valida stock de productos con `track_stock=1`;
  - asigna folio y actualiza `next_invoice_number`;
  - descuenta stock;
  - registra movimientos de inventario;
  - audita.
- Pagos parciales o totales; el estado pasa a `pagada` cuando `paid_cents >= total`.
- Borrado de pago y recálculo de estado.
- Cancelación: si estaba emitida, devuelve stock.
- **No es CFDI.** El formulario y la impresión lo dicen.

### 8.4 Contactos

- Cliente, proveedor o ambos.
- RFC, correo, teléfono, dirección, notas.
- Archivo lógico.
- Saldo por contacto calculado desde facturas emitidas.

### 8.5 Inventario

- Productos con SKU, unidad, precio, costo, IVA, stock y mínimo.
- `track_stock` para servicios.
- Movimientos: entrada, salida, ajuste.
- Movimiento con cuenta puede generar un gasto.
- Alertas de stock bajo.
- Exportación CSV.

### 8.6 Nómina

- Periodos mensuales o quincenales.
- Cálculo de base, percepciones extra, ISR, IMSS, Infonavit y otras
  deducciones.
- Pago con cuenta y fecha; genera un gasto con `source='payroll'`.
- Reversión y borrado.
- Edición manual de conceptos.

Las fórmulas de ISR/IMSS son estimaciones de control interno, igual que en la
app nativa.

### 8.7 Empleados

- Datos personales, RFC, CURP, NSS.
- Puesto, departamento, salario, fecha de inicio y fin.
- Estado activo/inactivo.
- Banco y cuenta.

### 8.8 Impuestos

- IVA trasladado por periodo.
- IVA de gastos.
- Diferencia.
- Vista anual por mes.
- Exportación CSV.

No calcula declaraciones ni presenta DIOT; es control interno.

### 8.9 Reportes

- Ingresos y egresos por categoría.
- Flujo de efectivo mensual.
- Antigüedad de saldos por cobrar.
- Desempeño mensual.
- Balances por cuenta.

### 8.10 Usuarios y auditoría

- Alta, edición, activación y reset de contraseña.
- Roles.
- Bitácora por acción, entidad y fecha.

### 8.11 Configuración

- Empresa: nombre, RFC, régimen, dirección, contacto, IVA, serie, folio, IMSS e
  ISR.
- Cuentas y categorías.
- Respaldo: crear, listar, descargar y borrar.
- Restauración: **manual**, reemplazando el archivo con la app cerrada.

### 8.12 Cuenta

- Cambio de la propia contraseña.
- Cierra las demás sesiones del usuario.

---

## 9. Impresión y exportación

### 9.1 Impresión

- Ruta `/imprimir/factura/[id]`.
- Renderiza empresa, cliente, folio, fechas, estado, conceptos, totales,
  pagos, notas y pie.
- El pie dice: “Documento interno de control. No es un CFDI ni un comprobante
  fiscal digital.”
- El botón **Imprimir** llama a `window.print()`.
- No hay PDF generado por el servidor.
- No hay impresión nativa: dependía del navegador.

### 9.2 Exportación

- `/api/exportar` produce CSV.
- Incluye BOM UTF-8.
- Importes con dos decimales.
- Se descarga por el navegador.
- No hay exportación a Excel, PDF ni JSON.

### 9.3 Diferencia con 2.0.0

La app nativa **no imprime facturas**; exporta CSV desde varias pantallas. Quien
necesite una representación impresa de una factura interna debe usar la versión
1.0.0 en un entorno aislado o generar el documento por fuera. Esta limitación
está documentada como hallazgo en [13](13-hallazgos-riesgos.md).

---

## 10. Respaldos y restauración

### 10.1 Crear

```text
data/respaldos/contabilidad-YYYY-MM-DD_HHMMSS.db
```

`src/lib/backups.ts` usa `backup(db, destino)` de `node:sqlite`, que produce una
copia consistente e incluye el WAL.

### 10.2 Listar y descargar

- La UI lista archivos `.db`.
- `/api/respaldo/[archivo]` permite descargarlos si el rol tiene
  `backup.manage`.
- `resolveBackup` valida el nombre con `^[A-Za-z0-9._-]+\.db$` y comprueba que
  la ruta resuelta esté dentro de `respaldos/`; evita traversal.

### 10.3 Borrar

- Acción `deleteBackupAction` con auditoría.
- Borra el archivo del disco.

### 10.4 Restaurar

**No hay restauración automática.** La UI indica:

```text
cierra la aplicación,
reemplaza data/contabilidad.db por el archivo descargado,
vuelve a iniciar.
```

No hay verificación de integridad, respaldo de seguridad automático ni
transacción. Es una operación manual y destructiva.

### 10.5 Relación con la app nativa

- La app nativa **no lista** los `.db` legados.
- El diálogo de restauración acepta `*.db`, pero `Database.RestoreFrom` exige
  SQLCipher y falla con un archivo plano.
- Los respaldos `.db` siguen en disco tras la actualización, pero no se pueden
  restaurar desde la UI/CLI nativas.
- La vía de recuperación es manual: copiar el `.db` a `contabilidad.db` con la
  app cerrada y dejar que el migrador lo convierta (destructivo).

Véase INS-06 en [08](08-operacion-instalador.md).

---

## 11. Instalador y build 1.0.0

### 11.1 `installer/libro.iss`

| Directiva | Valor | Nota |
|---|---|---|
| `AppId` | `{28083929-8EB6-45E7-A6CD-D27B813BCC34}` | **Compartido con 2.0.0**. |
| `AppVersion` | `1.0.0` | |
| `DefaultDirName` | `{localappdata}\Programs\Libro` | Misma carpeta. |
| `PrivilegesRequired` | `lowest` | Por usuario. |
| `OutputBaseFilename` | `Libro-Setup-1.0.0` | |
| `Compression` | `lzma2/max` | |
| `MinVersion` | `10.0` | |
| `[Files]` | `app\*` | Staging legado. |
| `[Dirs]` | `{localappdata}\Libro`, `...\data` | **Sin** `uninsneveruninstall`. |
| `[UninstallRun]` | `Libro.exe --stop --silent` | Puede colgar una desinstalación nativa. |
| `[Icons]` | Libro, **Detener Libro**, Carpeta de datos, Desinstalar | |
| `[Run]` | `Libro.exe` al terminar | |
| `CurUninstallStepChanged` | Pregunta si conservar datos | Si “No”, `DelTree` de `%LOCALAPPDATA%\Libro`. |

### 11.2 Lanzador

`installer/Launcher.cs` se compila con `csc.exe` de .NET Framework 4.8:

```text
/target:winexe /platform:x64 /win32icon:libro.ico
/reference:System.Windows.Forms.dll
/reference:System.Drawing.dll
/reference:System.Management.dll
/reference:System.Net.Http.dll
```

- Usa WinForms solo para mensajes.
- Usa WMI para verificar PIDs.
- Usa `HttpClient` para la sonda de salud.
- Abre el navegador con `UseShellExecute = true`.

### 11.3 `installer/build.ps1`

1. Verifica `csc.exe` e Inno Setup.
2. Verifica que el puerto 3000 esté libre.
3. `npm run build`.
4. Copia `.next/standalone` a `installer/app`.
5. Elimina `data/` y `*.db*` del staging.
6. Falla si encuentra `*.db`, `*.db-wal`, `*.db-shm` o una carpeta `data`.
7. Verifica `get-metadata-route.js`.
8. Copia `.next/static`.
9. Copia `node.exe`.
10. Genera `libro.ico` con `IconBuilder.cs`.
11. Compila `Launcher.cs` como `Libro.exe`.
12. Copia `start-server.cmd` y `LEEME.txt`.
13. Compila Inno.
14. Calcula SHA256 y lo escribe en `dist`.

La guardia del build legado es **más estricta** que la del build nativo: revisa
`*.db-wal` y `*.db-shm`, no solo `*.db` (compárese con INS-09).

### 11.4 Artefacto

| Archivo | Bytes | SHA256 |
|---|---:|---|
| `dist/Libro-Setup-1.0.0.exe` | 36,468,726 | `028BE5C570E98E60B3333C71DEA0CFE70F6C68EB2C530127A159D1F674032EFD` |
| `dist/Libro-Setup-1.0.0.exe.sha256` | 89 | — |

### 11.5 Staging

`installer/app/` **no existe** en este corte. El instalador legado no se puede
recompilar sin ejecutar `npm run build` y `installer/build.ps1`; el artefacto
`dist/Libro-Setup-1.0.0.exe` sí está conservado.

---

## 12. Seguridad y privacidad

### 12.1 Propiedades

| Control | Implementación | Evaluación |
|---|---|---|
| Hash de contraseña | scrypt con sal | Correcto. |
| Token de sesión | 32 bytes aleatorios; SHA-256 en base | Correcto. |
| Cookie | `HttpOnly`, `SameSite=Lax` | Correcto en lo básico. |
| `Secure` | Solo con `COOKIE_SECURE=1` | Limitado; HTTP local. |
| Path traversal en respaldos | Regex + resolución dentro de `respaldos/` | Correcto. |
| Permiso de descarga | `backup.manage` | Correcto. |
| Auditoría | `audit_log` | Correcto. |
| Cifrado en reposo | **Ninguno** | **Deficiente**. |
| Cifrado en tránsito | **Ninguno** (HTTP) | Aceptable solo en loopback. |
| Firma de código | **Ninguna** | Deficiente. |
| Pruebas | **Ninguna** | Deficiente. |
| CSRF | Sin token explícito; depende de `SameSite` y del origen de Next | Superficie reducida, no nula. |
| Exposición LAN | `HOSTNAME=0.0.0.0` manual | Riesgo alto si se usa. |

### 12.2 Riesgos principales

1. **Base y respaldos en texto plano.** Cualquier proceso o usuario con acceso a
   la carpeta puede leerlos.
2. **Servidor HTTP.** Aunque escuche en loopback, un malware local puede hablar
   con él; en LAN sin TLS, cualquiera en la red puede intentarlo.
3. **Cookie sin `Secure` por defecto.** En loopback no se puede exigir; en LAN
   sería un riesgo.
4. **Sesiones de 30 días.** Una cookie robada dura mucho.
5. **Bloqueo de login en memoria.** Un reinicio lo borra.
6. **Respaldos descargables.** Un usuario con `backup.manage` puede extraer toda
   la base en texto plano.
7. **Desinstalador destructivo.** La pregunta de conservar datos puede terminar
   en un borrado completo.
8. **Sin firma.** El instalador y el ejecutable no son verificables.
9. **Sin pruebas automatizadas.** No hay red de seguridad para regresiones.
10. **`AppId` compartido.** Un downgrade es peligroso.

### 12.3 Privacidad

La base contiene RFC, CURP, NSS, cuentas bancarias, ingresos, egresos y nómina.
En 1.0.0 todo eso estaba **sin cifrar** en disco. La app no tenía telemetría ni
envío de datos, pero el riesgo local era alto.

La migración a 2.0.0 cifra la base y la llave con DPAPI. Aun así, los respaldos
legados `.db` y la carpeta `backups/legacy-pre-native-20261023` siguen en texto
plano y deben tratarse como material sensible.

---

## 13. Diferencias con 2.0.0

| Aspecto | 1.0.0 | 2.0.0 |
|---|---|---|
| Runtime | Node.js / Next.js | .NET 8 / WPF |
| Interfaz | Navegador | Ventanas nativas |
| Servidor | HTTP en 3000 | No hay |
| Base | SQLite plano | SQLCipher |
| Llave | No | DPAPI `CurrentUser` |
| Sesión | Cookie 30 días | Memoria del proceso |
| Respaldos | `.db` plano | `.libro` cifrado |
| Restauración | Manual | UI/CLI con verificación |
| Impresión | Navegador | **No** |
| Exportación | API CSV | CSV desde UI/CLI |
| Instalador | `libro.iss` | `libro-native.iss` |
| `AppId` | `{28083929-...}` | El mismo |
| Datos | `%LOCALAPPDATA%\Libro\data` | La misma |
| Desinstalación | Pregunta y puede borrar datos | Nunca borra datos |
| `[UninstallRun]` | `--stop` | No existe |
| Firma | No | No |
| Pruebas | No | SelfTest + 11 E2E |
| Migración | No aplica | Texto plano → SQLCipher al primer arranque |

### 13.1 Lo que se reutilizó

- Esquema de 16 tablas.
- Roles y 14 permisos.
- Fórmulas de IVA, ISR, IMSS e Infonavit.
- Estados de factura y nómina.
- Folios y series.
- Concepto de auditoría.
- Estructura de reportes.

### 13.2 Lo que se corrigió

- Cifrado en reposo.
- Ausencia de servidor y navegador.
- Respaldos cifrados y restauración verificada.
- Instalador que no borra datos.
- Autoprueba.
- CLI para agentes.
- Separación `{app}` / `data`.

### 13.3 Lo que se perdió

- Impresión de facturas.
- Interfaz web y acceso desde otro dispositivo.
- Tema claro/oscuro.
- Descarga de respaldos por HTTP.
- Facilidad de modificar la UI con CSS.

---

## 14. Qué se conserva y qué no

| Elemento | ¿Se conserva? | Uso |
|---|---|---|
| `src/` | Sí | Referencia histórica. |
| `package.json` / lockfile | Sí | Reconstruir el legado si hiciera falta. |
| `next.config.ts`, `tsconfig.json`, `postcss.config.mjs` | Sí | Build legado. |
| `scripts/clean-standalone.mjs` | Sí | Build legado. |
| `installer/libro.iss` | Sí | Referencia del instalador 1.0.0. |
| `installer/build.ps1` | Sí | Build legado. |
| `installer/Launcher.cs` | Sí | Lanzador legado. |
| `installer/start-server.cmd` | Sí | Arranque legado. |
| `installer/IconBuilder.cs` | Sí | Generador del icono. |
| `installer/app/` | **No** | Staging ausente. |
| `dist/Libro-Setup-1.0.0.exe` | Sí | Artefacto histórico. |
| `data/` | Sí, pero **no distribuir** | Base legada real en texto plano. |
| `backups/legacy-pre-native-20261023/` | Sí, pero **no distribuir** | Copia previa a la migración. |
| `node_modules/`, `.next/` | Sí, generados | Rebuild local. |

---

## 15. Hallazgos del legado

Los identificadores `LEG-nn` son locales de este documento. El registro
consolidado y su equivalencia con `H-nn` están en
[13-hallazgos-riesgos.md](13-hallazgos-riesgos.md).

| ID | Severidad | Hallazgo | Evidencia | Impacto | Recomendación |
|---|---|---|---|---|---|
| LEG-01 | **Alta** | Base y respaldos en texto plano. | `src/lib/db.ts` no aplica cifrado; `backups.ts` copia `.db`. | Cualquiera con acceso al disco lee todo. | No usar 1.0.0 en producción; migrar y borrar copias planas. |
| LEG-02 | **Alta** | Servidor HTTP sin TLS y opción LAN. | `start-server.cmd` y `npm run start:lan`. | Exposición de credenciales y datos en la red. | Mantener loopback; no usar LAN sin TLS y control de red. |
| LEG-03 | **Alta** | El instalador comparte `AppId` con 2.0.0 y puede borrar datos al desinstalar. | `libro.iss` `AppId` y `CurUninstallStepChanged`. | Downgrade o desinstalación destructiva. | No instalar 1.0.0 sobre 2.0.0; conservar `LEEME.txt`. |
| LEG-04 | Media | Cookie sin `Secure` por defecto y sesión de 30 días. | `auth.ts`: `secure: process.env.COOKIE_SECURE === "1"`. | Mayor ventana de robo de sesión. | Usar TLS y `COOKIE_SECURE=1` si se expone. |
| LEG-05 | Media | Respaldos completos descargables por HTTP. | `/api/respaldo/[archivo]`. | Exfiltración por un usuario con `backup.manage`. | Limitar, auditar y cifrar el transporte. |
| LEG-06 | Media | Restauración manual sin verificación ni respaldo previo. | Texto de `/configuracion`. | Pérdida de datos por un reemplazo equivocado. | Restauración transaccional con verificación. |
| LEG-07 | Media | Bloqueo de login solo en memoria. | `globalThis.__loginAttempts`. | Un reinicio borra el bloqueo. | Persistir intentos o usar rate limit externo. |
| LEG-08 | Media | Sin pruebas automatizadas. | No hay archivos de test. | Regresiones no detectadas. | No volver a operar el legado; probar la app nativa. |
| LEG-09 | Media | Sin firma de código. | `build.ps1` no usa `SignTool`. | SmartScreen y suplantación. | Firmar si se redistribuye. |
| LEG-10 | Baja | `--stop` depende de WMI y de la línea de comandos. | `Launcher.cs` `KillNodeProcesses`. | Puede no detener el servidor en entornos restringidos. | Documentar el cierre manual. |
| LEG-11 | Baja | El log del servidor puede crecer sin rotación. | `start-server.cmd` redirige a `server.log`. | Consumo de disco. | Rotación o limpieza manual. |
| LEG-12 | Baja | No hay migraciones de esquema. | `schema.ts` + `CREATE TABLE IF NOT EXISTS`. | Una actualización no aplica columnas nuevas. | Migraciones versionadas (ya necesario en 2.0.0). |
| LEG-13 | Baja | El build legado exige `csc.exe` de .NET Framework y Node instalado. | `build.ps1`. | Rebuild no reproducible en cualquier equipo. | Documentar requisitos o archivar el artefacto. |
| LEG-14 | Baja | La impresión depende del navegador y del diálogo del sistema. | `print-button.tsx` → `window.print()`. | Resultado variable; no hay PDF. | Si se necesita impresión, implementarla en la app vigente. |

---

## 16. Cómo consultar el legado sin peligro

### 16.1 Solo lectura de código

No requiere ejecutar nada:

```powershell
Get-Content src\lib\schema.ts
Get-Content src\lib\invoices.ts
Get-Content installer\libro.iss
```

### 16.2 Ejecutarlo en un entorno aislado

Solo si es imprescindible:

1. Usa una máquina virtual o una cuenta de Windows desechable.
2. Copia el repositorio **sin** `data/`, `backups/` ni `%LOCALAPPDATA%\Libro`.
3. Define `DATA_DIR` a una carpeta temporal vacía.
4. No abras el puerto 3000 a la red.
5. No uses datos reales.
6. Al terminar, destruye la VM.

```powershell
$env:DATA_DIR = "$env:TEMP\libro-legacy-demo"
npm install
npm run dev
```

No ejecutes `installer\build.ps1` ni instales `Libro-Setup-1.0.0.exe` en el
equipo donde vive la app nativa.

### 16.3 Recuperar un respaldo legado

1. Conserva una copia del `.db` y del `contabilidad.db` actual.
2. Cierra la app nativa y el CLI.
3. Copia el `.db` legado como `%LOCALAPPDATA%\Libro\data\contabilidad.db`.
4. Abre la app nativa: detectará texto plano y migrará.
5. Verifica los datos y crea un respaldo `.libro`.
6. Conserva el `.db` original fuera de la carpeta activa.

Es una operación destructiva y manual; no hay botón “importar respaldo legado”.

---

## 17. Documentos relacionados

- [02 · Arquitectura](02-arquitectura.md): comparación de contenedores.
- [03 · Modelo de datos](03-modelo-datos.md): esquema que se heredó.
- [04 · Seguridad](04-seguridad.md): por qué se abandonó el texto plano.
- [05 · Módulos y flujos](05-modulos-flujos.md): reglas que se conservaron.
- [06 · Mapa de UI](06-mapa-ui.md): interfaz nativa que reemplazó al navegador.
- [07 · CLI](07-cli.md): automatización que no existía en 1.0.0.
- [08 · Operación e instalador](08-operacion-instalador.md): migración y
  diferencias de instalación.
- [10 · Inventario de archivos](10-inventario-archivos.md): ubicación de cada
  archivo legado.
- [13 · Hallazgos y riesgos](13-hallazgos-riesgos.md): consolidado.
- [14 · Decisiones](14-decisiones.md): por qué se reescribió en WPF.

---

## 18. Resumen para desambiguación

1. Si un documento dice “Next.js”, “navegador” o “puerto 3000”, habla de 1.0.0.
2. Si dice “WPF”, “SQLCipher”, “DPAPI” o “libro-cli”, habla de 2.0.0.
3. `src/` es legado; `desktop/` es vigente.
4. `installer/libro.iss` es legado; `installer/libro-native.iss` es vigente.
5. `dist/Libro-Setup-1.0.0.exe` es histórico; no se instala sobre 2.0.0.
6. La base plana se migra al primer arranque de 2.0.0; los respaldos `.db` no.
7. La impresión de facturas existía en 1.0.0 y **no** existe en 2.0.0.
8. Las facturas siempre fueron internas: **No es CFDI**.
