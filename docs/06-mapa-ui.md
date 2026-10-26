# 06 · Mapa de interfaz

> **Estado:** Vigente.
> **Corte:** 25 de octubre de 2026.
> **Ámbito:** shell, navegación, pantallas, ventanas modales, campos, botones,
> validaciones visibles, exportaciones y rarezas de la interfaz WPF de Libro
> 2.0.0.
> **Fuente de verdad:** `desktop/Libro.Desktop/*.xaml`, `Views/**/*.xaml`,
> `Views/**/*.xaml.cs`, `Controls/DateField.*`, `Converters/UiConverters.cs`,
> `Resources/Styles.xaml` y `Services/Permissions.cs`.
> **No cubre:** reglas de negocio y fórmulas ([05](05-modulos-flujos.md)),
> contratos del CLI ([07](07-cli.md)) ni columnas de la base
> ([03](03-modelo-datos.md)).

---

## 1. Shell de la aplicación

### 1.1 Arranque y elección de ventana

`App.OnStartup` decide la primera ventana antes de mostrar cualquier pantalla
principal:

| Condición | Ventana | Resultado |
|---|---|---|
| Argumento `--self-test` | ninguna | Ejecuta `SelfTest.Run()`, escribe resultado y termina. |
| `--dev` y `LIBRO_DEV=1` | `MainWindow` | Crea o entra con el usuario demo `admin@libro.local`; solo para desarrollo. |
| Base sin usuarios | `SetupWindow` | Si se cancela, la aplicación termina sin crear datos. |
| Base con usuarios | `LoginWindow` | Si se cancela, la aplicación termina. |
| Error de arranque | `MessageBox` | Muestra el error, sugiere conservar `clave.bin` y sale con código 1. |

`ShutdownMode` es `OnExplicitShutdown` durante el arranque y pasa a
`OnMainWindowClose` al mostrar `MainWindow`. Cerrar la ventana principal cierra
la aplicación.

### 1.2 Ventana principal

| Propiedad | Valor |
|---|---|
| Título | `Libro · Contabilidad` |
| Tamaño inicial | 1280 × 820; abre maximizada |
| Tamaño mínimo | 1080 × 680 |
| Distribución | Barra lateral fija de 246 px + área de contenido |
| Fondo | `CanvasBrush` (`#F5F5F1`) |
| Barra lateral | `SidebarBrush` (`#10231C`) |

La barra lateral contiene:

1. Marca “Libro” y subtítulo “Contabilidad para tu negocio”.
2. Lista de navegación (`NavList`), construida en código según permisos.
3. Tarjeta de usuario con iniciales, nombre, rol y botón de cerrar sesión
   (glifo `E7E8`, tooltip “Cerrar sesión”).

No hay barra superior, menú de archivo, impresión, preferencias de tema ni
indicador de estado de la base. La empresa activa solo aparece en el subtítulo
del Panel y en Configuración.

### 1.3 Navegación y visibilidad por rol

`MainWindow.BuildNavigation` arma la lista en este orden:

| Clave | Etiqueta | Permiso requerido | Visible para |
|---|---|---|---|
| `panel` | Panel | ninguno | Todos |
| `movimientos` | Movimientos | ninguno | Todos |
| `facturas` | Facturas | ninguno | Todos |
| `contactos` | Contactos | ninguno | Todos |
| `inventario` | Inventario | ninguno | Todos |
| `nomina` | Nómina | `payroll.view` | Admin, Contador |
| `empleados` | Empleados | `payroll.view` | Admin, Contador |
| `impuestos` | Impuestos | `tax.view` | Admin, Contador, Consulta |
| `reportes` | Reportes | `reports.view` | Admin, Contador, Consulta |
| `configuracion` | Configuración | ninguno | Todos, con campos y acciones limitados |
| `usuarios` | Usuarios | `users.manage` **o** `audit.view` | Admin; Contador solo Auditoría |

Consecuencias visibles:

- **Consulta** puede abrir Movimientos, Facturas, Contactos e Inventario, pero
  todas las acciones de escritura están ocultas. También ve Configuración en
  modo lectura y puede exportar reportes e IVA.
- **Contador** ve “Usuarios”, pero dentro solo existe la pestaña Auditoría.
- La etiqueta “Usuarios” no cambia aunque el usuario solo tenga `audit.view`.
- El módulo se reconstruye al iniciar sesión; no se actualiza si cambian los
  permisos del usuario en caliente.

### 1.4 Modelo de refresco

- Cada clic de navegación crea una **instancia nueva** de la vista
  (`new DashboardView()`, `new InvoicesView()`, etc.) y llama a `Refresh()` si
  implementa `IRefreshable`.
- Los filtros de la mayoría de las pantallas viven en la instancia; al salir y
  volver se pierden y se restablecen los valores por defecto.
- Los diálogos son modales (`ShowDialog`). Al devolver `true`, la pantalla que
  los abrió recarga su contenido; al devolver `false`, no hace nada.
- No hay refresco en segundo plano, ni temporizadores, ni notificaciones. Los
  cambios hechos por otro proceso no aparecen hasta navegar o filtrar de nuevo.
- `Panel` no se refresca al volver si la ventana ya está abierta: se crea una
  vista nueva cada vez que se selecciona “Panel”.

### 1.5 Sesión y cierre

El botón de cerrar sesión:

1. Pregunta “¿Quieres cerrar sesión?”.
2. Llama a `AppState.SignOut()`.
3. Abre `LoginWindow` como diálogo mientras la ventana principal sigue viva.
4. Si el login es correcto, crea otra `MainWindow`, la asigna a
   `Application.Current.MainWindow`, la muestra y cierra la anterior.
5. Si se cancela el login, cierra la aplicación.

No hay bloqueo automático por inactividad, cambio de usuario sin cerrar sesión
ni cierre por tiempo.

### 1.6 Modo desarrollo y autodiagnóstico

- `Libro.Desktop.exe --self-test` ejecuta pruebas internas de esquema,
  cifrado, migración y servicios, sin abrir interfaz.
- `--dev` solo funciona si además existe `LIBRO_DEV=1`; crea el negocio
  “Abarrotes La Esperanza” y el usuario `admin@libro.local` con contraseña
  `Secreta123!` si la base está vacía. **Nunca debe usarse en producción.**

### 1.7 Convenciones visuales y de captura

| Elemento | Convención |
|---|---|
| Dinero | Centavos enteros; se muestra con `Money.Format` en formato `es-MX`, dos decimales y símbolo `$`. |
| Captura de dinero | `Money.ParseAmount`: acepta `,` o `.` como decimal, quita símbolos y redondea a centavos. |
| Cantidades | Hasta tres decimales; `Money.ParseQty` redondea con `AwayFromZero`. |
| Fechas de negocio | Se guardan como `yyyy-MM-dd`. |
| Fechas visibles | `dd MMM yyyy` en español de México (por ejemplo “25 oct 2026”). |
| Fecha y hora | `dd MMM yyyy, HH:mm`, convertida a la zona de México. |
| Captura de fecha | `DateField`: `dd/MM/yyyy`, `d/M/yyyy` o `yyyy-MM-dd`; también acepta formatos que entienda `es-MX`. |
| IVA | Tasa `double` entre 0 y 1; se muestra como porcentaje sin decimales innecesarios. |
| Tablas | `DataGrid` de solo lectura con filas de 38 px; selección y doble clic. |
| Confirmaciones | `MessageBox` con `Sí/No`; las acciones destructivas usan icono de advertencia. |
| Errores de formulario | Texto rojo `ErrorText` dentro del diálogo, no un `MessageBox`. |
| Errores de carga | `MessageBox` con el mensaje de la excepción y registro en `AppLog`. |
| Estados vacíos | Panel centrado con icono, título, explicación y, si aplica, botón de creación. |

Paleta principal (no es un contrato funcional, pero explica la identidad
visual): canvas `#F5F5F1`, superficie `#FFFFFF`, tinta `#171A1C`, verde de
acento `#0F6B4F`, barra lateral `#10231C`, peligro `#B42318`, éxito `#067647`,
advertencia `#B54708`, información `#175CD3`, gasto `#C2410C`.

### 1.8 Comportamiento de `DateField`

- El texto se confirma con **Enter** o al perder el foco.
- El botón de calendario abre un `Popup` con calendario; al elegir un día se
  cierra y actualiza el texto.
- Si el texto está vacío, borra la fecha seleccionada.
- Si el texto no se puede interpretar, **revierte en silencio** al último valor
  válido; no muestra error.
- El control no tiene `IsReadOnly`; los formularios que lo usan como filtro
  permiten escribir, pero la consulta solo toma `SelectedDate`.
