# 01 · Visión, alcance e invariantes

## 1. Problema que resuelve

Un negocio pequeño en México suele llevar su contabilidad en hojas de cálculo,
notas o sistemas en la nube que:

- mezclan ingresos facturados con ingresos no facturados;
- no distinguen IVA trasladado de IVA acreditable;
- no controlan quién capturó o modificó cada operación;
- dependen de internet y de un proveedor externo;
- exponen los datos del negocio en servicios que el usuario no controla.

Libro propone una alternativa **local, nativa y explícita**:

- una base cifrada que vive en la computadora del usuario;
- una interfaz de escritorio en español, sin navegador;
- separación entre **facturas internas**, **movimientos no facturados**,
  **inventario**, **nómina** e **impuestos**;
- roles y auditoría para saber quién hizo qué;
- respaldos cifrados que se pueden restaurar sin conexión.

## 2. Propuesta de valor

| Valor | Implementación vigente |
| --- | --- |
| Datos locales y cifrados | SQLCipher + llave DPAPI en `%LOCALAPPDATA%\Libro\data` |
| Sin dependencia de internet | Aplicación WPF y SQLite local; sin HTTP ni nube |
| Contabilidad operativa completa | Ingresos, gastos, facturas internas, cobranza, inventario, nómina, IVA |
| Control de acceso | Cuatro roles, 14 permisos y auditoría persistente |
| Operación por agentes | CLI en C# con JSON estable, REPL y `schema` |
| Continuidad | Respaldos `.libro` verificados y restauración con copia de seguridad |
| Postura fiscal clara | Facturas internas “No es CFDI”; ISR/IMSS como referencia editable |

## 3. Alcance funcional (vigente)

### 3.1 Operación contable

- **Panel**: ingresos del mes, gastos, utilidad, efectivo y bancos, IVA
  trasladado/acreditable, por cobrar, inventario, últimos movimientos, facturas
  recientes, gráfica de seis meses y stock bajo.
- **Movimientos**: ingresos no facturados y gastos con subtotal, IVA, total,
  categoría, contacto, cuenta, método y referencia.
- **Facturas internas**: borrador, emisión con folio, conceptos, IVA por línea,
  pagos, saldo, cancelación y eliminación de borradores. Sin CFDI.
- **Contactos**: clientes, proveedores o ambos; RFC, correo, teléfono,
  dirección, notas y saldo por cobrar.
- **Inventario**: productos/servicios, SKU, unidad, precio, costo, stock,
  mínimo, control opcional de existencias, entradas, salidas, ajustes y compras
  con gasto automático.
- **Empleados y nómina**: plantilla, sueldos mensuales, periodos mensuales o
  quincenales, prorrateo por días, ISR/IMSS de referencia, edición de conceptos,
  pago con gasto automático y reversión.
- **Impuestos**: IVA trasladado, acreditable y neto por periodo; resumen anual
  por mes; exportación CSV.
- **Reportes**: estado de resultados, flujo de efectivo, antigüedad de saldos,
  desempeño mensual, saldos de cuentas y exportaciones CSV.
- **Usuarios y auditoría**: alta, edición, activación, reseteo de contraseña,
  roles y consulta del registro de cambios.
- **Configuración y respaldos**: datos del negocio, cuentas, categorías, tasas,
  series/folios, respaldos cifrados, copia externa y restauración.

### 3.2 Operación por agentes

- CLI `libro-cli` que referencia el proyecto WPF y usa sus servicios reales.
- Salida JSON en `stdout`, errores JSON en `stderr`, códigos de salida.
- Entrada JSON por `--input` o `--stdin`.
- REPL con sesión en memoria.
- Comando `doctor` para diagnóstico de base, llave y respaldos.
- Comando `schema` con ejemplos de las entradas JSON.
- Skill de OpenCode generada en
  `agent-harness/skills/cli-anything-libro/SKILL.md`.

### 3.3 Distribución