- No hay validación de rango de fechas en el control; cada pantalla decide si
  intercambia `desde/hasta` (Reportes) o no.

### 1.9 Teclado y accesibilidad

| Interacción | Dónde |
|---|---|
| `Enter` | Inicia sesión, crea la cuenta inicial, guarda movimiento, confirma `DateField`, aplica búsqueda en Inventario/Empleados. |
| `Esc` | Cierra diálogos con botón `IsCancel` (cancelar, cerrar, salir sin guardar). |
| Doble clic | Edita movimiento manual, abre factura, edita contacto, abre producto, edita empleado, abre nómina. |
| `Tab` | Orden automático de WPF; no hay orden explícito ni atajos globales. |

No se declaran `AutomationProperties`, textos alternativos para iconos ni
etiquetas asociadas con `Label.Target`. Los glifos usan la fuente de iconos
`Segoe Fluent Icons`; su significado depende del `ToolTip` o del contexto.

---

## 2. Pantallas principales

### 2.1 Configuración inicial (`SetupWindow`)

| Propiedad | Valor |
|---|---|
| Título | `Libro · Configuración inicial` |
| Tamaño | 980 × 640, no redimensionable |
| Distribución | Columna izquierda oscura de marca; columna derecha de formulario |

**Columna izquierda:** logotipo, “Libro”, “Contabilidad para tu negocio”,
“Tu contabilidad, ordenada desde el primer día”, tres viñetas (“Datos guardados
en tu computadora”, “Interfaz nativa de Windows”, “Base y respaldos cifrados”) y
la nota de protección automática: la llave queda ligada a la cuenta de Windows
y sin ella la base no se abre en otra computadora.

**Formulario:**

| Campo | Control | Obligatorio |
|---|---|---|
| Nombre del negocio | `CompanyBox` | Sí |
| Tu nombre | `NameBox` | Sí |
| Correo | `EmailBox` | Sí, formato válido |
| Contraseña | `PasswordBox` | Sí, mínimo 8 caracteres |
| Confirmar contraseña | `ConfirmBox` | Sí, debe coincidir |

Botón principal: **“Crear cuenta y entrar”** (`IsDefault`). Nota inferior: “Al
crear la cuenta se genera una llave de cifrado aleatoria protegida por
Windows.”

Validaciones visibles, en orden:

1. “Escribe el nombre de tu negocio.”
2. “Escribe tu nombre.”
3. Error de `Validation.ValidateEmail` (correo vacío o inválido).
4. Error de `Validation.ValidatePassword` (mínimo 8 caracteres).
5. “Las contraseñas no coinciden.”
6. Cualquier excepción de `AuthService.Setup` se muestra en `ErrorText`.

No se piden RFC, régimen fiscal, dirección, serie ni folio. Esos datos se
capturan después en Configuración. Al crear, se siembran las 13 categorías y
las cuentas `Caja` y `Banco`, y se entra directo a `MainWindow`.

### 2.2 Inicio de sesión (`LoginWindow`)

| Propiedad | Valor |
|---|---|
| Título | `Libro · Entrar` |
| Tamaño | 920 × 580, no redimensionable |
| Distribución | Columna izquierda de marca; derecha con formulario |

**Izquierda:** marca, “Todo tu negocio, en orden.”, texto de valor, nombre de la
empresa (`CompanyNameText`, o “Mi negocio” si está vacío) y “Los datos están
cifrados y protegidos por tu cuenta de Windows.”

**Derecha:** “Entra a tu contabilidad”, “Usa la cuenta que creaste al configurar
el sistema.”, campos **Correo** y **Contraseña**, botón **“Entrar”**
(`IsDefault`) y la ayuda “¿Olvidaste tu contraseña? Pídele al administrador que
la restablezca desde Usuarios.”

Validaciones:

1. “Escribe tu correo.”
2. “Escribe tu contraseña.”
3. Error de `AuthService.Login` (credenciales, usuario inactivo, etc.).
4. “No se pudo iniciar sesión: …” ante excepción.

Al fallar, limpia la contraseña y devuelve el foco al campo. El botón se
deshabilita mientras autentica. No hay “recordarme”, recuperación por correo,
captcha ni segundo factor.

### 2.3 Panel (`DashboardView`)

**Cabecera:** título “Panel”; subtítulo dinámico
“Resumen de {mes} de {año} · {empresa}”. Botones **“Nueva factura”** (secundario)
y **“Nuevo movimiento”** (primario). Ambos **navegan** a la pantalla
correspondiente; no abren el diálogo directamente.

**Indicadores (dos filas de cuatro tarjetas):**

| Fila | Tarjeta | Dato | Nota visible |
|---|---|---|---|
| 1 | Ingresos del mes | Facturado + otros ingresos, sin IVA | “Facturado + otros ingresos” |
| 1 | Gastos del mes | Gastos y compras, sin IVA | “Compras y gastos deducibles” |
| 1 | Utilidad del mes | Ingresos − gastos | “Ingresos menos gastos”; verde si ≥ 0, rojo si < 0 |
| 1 | Efectivo y bancos | Suma de saldos de cuentas activas | “Saldo de tus cuentas” |
| 2 | IVA trasladado | IVA de facturas emitidas/pagadas + otros ingresos | “Cobrado en ventas” (etiqueta imprecisa; ver hallazgos) |
| 2 | IVA acreditable | IVA de gastos | “Pagado en gastos” |
| 2 | Por cobrar | Saldo de facturas emitidas | Texto de vencidas o “Sin facturas vencidas”; insignia cambia a “Requiere atención” / “Al día” |
| 2 | Inventario | Stock × costo promedio | “Valor al costo” |

**“Últimos movimientos”:** tabla de hasta 5 movimientos, columnas Fecha,
Descripción, Categoría y Monto (coloreado por tipo). Botón “Ver todos”.

**“Facturas recientes”:** hasta 5 facturas, columnas Folio, Cliente, Fecha,
Total y Estado. Botón “Ver todas”. Subtítulo: “Documentos internos · No son
CFDI”.

**“Últimos 6 meses”:** barras verticales de ingresos (verde) y gastos (naranja)
por mes, normalizadas al máximo del periodo; etiquetas de mes y leyenda.

**“Cobros”:** saldo por cobrar y una insignia de vencimiento.

**“Stock bajo”:** hasta 5 productos activos con `stock <= min_stock`, ordenados
por diferencia ascendente; muestra nombre, existencias y píldora “Bajo”. Si no
hay, muestra “Todo el inventario está en orden.”

**Rarezas:**

- Los botones de creación y los enlaces “Ver todos” están visibles para todos
  los roles, incluido Consulta; al navegar, la pantalla de destino oculta las
  acciones de escritura.
- Si falla la carga, solo cambia el subtítulo a “No se pudo cargar el resumen.
  Revisa el registro de la aplicación.”; las tarjetas conservan sus valores
  anteriores o quedan vacías.
- La definición de “Por cobrar” del Panel no es idéntica a la de Facturas
  (ver [05](05-modulos-flujos.md)).

### 2.4 Movimientos (`TransactionsView`)

**Cabecera:** “Movimientos”; subtítulo “Ingresos no facturados y gastos. Las
ventas con factura viven en Facturas para no duplicar.” Botón **“Nuevo
movimiento”**.

**Indicadores:** Ingresos, Gastos, Neto e IVA neto, todos sin IVA salvo el
último. El Neto se pinta verde/rojo; el IVA neto se pinta ámbar si es positivo.

**Filtros:**

- `Desde` y `Hasta` con `DateField`; por defecto el mes actual.
- `Tipo`: Todos, Ingresos, Gastos.
- `Buscar`: descripción, referencia o contacto.
- Botón `Filtrar`.
- Segunda fila: `Categoría` (incluye archivadas) y `Limpiar filtros`.
- Atajos “Este mes” y “Ver todo” (limpia fechas, tipo, categoría y búsqueda).

**Tabla:** Fecha, Descripción (con píldora de origen si es automático), Contacto,
Cuenta, Tipo, Subtotal, IVA, Total y una columna final de acciones. En filas
manuales aparecen lápiz (editar) y basura (eliminar); en filas automáticas
aparece el origen (“Nómina”, “Inventario”, “Factura”, “Pago” o “Automático”) y
no se pueden editar ni borrar.

**Comportamiento:**

- Carga hasta 200 filas; el pie muestra “N movimientos · mostrando 200” cuando
  hay más.
- El pie también muestra “Neto: …”.
- El resumen (`GetTotals`) se calcula sobre todos los filtros, no solo las 200
  filas visibles.
- Doble clic en una fila manual abre el diálogo de edición; en una automática no
  hace nada.
- Eliminar pide confirmación con descripción e importe y advierte que no se
  puede deshacer.
- Si no hay filas, se oculta la tabla y aparece el estado vacío “Sin movimientos
  en este periodo” con botón “Nuevo movimiento”.
- Los movimientos automáticos se crean por facturas, pagos, inventario y nómina;
  el origen se muestra, pero no se puede abrir desde aquí.

### 2.5 Facturas (`InvoicesView`)

**Cabecera:** “Facturas”; subtítulo “Controla lo que facturas, lo que te deben y
los pagos recibidos. Documento interno; no genera CFDI.” Botón **“Nueva
factura”** (oculto sin `invoice.write`).

**Indicadores:**

| Tarjeta | Contenido |
|---|---|
| Por cobrar | Saldo total de facturas emitidas |
| Vencido | Saldo de facturas emitidas con vencimiento anterior a hoy; rojo si > 0; texto “N factura(s) vencida(s)” |
| Emitido este mes | Total emitido en el mes; texto “N factura(s)” |
| Borradores | Cantidad; nota “Aún no afectan reportes” |

**Filtros:** búsqueda por cliente, folio o nota; estado (Todas, Borradores,
Emitidas, Pagadas, Canceladas); botones Filtrar y Limpiar. **No hay filtro por
fechas** ni por cliente.

**Tabla:** Folio, Fecha, Cliente, Vence, Total, Pagado, Saldo, Estado y botón
“Ver”. El estado se muestra como Borrador, Emitida, Pagada, Cancelada o
**Vencida**. “Vencida” no es un estado almacenado: un disparador de
`IsOverdue` reemplaza la etiqueta “Emitida” cuando la factura tiene saldo y su
vencimiento ya pasó.

**Comportamiento:**

- Carga hasta 200 facturas; el pie muestra el total y, si aplica, “mostrando
  200”.
- El pie muestra “Saldo listado: …”, suma de los saldos de las filas
  cargadas, no del universo filtrado.
- Doble clic o “Ver” abre `InvoiceDetailWindow`; al cerrar, recarga la lista.
- No hay impresión, PDF, envío por correo, copia de factura ni integración
  fiscal. El texto “No es CFDI” es explícito en la cabecera y en el detalle.
- Si no hay clientes, el diálogo de nueva factura queda bloqueado (ver 3.3).

### 2.6 Contactos (`ContactsView`)

**Cabecera:** “Contactos”; subtítulo “Clientes y proveedores con su RFC, datos
de contacto y saldo pendiente.” Botón **“Nuevo contacto”**.

**Indicadores:** Clientes activos, Proveedores activos, Por cobrar (facturas
emitidas) y Archivados.

**Filtros:** búsqueda por nombre, RFC o correo; tipo (Todos, Clientes,
Proveedores); casilla “Incluir archivados”; botón Filtrar. No hay filtro por
fecha ni por saldo.

**Tabla:** Nombre, Tipo, RFC, Contacto (correo/teléfono), Por cobrar, Estado y
acciones “Editar” / “Archivar” o “Reactivar”. El estado muestra “Activo” o
“Archivado” con píldora.

**Comportamiento:**

- Los contactos archivados no aparecen por defecto ni en los selectores de
  facturas nuevas.
- Archivar pide confirmación y conserva historial; reactivar también confirma.
- Doble clic abre edición; todas las acciones respetan `contacts.write`.
- El saldo por cobrar del contacto se calcula con facturas emitidas, no con
  pagos pendientes de registrar.

### 2.7 Inventario (`InventoryView`)

**Cabecera:** “Inventario”; subtítulo “Productos, existencias y costo promedio.
El stock se mueve con entradas, salidas y ajustes.” Botones **“Exportar CSV”**
y **“Nuevo producto”**.

**Indicadores:** Productos activos, Valor del inventario (stock × costo
promedio), Bajo stock (en o bajo el mínimo) y Agotados (stock ≤ 0).

**Filtros:** búsqueda por nombre o SKU; “Solo bajo stock”; “Incluir inactivos”;
Filtrar y Limpiar. Enter en la búsqueda aplica el filtro.

**Tabla:** Producto, SKU, Stock, Mínimo, Costo, Precio, Valor, Estado, botón
“Ver” y, si hay permiso, “Editar” y “Desactivar/Activar”. El estado se calcula
como Disponible, Bajo stock, Agotado, Sin stock (sin control) o Inactivo.

**Comportamiento:**

- El permiso de escritura es `inventory.write` **o** `products.write`; un rol
  con solo uno de ellos ve las acciones, pero el servicio vuelve a validar la
  operación concreta.
- Desactivar pide confirmación y el producto deja de aparecer en facturas
  nuevas; no borra historial.
- El botón de alternar dice siempre “Desactivar”, incluso en productos
  inactivos (defecto visual; la acción sí reactiva).
- “Ver” abre `ProductDetailWindow`; al cerrar, recarga la lista.
- Exportar CSV usa las filas actualmente filtradas, no todo el catálogo.

### 2.8 Nómina (`PayrollView`)

**Cabecera:** “Nómina”; subtítulo “Periodos, ISR, IMSS y pago de sueldos. Los
cálculos son una referencia editable.” Botón **“Nueva nómina”**.

**Indicadores:** En plantilla (empleados activos), Nóminas en borrador (cantidad
y neto por pagar), Pagado este mes (importe y cantidad de nóminas).

**Tabla:** Periodo, Tipo (Mensual/Quincenal), Empleados, Deducciones, Neto,
Estado (Borrador/Pagada), botón “Ver” y “Eliminar”. No muestra percepciones ni
ISR/IMSS desglosados en la lista.

**Comportamiento:**

- Cargar la pantalla recalcula el resumen, no las nóminas.
- “Nueva nómina” abre `PayrollPeriodDialog`; al crearla, abre de inmediato el
  detalle del periodo.
- “Eliminar” pide confirmación y advierte que borra los conceptos. Si la nómina
  está pagada, el servicio rechaza la operación con “No se puede eliminar una
  nómina pagada. Revierte el pago primero.”
- Doble clic abre el detalle.
- No hay exportación de nómina, CFDI de nómina, timbrado ni cálculo de cuotas
  obrero-patronales completas.

### 2.9 Empleados (`EmployeesView`)

**Cabecera:** “Empleados”; subtítulo “Plantilla, datos fiscales y sueldos para el
cálculo de la nómina.” Botón **“Nuevo empleado”**.

**Indicadores:** Empleados activos, Nómina mensual (suma de sueldos base),
Inactivos (histórico).

**Filtros:** búsqueda por nombre, puesto, RFC o departamento; “Incluir
inactivos”; Filtrar y Limpiar. Enter en la búsqueda aplica el filtro.

**Tabla:** Empleado, Puesto, Departamento, Ingreso, Sueldo, Estado y acciones
“Editar” / “Baja”. El botón de baja dice siempre “Baja”, incluso para empleados
inactivos; la acción sí reactiva.

**Comportamiento:**

- Dar de baja pide confirmación y conserva el historial de nómina.
- La baja cambia `status` pero **no** escribe `end_date`; la fecha de baja debe
  capturarse a mano en el diálogo.
- Doble clic abre edición.
- No hay importación de plantilla ni cálculo de antigüedad.

### 2.10 Impuestos (`TaxesView`)

**Cabecera:** “Impuestos”; subtítulo “IVA trasladado, acreditable y neto por
mes. Cifras de trabajo, no un acuse fiscal.” Botón **“Exportar CSV”**.

**Filtros:** Año (texto, 2000–2200), Mes (combo de enero a diciembre),
“Consultar” y “Mes actual”. Al entrar, selecciona el mes y año actuales.

**Indicadores:** IVA trasladado (“Cobrado en ventas”), IVA acreditable (“Pagado
en gastos”), IVA neto (“Trasladado menos acreditable”) y Facturas del mes
(“Emitidas y pagadas”).

**Detalle:**

- “Ventas facturadas”: subtotal e IVA del mes.
- “Otros ingresos”: subtotal e IVA.
- “Gastos con IVA”: subtotal e IVA.
- Tabla “Detalle de ingresos”: Tipo, Folio, Fecha, Cliente, Detalle, Subtotal,
  IVA, Total.
- Tabla “IVA del año”: Mes, IVA trasladado, IVA acreditable, IVA neto.
- Pie: “Acumulado {año}: trasladado … · acreditable … · neto …”.

**Comportamiento:**

- El IVA trasladado se calcula con facturas emitidas/pagadas del periodo, no
  con pagos cobrados; la etiqueta “Cobrado en ventas” es imprecisa.
- El IVA neto se pinta verde si es negativo (saldo a favor) y acento si es
  positivo.