- Instalador Inno Setup por usuario, sin privilegios de administrador.
- Publicación self-contained `win-x64`.
- Actualización sobre la versión Next.js anterior limpiando archivos obsoletos
  sin tocar `%LOCALAPPDATA%\Libro\data`.
- Desinstalación que conserva los datos.

## 4. Fuera de alcance (no objetivos)

| No objetivo | Estado |
| --- | --- |
| CFDI 4.0, XML, timbrado, PAC, SAT | **No implementado**. Las facturas son internas. |
| Facturación electrónica de nómina | **No implementado**. La nómina es operativa interna. |
| Declaraciones fiscales, DIOT, contabilidad electrónica | **No implementado**. |
| Multiusuario concurrente en red | **No implementado**. No hay servidor ni bloqueo multiusuario. |
| Nube, sincronización, app móvil | **No implementado**. Todo es local. |
| Multiempresa o multitenant | **No implementado**. Una empresa por carpeta de datos. |
| Multimoneda | **No implementado**. MXN implícito. |
| Conciliación bancaria automática | **No implementado**. Saldos por movimientos y cobros. |
| Activos fijos, depreciación, centros de costo | **No implementado**. |
| Nómina con cuotas IMSS/INFONAVIT oficiales completas | **No implementado**. IMSS es un porcentaje simple; INFONAVIT es captura manual. |
| Impresión de facturas en la app nativa | **No implementado** en 2.0.0; véase hallazgo H-01. |
| API HTTP o WebView | **Prohibido por diseño** en la app vigente. |
| Node.js en producción | **Prohibido por diseño**; `src/` es legado archivado. |

## 5. Invariantes del producto

Estas reglas deben conservarse en cualquier cambio:

1. **Los datos viven en `%LOCALAPPDATA%\Libro\data`** (o en el directorio que
   indique `LIBRO_DATA_DIR` / `--data-dir`), nunca dentro de `{app}`.
2. **La base está cifrada con SQLCipher**; la llave se protege con DPAPI del
   usuario de Windows y no se escribe en texto plano.
3. **No se copia una base sin `clave.bin`**. Los respaldos solo se restauran con
   la misma cuenta de Windows.
4. **El dinero es entero en centavos**. No se usan `double` para importes.
5. **Las facturas son documentos internos**. Se muestra “No es CFDI”; no se
   genera XML ni se contacta a un PAC.
6. **Las ventas facturadas no se duplican como movimientos manuales**. Los
   ingresos no facturados van en Movimientos; las ventas con factura van en
   Facturas.
7. **La emisión de una factura descuenta stock** de los productos con control de
   inventario; la cancelación lo repone.
8. **Los movimientos generados automáticamente** (`source = inventario` o
   `nomina`) no se editan ni eliminan manualmente.
9. **Toda escritura relevante pasa por un servicio** que valida permisos y
   registra auditoría. Las vistas no escriben SQL directo.
10. **Los roles se respetan en UI, servicios y CLI**. Ocultar un botón no es
    suficiente: el servicio vuelve a validar.
11. **Los datos de prueba van a carpetas temporales**. Nunca a
    `%LOCALAPPDATA%\Libro\data`.
12. **La aplicación vigente es nativa**. No se reintroduce Node, HTTP, WebView ni
    un servidor local.

## 6. Actores y roles

| Rol | Clave | Descripción | Uso típico |
| --- | --- | --- | --- |
| Administrador | `admin` | Acceso total, usuarios, configuración y respaldos. | Dueño o responsable de sistemas |
| Contador | `contador` | Operación contable, facturas, nómina, impuestos, reportes y auditoría; sin usuarios, configuración ni respaldos. | Contador externo o interno |
| Capturista | `capturista` | Captura de movimientos, facturas, contactos, productos e inventario. Sin reportes, impuestos, nómina ni auditoría. | Auxiliar administrativo |
| Consulta | `consulta` | Solo lectura de reportes e impuestos. | Socio o auditor de lectura |