- Exportar sin haber consultado muestra “Primero consulta un periodo.”
- No hay declaración, acuse, DIOT, retenciones ni cálculo de ISR mensual.

### 2.11 Reportes (`ReportsView`)

**Cabecera:** “Reportes”; subtítulo “Estado de resultados, flujo de efectivo y
antigüedad de saldos. Resultados sin IVA; flujo con IVA.” Botones **“Movimientos
CSV”** y **“Facturas CSV”**.

**Filtros:** Desde/Hasta con `DateField`; por defecto del 1 de enero al 31 de
diciembre del año actual. Botones Aplicar, Este mes y Año. Si `desde > hasta`,
los intercambia en silencio.

**Indicadores:** Ingresos (“Facturado + otros, sin IVA”), Gastos (“Sin IVA”),
Utilidad operativa (“Ingresos menos gastos”) y Flujo neto (“Cobros + ingresos −
gastos”). La utilidad y el flujo se pintan rojos si son negativos.

**Secciones:**

1. **Estado de resultados:** ventas facturadas, otros ingresos, total de
   ingresos, total de gastos y utilidad operativa.
2. **Referencia de costo de ventas:** costo de productos facturados y margen
   bruto de referencia; nota de que usa el costo promedio al facturar y no
   descuenta compras no vendidas.
3. **Ingresos vs. gastos:** tabla mensual con Mes, Ingresos, Gastos y Utilidad;
   nota de que la utilidad mensual puede incluir compras de inventario.
4. **Gastos por categoría:** barras horizontales normalizadas al gasto mayor;
   “Sin gastos en el periodo.” si no hay.
5. **Antigüedad de saldos:** cubetas (por ejemplo “Al día”, “1–30”, “31–60”,
   “61–90”, “+90”), total por cobrar y tabla de facturas con saldo: Folio,
   Cliente, Vence, Estado (etiqueta de vencimiento) y Saldo.
6. **Saldos de cuentas:** lista de cuentas con saldo calculado.
7. **Inventario:** valor, productos activos y “bajo stock / agotados”.

**Comportamiento:**

- El flujo de efectivo es el único bloque que incluye IVA; el resto es base sin
  IVA.
- La antigüedad usa todas las facturas emitidas con saldo, no solo el rango de
  fechas seleccionado.
- Los CSV usan el rango de fechas seleccionado y hasta 10,000 filas.
- No hay exportación PDF, Excel ni gráficas imprimibles.

### 2.12 Configuración (`SettingsView`)

**Cabecera:** “Configuración”; subtítulo “Datos del negocio, cuentas,
categorías, impuestos y respaldos cifrados.”

Navegación lateral con cuatro secciones:

| Sección | Tag | Visible |
|---|---|---|
| Negocio e impuestos | `negocio` | Siempre |
| Cuentas de efectivo | `cuentas` | Siempre |
| Categorías | `categorias` | Siempre |
| Respaldos | `respaldos` | Solo `backup.manage` |

Permisos:

- `settings.manage`: edita empresa, impuestos, ISR y guarda.
- `settings.manage` **o** `accounts.write`: crea/edita/activa cuentas y
  categorías.
- `backup.manage`: crea, guarda, restaura y lista respaldos.
- Sin `settings.manage`, todos los campos de empresa e ISR son de solo lectura y
  los botones Guardar/Restaurar ISR están ocultos.

#### Negocio e impuestos

Campos: Nombre del negocio, RFC, Régimen fiscal, Teléfono, Correo, IVA (%),
Dirección, Serie, Siguiente folio, IMSS (%) y un `Expander` “Tarifas de ISR
(JSON avanzado)”.

Notas visibles:

- “Aparecen en facturas y reportes internos.”
- “Las facturas son documentos internos y no se timbran ante el SAT (No es
  CFDI).”
- “El IMSS se calcula como porcentaje de las percepciones. Las tarifas de ISR
  son una referencia editable; confirma con tu contador.”

Acciones: “Restaurar tarifas de ISR de referencia” y “Guardar configuración”.

Validaciones:

1. “Escribe el nombre del negocio.”
2. “El IVA debe ser un porcentaje entre 0 y 100.”
3. “El IMSS debe ser un porcentaje entre 0 y 100.”
4. “El siguiente folio debe ser un número mayor a cero.”
5. “La serie debe tener de 1 a 10 letras o números, sin espacios.”
6. “El RFC debe tener 12 o 13 caracteres (puedes dejarlo vacío).”
7. Errores de ISR: JSON inválido, lista vacía, límites negativos, tasa fuera de
   0..1, límite superior ≤ inferior, más de una tarifa sin límite superior o
   última tarifa con límite superior no nulo.
8. “Configuración guardada.” en verde, o el mensaje de la excepción en rojo.

Al guardar se actualiza `AppState.Company` y el nombre de la empresa en la barra
lateral. La serie se convierte a mayúsculas. El “siguiente folio” no se
recalcula automáticamente al emitir; el servicio lo incrementa.

#### Cuentas de efectivo

Tabla: Cuenta, Tipo, Saldo inicial, Saldo actual, Estado y acciones Editar /
Desactivar o Activar. Botón “Nueva cuenta”. Nota: “La cuenta se usa en
movimientos, cobros y pagos de nómina.”

Defectos conocidos:

- El diálogo ofrece el tipo **“Otro”**, pero el `CHECK` de la base solo admite
  `efectivo`, `banco` y `tarjeta`; guardar “Otro” falla con el error de
  restricción de SQLite.
- En una cuenta nueva, la casilla “Cuenta activa” se ignora: el servicio inserta
  siempre `active = 1`. Solo al editar se respeta.

#### Categorías

Tabla: Categoría, Tipo (Ingreso/Gasto), Estado (Activa/Archivada) y acciones
Editar / Archivar o Restaurar. Casilla “Incluir archivadas”; botón “Nueva
categoría”. Nota: “Clasifican ingresos y gastos. Puedes archivar sin borrar
historial.”

Defecto: en una categoría nueva, la casilla “Archivada” se ignora; el servicio
inserta siempre `archived = 0`. Solo al editar se respeta.

#### Respaldos

- Campo de solo lectura con la carpeta (`%LOCALAPPDATA%\Libro\data\respaldos`).
- “Crear respaldo ahora”.
- “Guardar copia en…”. Abre `SaveFileDialog` con filtro `*.libro`.
- “Abrir carpeta”.
- Tabla “Respaldos disponibles”: Archivo, Fecha, Tamaño y botón “Restaurar”.
- “Restaurar desde archivo…”. Acepta `*.libro` y `*.db`.

Restaurar muestra una advertencia: reemplaza **todos** los datos, crea un
respaldo de seguridad y cierra Libro al terminar. Si falla, la base actual se
conserva. No hay restauración selectiva, ni copia a la nube, ni cifrado
adicional distinto de SQLCipher/DPAPI.

### 2.13 Usuarios y auditoría (`UsersView`)

**Cabecera:** “Usuarios y auditoría”; subtítulo “Cuentas, roles y registro de
cambios. Solo los administradores pueden modificar usuarios.”

Pestañas: **Usuarios** (solo `users.manage`) y **Auditoría** (solo
`audit.view`). Si el usuario solo tiene auditoría, la pestaña Usuarios no existe
y la pantalla abre directamente en Auditoría.

#### Usuarios

Indicadores: Usuarios registrados, Activos y Administradores. Botón “Nuevo
usuario”.

Tabla: Usuario (nombre y correo), Rol, Estado, Último acceso y acciones Editar /
Contraseña / “Activar”. El botón dice siempre “Activar”, incluso para usuarios
activos; la acción real alterna según el estado.

Acciones:

- Nuevo usuario: nombre, correo, rol, activo, contraseña inicial y confirmación.
- Editar: nombre, correo, rol y activo; no cambia contraseña.
- Contraseña: abre `PasswordResetDialog`.
- Activar/Desactivar: confirma; no permite desactivarse a sí mismo ni dejar la
  aplicación sin administradores activos.

El resumen de roles se muestra como “Administrador, Contador, Capturista y
Consulta”. El rol por defecto de un usuario nuevo es **Capturista**.

#### Auditoría

Filtros: búsqueda por usuario, detalle o entidad; Acción; Entidad; Desde y
Hasta; botones Buscar y Limpiar. No hay paginación visible: se cargan todas las
entradas que devuelve el servicio.

Tabla: Fecha, Usuario, Acción, Entidad y Detalle. Las acciones y entidades se
traducen al español con `AuditText`; los importes guardados como centavos se
formatean como dinero. Si no hay usuario, se muestra “Sistema”.

El catálogo de acciones incluye configuración, creación, actualización,
eliminación, archivo, restauración, activación, desactivación, cambio de
contraseña, inicio/cierre de sesión, respaldo, emisión, cancelación, pagos,
pago/revierte de nómina, recálculo, entradas, salidas, ajustes, ventas y
compras.

---

## 3. Diálogos y ventanas de detalle

Todos los diálogos son modales respecto de su ventana propietaria, se centran
en ella y no aparecen en la barra de tareas (`ShowInTaskbar="False"`), salvo las
ventanas de detalle que sí son ventanas normales.

### 3.1 Movimiento (`TransactionDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Movimiento · Libro` |
| Tamaño | 720 × 900; mínimo 680 × 720 |
| Redimensionable | No |

**Campos:**

- Tipo: radios **Ingreso** / **Gasto** (por defecto Gasto).
- Fecha (`DateField`, por defecto hoy).
- Categoría (filtrada por tipo; archivadas solo al editar).
- Descripción (obligatoria).
- Contacto (opcional; “Sin contacto”).
- Cuenta de efectivo (obligatoria; solo activas, incluye la inactiva si el
  movimiento ya la usaba).
- Método: Transferencia, Efectivo, Tarjeta, Cheque, Domiciliación, Otro.
- Referencia.
- Subtotal sin IVA, IVA (16%, 8% frontera, 0% exento o la tasa guardada).
- IVA calculado y Total calculado en vivo.
- Botones Cancelar / Guardar movimiento (o Guardar cambios).

**Validaciones, en orden:** fecha, monto > 0, descripción, categoría, cuenta.
Los mensajes se muestran en `ErrorText`; el constructor los escribe con
**mojibake** (“descripciÃ³n”, “categorÃ­a”), igual que el subtítulo
“facturaciÃ³n” y la opción “DomiciliaciÃ³n”. Es un defecto visible de codificación.

Al cambiar el tipo se recargan las categorías y puede quedar sin selección. El
IVA se recalcula con `Math.Round(subtotal * tasa, AwayFromZero)`. Al guardar, el
servicio vuelve a calcular IVA y total; no se confía en los valores mostrados.

Solo se editan movimientos con `source = 'manual'`. Los automáticos se abren en
modo lectura desde la lista, pero el diálogo ni siquiera se lanza.

### 3.2 Contacto (`ContactDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Contacto · Libro` |
| Tamaño | 660 × 690 |
| Redimensionable | No |

Campos: Tipo (Cliente, Proveedor, Cliente y proveedor), Nombre o razón social,
RFC, Correo, Teléfono, Dirección y Notas. El RFC se guarda en mayúsculas.

Validaciones del servicio: nombre obligatorio, tipo válido, correo válido si se
captura, RFC ≤ 20, teléfono ≤ 60, dirección ≤ 500, notas ≤ 1000. No se valida
la estructura del RFC mexicano ni se consulta el SAT.

### 3.3 Factura (`InvoiceDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Factura · Libro` |
| Tamaño | 1180 × 900; mínimo 1040 × 760 |
| Redimensionable | Sí |

**Cabecera:** “Nueva factura” o “Editar borrador”; subtítulo “Documento interno
de control. No es CFDI ni comprobante fiscal.” Insignia “No es CFDI”.

**Datos:** Cliente (solo clientes o “ambos” activos; al editar incluye
archivados y el cliente actual), Fecha de emisión (hoy), Vencimiento (hoy + 30),
Condiciones de pago (“Pago a 30 días”) y Notas.

**Conceptos:** tabla editable con Producto, Descripción, Cant., Precio, IVA e
Importe. El producto “Sin producto / servicio libre” tiene Id 0. Al elegir un
producto se copian nombre, precio y tasa; la cantidad inicial es 1. Se pueden
agregar y quitar líneas; si se quita la última, se agrega una en blanco.

**Resumen:** Subtotal, IVA y Total en vivo. Nota: “Al emitir se asigna folio y
se descuenta el inventario de los productos incluidos. Los borradores no
afectan reportes.”

**Acciones:** “Emitir factura” (o “Guardar y emitir”), “Guardar borrador” (o
“Guardar cambios”) y Cancelar. Si no hay clientes, ambos botones de guardado se
deshabilitan y aparece “No hay clientes registrados. Crea un contacto de tipo
cliente antes de facturar.”

**Validaciones de UI:** cliente, fechas, al menos un concepto. **Validaciones
del servicio:** fechas ISO válidas, vencimiento no anterior a emisión, una
descripción por línea, cantidad > 0 y ≤ 1,000,000, precio ≥ 0, tasa 0..1,
cliente existente y límite de conceptos.

No se valida stock antes de emitir; si falta inventario, la emisión falla y el
error se muestra en `ErrorText`. No se puede crear un cliente desde este
diálogo. No hay impresión ni vista previa.

### 3.4 Detalle de factura (`InvoiceDetailWindow`)

| Propiedad | Valor |
|---|---|
| Título | `{folio} · Libro` |
| Tamaño | 1040 × 880 |
| Redimensionable | Sí (sin mínimo explícito) |

**Cabecera:** folio, cliente y fecha; botón Cerrar.

**Columna izquierda:** cliente y RFC (o “Sin RFC registrado”), emisión,
vencimiento, condiciones, usuario que registró, notas (si existen), conceptos
(Descripción, Cant., Precio, IVA, Importe) y pagos recibidos (Fecha, Método,
Cuenta, Referencia, Monto y botón de eliminar). Si no hay pagos: “Sin pagos
registrados.”

**Columna derecha:** totales (Subtotal, IVA, Total, Pagado, Saldo), aviso “No es
un CFDI”, panel de borrador, panel de cobranza, panel de cancelación y nota de
pagos.

Reglas de visibilidad:

| Panel | Se muestra cuando |
|---|---|
| Borrador | Estado `borrador` y `invoice.write` |
| Cobranza | Estado `emitida` o `pagada`, saldo > 0, `invoice.write` y hay cuentas activas |
| Cancelación | `invoice.write`, no borrador, no cancelada y `paid_cents = 0` |
| Nota de pagos | Hay pagos y no está cancelada |
| Motivo de cancelación | Estado `cancelada` |

Acciones:

- **Emitir factura** (solo borrador): confirma que se asignará folio y se
  descontará inventario.
- **Editar borrador**: abre `InvoiceDialog`.
- **Eliminar borrador**: confirma y cierra el detalle.
- **Registrar pago**: abre `PaymentDialog`.
- **Eliminar pago**: confirma y recalcula el saldo.
- **Cancelar factura**: pide un motivo en `TextPromptDialog`; repone inventario
  y no se puede deshacer. Si hay pagos, primero deben eliminarse.

No se puede cancelar una factura con pagos; el panel de cancelación se oculta y
la nota lo explica. La lista y el detalle muestran “Vencida” para una factura
emitida con saldo cuyo vencimiento ya pasó; el filtro de estado, en cambio, no
ofrece “Vencidas” y agrupa esas facturas bajo “Emitidas”.

### 3.5 Pago (`PaymentDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Pago · Libro` |
| Tamaño | 600 × 650 |
| Redimensionable | No |

Campos: Monto (por defecto el saldo completo), Fecha (hoy), Cuenta de efectivo
(solo activas; primera seleccionada), Método (Transferencia, Efectivo, Tarjeta,
Cheque, Domiciliación, Otro), Referencia y Notas.

Validaciones: monto > 0 y fecha. El servicio rechaza pagos que excedan el saldo
con “El pago excede el saldo pendiente (…).” También rechaza facturas no
emitidas/pagadas y cuentas inexistentes. No hay aplicación automática de saldo
a favor ni pagos parciales múltiples en una sola operación.

### 3.6 Producto (`ProductDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Producto · Libro` |
| Tamaño | 760 × 780 |
| Redimensionable | No |

Campos: Nombre, SKU, Unidad, Precio de venta, Costo unitario, IVA (16%, 8%
frontera, 0%), “Controlar inventario y descontar stock al facturar”, Stock
mínimo, Stock inicial (solo en producto nuevo con control de stock) y
Descripción.

Valores por defecto: unidad “pieza”, IVA 16%, control de stock activado. Al
editar, el stock no se modifica desde la ficha y el panel de stock inicial se
oculta. Validaciones: nombre y unidad; el servicio valida precios, costo y
stock. Si el producto nuevo tiene stock inicial > 0, el servicio crea el
movimiento de entrada correspondiente.

### 3.7 Detalle de producto (`ProductDetailWindow`)

| Propiedad | Valor |
|---|---|
| Título | `{producto} · Libro` |
| Tamaño | 1160 × 880; mínimo 1000 × 720 |
| Redimensionable | Sí |

**Indicadores:** Stock actual (o “—” si no controla stock), Costo promedio, Valor
(stock × costo) y Margen. La ficha muestra SKU, unidad, IVA y descripción.