La matriz exacta de permisos está en
[04-seguridad.md](04-seguridad.md#72-matriz-completa).

## 7. Casos de uso principales

### CU-01 · Primer arranque

Una instalación nueva no tiene usuarios. La app muestra **Configuración
inicial**, crea la empresa, el administrador y la llave de cifrado, y entra al
panel. El CLI equivalente es `auth setup`.

### CU-02 · Operación diaria

El capturista registra gastos y ventas no facturadas, crea contactos y productos,
y emite facturas internas. El contador revisa reportes e impuestos y paga la
nómina. El administrador gestiona usuarios y respaldos.

### CU-03 · Cobranza

Se emite una factura, se registran uno o varios pagos y el estado cambia a
`pagada` cuando `paid_cents >= total_cents`. La antigüedad de saldos muestra lo
que sigue pendiente.

### CU-04 · Compra de inventario

Una entrada con cuenta de pago genera un gasto con IVA acreditable, recalcula el
costo promedio y actualiza el stock. Una salida manual o una venta facturada
descuentan existencias.

### CU-05 · Nómina mensual

Se crea el periodo, se calculan los conceptos de empleados activos, se ajustan
ISR/IMSS/INFONAVIT si hace falta, se paga con una cuenta y se genera un gasto sin
IVA. Si hay un error, se revierte el pago y se vuelve a borrador.

### CU-06 · Respaldo y restauración

El administrador crea un respaldo `.libro`, guarda una copia externa y, si algo
sale mal, restaura desde la app o el CLI. La restauración crea antes un respaldo
de seguridad y reemplaza la base.

### CU-07 · Automatización por agente

Un agente ejecuta `libro-cli --json ...`, interpreta importes en centavos y
valida permisos. No toca la base real si usa `--data-dir` temporal.

## 8. Métricas de éxito operativas

No hay telemetría remota. El éxito se evalúa localmente:

- la app abre con la base cifrada y sin errores;
- `doctor` reporta `opened: true` y conteos coherentes;
- las pruebas E2E pasan 11/11;
- los respaldos se crean con SHA256 y se restauran con copia de seguridad;
- los reportes cuadran con los movimientos y facturas del periodo;
- ningún rol puede ejecutar una escritura para la que no tiene permiso.

## 9. Restricciones técnicas

- Windows 10/11 x64.
- .NET 8 SDK para compilar; runtime self-contained para ejecutar el instalador.
- SQLCipher mediante `SQLitePCLRaw.bundle_e_sqlcipher`.
- DPAPI ligado a la cuenta de Windows y al perfil del equipo.
- Interfaz WPF: no hay versión web ni multiplataforma.
- Inno Setup 6 para generar el instalador.
- Python 3 con `unittest` para las pruebas E2E (no se requiere `pytest`).

## 10. Postura fiscal y legal

- Libro es una herramienta de **control interno**, no un emisor de comprobantes
  fiscales.
- El RFC, régimen fiscal y datos del negocio se usan para presentación en
  facturas internas y reportes, no para timbrado.
- El IVA se calcula por línea y se resume por periodo; no se generan acuses ni
  declaraciones.
- El ISR se calcula con tarifas mensuales de referencia y se escala a quincenal;
  el IMSS es un porcentaje simple sobre percepciones. Ambos son editables y
  deben confirmarse con un contador.
- La documentación no constituye asesoría legal ni fiscal.

## 11. Estado de la versión

| Artefacto | Versión | Estado |
| --- | --- | --- |
| App WPF | 2.0.0 | Vigente |
| Base de datos | `user_version = 1`, 16 tablas | Vigente |
| CLI | 1.0.0 | Vigente |
| Instalador | 2.0.0 | Publicado en `dist/` |
| App Next.js | 0.1.0 | Archivada en `src/` |
| Instalador Next.js | 1.0.0 | Archivado en `dist/` |

Véase [08-operacion-instalador.md](08-operacion-instalador.md) para el detalle de
artefactos y [11-legado-nextjs.md](11-legado-nextjs.md) para el código
archivado.