**Movimientos de inventario:** Fecha, Tipo, Cantidad, Costo, Motivo, Cuenta y
Registró. Si no hay: “Aún no hay movimientos registrados.”

**Formulario “Registrar movimiento”:** Tipo (Entrada, Salida, Ajuste de stock),
Fecha, Cantidad / “Stock real contado”, Costo unitario, “¿Pagaste con una
cuenta?”, IVA de la compra, Motivo y Referencia. El panel de compra solo
aparece en entradas; el botón cambia a “Registrar entrada”, “Registrar salida” o
“Ajustar stock”. El ajuste precarga el stock actual y muestra el delta y el
stock resultante; si el resultado es negativo, se pinta en rojo.

Reglas:

- Entrada: suma stock y recalcula costo promedio; si se elige cuenta, crea un
  gasto con IVA acreditable.
- Salida: resta stock; no permite dejar stock negativo (lo valida el servicio).
- Ajuste: fija el stock al valor contado y registra la diferencia.
- El movimiento se guarda sin cerrar la ventana; luego se recarga la ficha.
- Sin `inventory.write`/`products.write`, se ocultan Editar y el formulario.
- Si el producto no controla stock, se muestra “Sin control de stock” y no hay
  formulario de movimiento.

### 3.8 Empleado (`EmployeeDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Empleado · Libro` |
| Tamaño | 780 × 800 |
| Redimensionable | No |

Campos: Nombre completo, RFC, CURP, NSS, Puesto, Departamento, Sueldo mensual,
Fecha de ingreso (hoy por defecto), Fecha de baja (opcional), Estatus
(Activo/Inactivo), Banco y Cuenta bancaria.

Validaciones de UI: nombre y fecha de ingreso. El servicio valida fechas,
sueldo y longitud de datos. No se valida la CURP/NSS/RFC con reglas fiscales ni
se calculan cuotas obrero-patronales.

### 3.9 Nueva nómina (`PayrollPeriodDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Nueva nómina · Libro` |
| Tamaño | 520 × 540 |
| Redimensionable | No |

Campos: Año (texto, 2000–2200), Mes, Tipo de periodo (Mensual/Quincenal),
Quincena (Primera/Segunda, solo si es quincenal) y vista previa del rango.

La vista previa muestra el rango calculado y cuántos empleados activos caen en
él, considerando fecha de ingreso y baja. Validaciones: año válido y mes
seleccionado. El servicio rechaza duplicados y crea los conceptos de todos los
empleados activos en el periodo. Al crear, el diálogo devuelve
`CreatedPeriodId` y la pantalla abre el detalle.

### 3.10 Detalle de nómina (`PayrollDetailWindow`)

| Propiedad | Valor |
|---|---|
| Título | `{periodo} · Libro` |
| Tamaño | 1280 × 880; mínimo 1080 × 720 |
| Redimensionable | Sí |

**Cabecera:** etiqueta del periodo, rango, píldora Borrador/Pagada, botones
Recalcular, Revertir pago, Registrar pago y Cerrar.

**Indicadores:** Empleados, Percepciones, Deducciones y Neto a pagar.

**Tabla:** Empleado, Días, Base, Extra, ISR, IMSS, Infonavit, Otras, Neto y
acciones Editar/Quitar. Si no hay conceptos: “Sin conceptos” y la explicación
“No hay empleados activos en este periodo. Agrega empleados o recalcula.”

**Reglas:**

- En borrador y con `payroll.write`: Recalcular, Registrar pago, Editar y Quitar.
- Pagada: solo Revertir pago; se muestra “Pagada el … Revierte el pago para
  volver a editarla.”
- Recalcular pide confirmación y advierte que conserva extras, Infonavit, otras
  deducciones y notas.
- Quitar pide confirmación por empleado.
- Revertir pago elimina el gasto vinculado y vuelve a borrador.
- El pie muestra “Neto del periodo: …”.
- Nota: “El ISR e IMSS son cálculos de referencia. Puedes editar cada concepto
  antes de pagar.”

### 3.11 Concepto de nómina (`PayrollItemDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Concepto de nómina · Libro` |
| Tamaño | 660 × 720 |
| Redimensionable | No |

Campos: Días, Sueldo base, Percepciones extra, ISR, IMSS, Infonavit, Otras
deducciones y Notas. El Neto se calcula en vivo como
`max(0, base + extra − deducciones)`; se muestra en grande.

Validaciones: días numéricos ≥ 0; importes con hasta dos decimales. El servicio
actualiza el concepto y audita el nuevo neto. No recalcula ISR/IMSS al editar
manualmente; el usuario puede sobrescribir los valores de referencia.

### 3.12 Pagar nómina (`PayrollPaymentDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Pagar nómina · Libro` |
| Tamaño | 540 × 580 |
| Redimensionable | No |

Muestra el neto total y el número de empleados; pide Cuenta de pago (solo
activas) y Fecha de pago (hoy). Nota: “Se registrará un gasto de nómina sin IVA,
vinculado a este periodo.”

Validaciones: cuenta y fecha. El servicio exige que la nómina no esté pagada,
tenga empleados y neto > 0, y que la cuenta esté activa. Al pagar se crea un
movimiento con categoría “Nómina”, origen `nomina`, referencia del periodo y se
marca la nómina como pagada.

### 3.13 Usuario (`UserDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Usuario · Libro` |
| Tamaño | 560 × 720 |
| Redimensionable | No |

Campos: Nombre, Correo, Rol (Administrador, Contador, Capturista, Consulta),
“Usuario activo” y, solo al crear, Contraseña inicial y Confirmar. La ayuda
describe el rol seleccionado.

Validaciones de UI: nombre y correo; contraseña inicial y coincidencia al crear.
Validaciones del servicio: nombre, correo válido, rol válido, correo único,
contraseña de mínimo 8 caracteres, no quitarse el rol de administrador ni
desactivarse, y dejar al menos un administrador activo.

Al editar no se cambia la contraseña; para eso existe “Contraseña” en la lista.
El rol por defecto es Capturista.

### 3.14 Restablecer contraseña (`PasswordResetDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Restablecer contraseña · Libro` |
| Tamaño | 500 × 500 |
| Redimensionable | No |

Muestra nombre y correo del usuario. Pide nueva contraseña y confirmación.
Valida que no esté vacía, que coincida y que cumpla el mínimo de 8 caracteres
en el servicio. Al guardar muestra “Contraseña actualizada. El usuario puede
iniciar sesión con ella.” No cierra la sesión del usuario afectado ni invalida
sesiones existentes.

### 3.15 Cuenta (`AccountDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Cuenta · Libro` |
| Tamaño | 520 × 580 |
| Redimensionable | No |

Campos: Nombre, Tipo (Efectivo, Banco, Tarjeta, Otro), Saldo inicial y “Cuenta
activa”. Nota: “El saldo inicial se suma al saldo calculado con movimientos y
cobros.”

Validación de UI: nombre. **Defectos:** “Otro” no está permitido por el `CHECK`
de la base; en altas, la casilla de activa se ignora y la cuenta siempre se crea
activa.

### 3.16 Categoría (`CategoryDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Categoría · Libro` |
| Tamaño | 500 × 470 |
| Redimensionable | No |

Campos: Nombre, Tipo (Gasto/Ingreso) y “Archivada (oculta en movimientos
nuevos)”. Validación de UI: nombre. **Defecto:** en altas, la casilla de
archivada se ignora y la categoría siempre se crea activa.

### 3.17 Motivo (`TextPromptDialog`)

| Propiedad | Valor |
|---|---|
| Título | `Libro` (el encabezado interno muestra el título recibido) |
| Tamaño | 480 × 340 |
| Redimensionable | No |

Se usa para cancelar facturas. Muestra un mensaje explicativo y un campo de
motivo; el botón “Continuar” valida que no esté vacío con “Escribe el motivo.”
No se usa para otros flujos actualmente.

---

## 4. Matriz de controles por permiso

| Pantalla / control | Permiso | Sin permiso |
|---|---|---|
| Nav Nómina/Empleados | `payroll.view` | No aparecen |
| Nav Impuestos | `tax.view` | No aparece |
| Nav Reportes | `reports.view` | No aparece |
| Nav Usuarios | `users.manage` o `audit.view` | No aparece |
| Movimientos: Nuevo, editar, eliminar | `tx.write` | Botones y columna de acciones ocultos |
| Facturas: Nueva | `invoice.write` | Botón y estado vacío sin botón |
| Detalle factura: emitir, editar, borrar, pagar, cancelar | `invoice.write` | Solo lectura |
| Contactos: nuevo, editar, archivar | `contacts.write` | Botones y columna de acciones ocultos |
| Inventario: nuevo, editar, alternar, mover | `inventory.write` o `products.write` | Botones y columna de acciones ocultos |
| Producto: editar y mover | `inventory.write` o `products.write` | Solo lectura |
| Nómina: nueva, eliminar | `payroll.write` | Botones ocultos |
| Detalle nómina: recalcular, pagar, editar, quitar, revertir | `payroll.write` | Solo lectura |
| Empleados: nuevo, editar, baja | `payroll.write` | Botones y columna de acciones ocultos |
| Configuración: campos de empresa e ISR | `settings.manage` | Solo lectura |
| Configuración: cuentas y categorías | `settings.manage` o `accounts.write` | Sin botones de alta ni columna de acciones |
| Configuración: respaldos | `backup.manage` | Pestaña oculta |
| Usuarios: pestaña y acciones | `users.manage` | Pestaña oculta |
| Auditoría: pestaña y filtros | `audit.view` | Pestaña oculta |

La UI **oculta** controles, pero la autorización real vive en los servicios.
Las excepciones conocidas son `CompanyService` (cuentas, categorías y empresa),
que no vuelve a comprobar permisos, y las lecturas de reportes/impuestos, que
dependen del filtro de navegación.

---

## 5. Catálogo de validaciones visibles

| Diálogo / pantalla | Campo o situación | Mensaje |
|---|---|---|
| Login | Correo vacío | “Escribe tu correo.” |
| Login | Contraseña vacía | “Escribe tu contraseña.” |
| Login | Credenciales | Error del servicio o “Correo o contraseña incorrectos.” |
| Setup | Negocio | “Escribe el nombre de tu negocio.” |
| Setup | Nombre | “Escribe tu nombre.” |
| Setup | Correo | Error de formato |
| Setup | Contraseña | Error de mínimo 8 |
| Setup | Confirmación | “Las contraseñas no coinciden.” |
| Movimiento | Fecha | “Selecciona una fecha.” |
| Movimiento | Subtotal | “Escribe un monto mayor a cero.” |
| Movimiento | Descripción | “Escribe una descripción.” (con mojibake real) |
| Movimiento | Categoría | “Selecciona una categoría.” (con mojibake real) |
| Movimiento | Cuenta | “Selecciona una cuenta de efectivo.” |
| Contacto | Nombre / correo / longitudes | Mensajes del servicio |
| Factura | Cliente | “Selecciona un cliente.” |
| Factura | Fechas | “Selecciona la fecha de emisión y el vencimiento.” |
| Factura | Conceptos | “Agrega al menos un concepto.” |
| Factura | Servicio | Descripción, cantidad, precio, tasa, cliente, vencimiento |
| Pago | Monto | “Escribe un monto mayor a cero.” |
| Pago | Fecha | “Selecciona la fecha del pago.” |
| Pago | Sin cuentas | “No hay cuentas de efectivo activas.” |
| Producto | Nombre | “Escribe el nombre del producto.” |
| Producto | Unidad | “Escribe la unidad.” |
| Producto (detalle) | Cantidad | “Escribe una cantidad válida.” |
| Producto (detalle) | Fecha | “Selecciona la fecha.” |
| Empleado | Nombre | “Escribe el nombre del empleado.” |
| Empleado | Ingreso | “Selecciona la fecha de ingreso.” |
| Nómina (periodo) | Año | “Escribe un año válido.” |
| Nómina (periodo) | Mes | “Selecciona el mes.” |
| Nómina (concepto) | Días | “Escribe los días trabajados.” |
| Nómina (concepto) | Importes | “Revisa los importes: usa números con hasta dos decimales.” |
| Pago nómina | Cuenta | “Selecciona la cuenta de pago.” |
| Pago nómina | Fecha | “Selecciona la fecha de pago.” |
| Usuario | Nombre/correo | “Escribe el nombre y el correo del usuario.” |
| Usuario | Contraseña inicial | “Escribe una contraseña inicial.” |
| Usuario | Confirmación | “Las contraseñas no coinciden.” |
| Contraseña | Nueva | “Escribe la nueva contraseña.” |
| Contraseña | Confirmación | “Las contraseñas no coinciden.” |
| Cuenta | Nombre | “Escribe el nombre de la cuenta.” |
| Categoría | Nombre | “Escribe el nombre de la categoría.” |
| Motivo | Texto | “Escribe el motivo.” |
| Configuración | Negocio | “Escribe el nombre del negocio.” |
| Configuración | IVA | “El IVA debe ser un porcentaje entre 0 y 100.” |
| Configuración | IMSS | “El IMSS debe ser un porcentaje entre 0 y 100.” |
| Configuración | Folio | “El siguiente folio debe ser un número mayor a cero.” |
| Configuración | Serie | “La serie debe tener de 1 a 10 letras o números, sin espacios.” |
| Configuración | RFC | “El RFC debe tener 12 o 13 caracteres (puedes dejarlo vacío).” |
| Configuración | ISR | Errores específicos de JSON y límites |
| Reportes | Fechas | “Selecciona las fechas desde y hasta.” |
| Impuestos | Año | “Escribe un año válido.” |
| Impuestos | Mes | “Selecciona el mes.” |
| Impuestos | Exportar sin consulta | “Primero consulta un periodo.” |
| Inventario | Exportar vacío | “No hay productos para exportar.” |

Los mensajes de error de servicios se muestran tal cual, en español, dentro de
`ErrorText` o en un `MessageBox` según el contexto.

---

## 6. Exportaciones CSV

Todas las exportaciones usan `SaveFileDialog`, escriben **UTF-8 con BOM**
(`new UTF8Encoding(true)`), separador coma, salto de línea del sistema y
importes con punto decimal y dos decimales (`0.00`, sin separador de miles).
Las cadenas se escapan solo si contienen coma, comilla o salto de línea.

| Pantalla | Archivo sugerido | Columnas |
|---|---|---|
| Inventario | `Inventario-yyyyMMdd.csv` | Producto, SKU, Unidad, Stock, Mínimo, Costo, Precio, Valor, Estado, Activo |
| Reportes → Movimientos | `Movimientos-{desde}-a-{hasta}.csv` | Fecha, Tipo, Categoría, Contacto, Descripción, Subtotal, IVA, Total, Cuenta, Origen |
| Reportes → Facturas | `Facturas-{desde}-a-{hasta}.csv` | Folio, Fecha, Vence, Cliente, Estado, Subtotal, IVA, Total, Pagado, Saldo |
| Impuestos | `IVA-{desde}-a-{hasta}.csv` | Tipo, Folio, Fecha, Cliente, Detalle, Subtotal, IVA, Total; después, filas “IVA trasladado”, “IVA acreditable” e “IVA neto” |

Detalles:

- Inventario exporta solo las filas visibles tras aplicar filtros, no todo el
  catálogo.
- Los CSV de Reportes exportan hasta 10,000 movimientos o facturas del rango.
- El CSV de Impuestos incluye ingresos y gastos; los gastos usan folio
  `MOV-{id}`. Los totales finales van en filas de dos columnas.
- Los estados y tipos de los CSV de Reportes salen **sin traducir** (`borrador`,
  `emitida`, `ingreso`, `gasto`, `manual`, etc.), salvo el estado de inventario.
- No hay exportación a PDF, Excel, XML, JSON ni envío por correo.

---

## 7. Estados vacíos y mensajes de carga

| Pantalla | Estado vacío | Acción |
|---|---|---|
| Panel | “Todo el inventario está en orden.” | Ninguna |
| Movimientos | “Sin movimientos en este periodo” | “Nuevo movimiento” si hay permiso |
| Facturas | “Aún no hay facturas” | “Nueva factura” si hay permiso |
| Contactos | “Sin contactos” | “Nuevo contacto” si hay permiso |
| Inventario | “Sin productos” | “Nuevo producto” si hay permiso |
| Nómina | “Sin nóminas” | “Nueva nómina” si hay permiso |
| Empleados | “Sin empleados registrados” | “Nuevo empleado” si hay permiso |
| Detalle factura | “Sin pagos registrados.” | Registrar pago si aplica |
| Detalle producto | “Aún no hay movimientos registrados.” | Formulario si controla stock |
| Detalle nómina | “Sin conceptos” | Recalcular si está en borrador |
| Reportes | “Sin gastos en el periodo.” | Ninguna |

Errores de carga habituales: “No se pudieron cargar los movimientos.”, “No se
pudieron cargar las facturas.”, “No se pudieron cargar los contactos.”, “No se
pudo cargar el inventario.”, “No se pudo cargar la nómina.”, “No se pudo cargar
la plantilla.”, “No se pudieron cargar los impuestos.”, “No se pudieron cargar
los reportes.”, “No se pudieron cargar los usuarios.”, “No se pudo cargar la
auditoría.” Todos incluyen el mensaje de la excepción y registran el fallo.

---

## 8. Defectos y rarezas de interfaz verificados

Cada defecto lleva un identificador **UI-01**…**UI-20**; el registro
consolidado y su mapeo a `H-nn` están en
[13-hallazgos-riesgos.md](13-hallazgos-riesgos.md).

1. **UI-01** · **Mojibake en `TransactionDialog`.** El subtítulo, la lista de métodos de
   pago y dos validaciones usan literales dañados: `facturaciÃ³n`,
   `DomiciliaciÃ³n`, `descripciÃ³n` y `categorÃ­a`. El subtítulo de XAML sí está
   correcto, pero el constructor lo sobrescribe; la lista de métodos y las
   validaciones solo existen en el código.
2. **UI-02** · **Botón “Activar” fijo en Usuarios.** `UsersView.xaml` usa
   `Content="Activar"` sin disparador; un usuario activo muestra “Activar” y la
   acción real es desactivar.
3. **UI-03** · **Botón “Desactivar” fijo en Inventario.** Ocurre lo mismo con productos
   inactivos: el botón dice “Desactivar” y la acción real es reactivar.
4. **UI-04** · **Botón “Baja” fijo en Empleados.** Un empleado inactivo sigue mostrando
   “Baja”; la acción real es reactivar.
5. **UI-05** · **Tipo de cuenta “Otro” inválido.** El combo lo ofrece, pero el esquema solo
   permite `efectivo`, `banco` y `tarjeta`.
6. **UI-06** · **Altas ignoran casillas de estado.** Una cuenta nueva siempre nace activa y
   una categoría nueva siempre nace sin archivar, aunque el usuario desmarque la
   casilla.
7. **UI-07** · **Etiquetas de IVA imprecisas.** “Cobrado en ventas” y “Pagado en gastos”
   aparecen en Panel e Impuestos, pero los cálculos usan IVA de facturas
   emitidas/pagadas y de gastos, no flujos de caja cobrados/pagados.
8. **UI-08** · **“Por cobrar” con definiciones distintas.** Panel, Facturas, Contactos y
   Reportes no usan exactamente el mismo universo de facturas.
9. **UI-09** · **Botones de creación del Panel siempre visibles.** Un rol de solo consulta
   ve “Nueva factura” y “Nuevo movimiento”; al navegar no puede ejecutarlos.
10. **UI-10** · **“Usuarios” para auditores.** Un Contador entra a una pantalla llamada
    “Usuarios” que solo contiene Auditoría.
11. **UI-11** · **Sin filtro “Vencidas”.** La tabla sí muestra “Vencida” mediante el
    disparador de `IsOverdue`, pero el filtro de estado no ofrece “Vencidas” y
    “Emitidas” mezcla facturas vigentes y vencidas. No se puede aislar la
    cartera vencida desde el filtro.
12. **UI-12** · **Límite silencioso de 200.** Movimientos y Facturas solo muestran 200
    filas; no hay paginación ni aviso destacado, solo el pie “mostrando 200”.
13. **UI-13** · **Saldo listado parcial.** El pie de Facturas suma solo las filas cargadas,
    no todo el filtro.
14. **UI-14** · **CSV con estados crudos.** Los estados y orígenes de los CSV de Reportes no
    están traducidos al español.
15. **UI-15** · **Sin impresión.** No existe botón, vista ni código de impresión en la
    aplicación nativa; el README y las notas de versión lo anunciaban en el
    corte original y se corrigieron el 2026-10-25.
16. **UI-16** · **Sin accesibilidad declarada.** No hay etiquetas asociadas, textos
    alternativos ni orden de tabulación explícito; los iconos dependen de
    tooltips.
17. **UI-17** · **Diálogo de factura bloqueado sin clientes.** No permite crear el cliente
    desde ahí; obliga a cerrar y ir a Contactos.
18. **UI-18** · **Sin aviso de stock en factura.** El diálogo permite agregar un producto sin
    stock; el error aparece hasta emitir.
19. **UI-19** · **Pago sin validación previa de saldo.** El diálogo permite escribir un monto
    mayor al saldo; el servicio lo rechaza al guardar.
20. **UI-20** · **Nómina pagada eliminable desde la lista.** El botón “Eliminar” está
    visible; el servicio responde con un error después de la confirmación.

---

## 9. Inventario de archivos de interfaz

| Archivo | Tipo | Función |
|---|---|---|
| `MainWindow.xaml/.cs` | Ventana | Shell, navegación, sesión |
| `App.xaml/.cs` | Aplicación | Arranque, recursos, excepciones |
| `Views/SetupWindow.xaml/.cs` | Ventana | Configuración inicial |
| `Views/LoginWindow.xaml/.cs` | Ventana | Inicio de sesión |
| `Views/DashboardView.xaml/.cs` | UserControl | Panel |
| `Views/TransactionsView.xaml/.cs` | UserControl | Movimientos |
| `Views/InvoicesView.xaml/.cs` | UserControl | Facturas |
| `Views/ContactsView.xaml/.cs` | UserControl | Contactos |
| `Views/InventoryView.xaml/.cs` | UserControl | Inventario |
| `Views/PayrollView.xaml/.cs` | UserControl | Nómina |
| `Views/PayrollDetailWindow.xaml/.cs` | Ventana | Detalle de nómina |
| `Views/EmployeesView.xaml/.cs` | UserControl | Empleados |
| `Views/TaxesView.xaml/.cs` | UserControl | Impuestos |
| `Views/ReportsView.xaml/.cs` | UserControl | Reportes |
| `Views/SettingsView.xaml/.cs` | UserControl | Configuración |
| `Views/UsersView.xaml/.cs` | UserControl | Usuarios y auditoría |
| `Views/PlaceholderView.xaml/.cs` | UserControl | Módulo en construcción (no se usa en la navegación actual) |
| `Views/Dialogs/TransactionDialog.xaml/.cs` | Diálogo | Alta/edición de movimiento |
| `Views/Dialogs/ContactDialog.xaml/.cs` | Diálogo | Alta/edición de contacto |
| `Views/Dialogs/InvoiceDialog.xaml/.cs` | Diálogo | Alta/edición de factura |
| `Views/Dialogs/InvoiceDetailWindow.xaml/.cs` | Ventana | Detalle, cobranza y cancelación |
| `Views/Dialogs/InvoiceLineRow.cs` | Modelo de vista | Línea de factura |
| `Views/Dialogs/PaymentDialog.xaml/.cs` | Diálogo | Registro de pago |
| `Views/Dialogs/ProductDialog.xaml/.cs` | Diálogo | Alta/edición de producto |
| `Views/Dialogs/ProductDetailWindow.xaml/.cs` | Ventana | Ficha y movimientos |
| `Views/Dialogs/EmployeeDialog.xaml/.cs` | Diálogo | Alta/edición de empleado |
| `Views/Dialogs/PayrollPeriodDialog.xaml/.cs` | Diálogo | Nueva nómina |
| `Views/Dialogs/PayrollItemDialog.xaml/.cs` | Diálogo | Edición de concepto |
| `Views/Dialogs/PayrollPaymentDialog.xaml/.cs` | Diálogo | Pago de nómina |
| `Views/Dialogs/UserDialog.xaml/.cs` | Diálogo | Alta/edición de usuario |
| `Views/Dialogs/PasswordResetDialog.xaml/.cs` | Diálogo | Restablecer contraseña |
| `Views/Dialogs/AccountDialog.xaml/.cs` | Diálogo | Alta/edición de cuenta |
| `Views/Dialogs/CategoryDialog.xaml/.cs` | Diálogo | Alta/edición de categoría |
| `Views/Dialogs/TextPromptDialog.xaml/.cs` | Diálogo | Captura de motivo |
| `Controls/DateField.xaml/.cs` | Control | Fecha con calendario |
| `Converters/UiConverters.cs` | Convertidores | Dinero, fecha, porcentaje, estado, color, visibilidad |
| `Resources/Styles.xaml` | Recursos | Paleta, tipografía, botones, tarjetas, tablas |

---

## 10. Enlaces

- Reglas y fórmulas: [05 · Módulos, reglas y flujos](05-modulos-flujos.md).
- Permisos y auditoría: [04 · Seguridad](04-seguridad.md).
- Modelo de datos: [03 · Modelo de datos](03-modelo-datos.md).
- CLI equivalente: [07 · CLI](07-cli.md).
- Hallazgos consolidados: [13 · Hallazgos y riesgos](13-hallazgos-riesgos.md).
