# 12 · Glosario

> **Estado:** Vigente.
> **Corte:** 25 de octubre de 2026.
> **Ámbito:** términos contables, fiscales, de nómina, de seguridad, de CLI y de
> instalación usados en la documentación de Libro.
> **Fuente de verdad:** `docs/`, `desktop/Libro.Desktop/`, `agent-harness/` y
> `installer/`.
> **No cubre:** asesoría fiscal ni definiciones legales completas. Las
> definiciones son operativas: explican cómo usa el término **este** software.

---

## 1. Cómo usar este glosario

Cada entrada tiene:

- **Término** y, si aplica, sinónimo o acrónimo.
- **Qué significa** en general.
- **Cómo lo usa Libro**, con la diferencia entre “implementado” y “no
  implementado”.

Las marcas siguientes son importantes:

| Marca | Significado |
|---|---|
| **Implementado** | Libro lo calcula, guarda o muestra. |
| **Parcial** | Libro lo usa de forma simplificada. |
| **No implementado** | El término aparece en el dominio, pero Libro no lo hace. |
| **Solo dato** | Se guarda como texto, sin cálculo asociado. |

Si una definición fiscal contradice una autoridad, prevalece la autoridad. Libro
es una herramienta de control interno, no una declaración fiscal.

---

## 2. Contabilidad básica

### Activo

Bien o derecho con valor económico: efectivo, banco, cuentas por cobrar,
inventario, equipo. **Libro** no lleva un balance general formal; aproxima el
activo con saldos de cuentas, cuentas por cobrar y valor de inventario.

### Pasivo

Obligación o deuda. **Libro** no lleva pasivos formales. La nómina por pagar y
los impuestos por pagar no se registran como pasivos; se reconocen como gasto
cuando se pagan.

### Capital / patrimonio

Diferencia entre activo y pasivo. **No implementado** como estado financiero.

### Ingreso

Entrada de dinero o derecho a cobrarlo por venta de bienes o servicios.
**Implementado**:

- movimientos con `type = 'ingreso'`;
- facturas emitidas (`invoicedRevenueCents`);
- pagos cobrados (flujo de efectivo).

Libro distingue **ingreso facturado** de **ingreso cobrado**. No son lo mismo.

### Gasto

Salida de dinero o consumo de un recurso para operar. **Implementado**:

- movimientos con `type = 'gasto'`;
- gasto generado por nómina pagada;
- gasto generado por movimiento de inventario con cuenta.

### Costo

Valor de adquisición de un bien vendido o de un insumo. **Parcial**:

- los productos tienen `cost_cents`;
- las facturas guardan `unit_cost_cents` por línea;
- no hay costo de ventas consolidado ni margen bruto formal.

### Utilidad

Ingresos menos gastos y costos. **Parcial**: los reportes muestran ingresos,
gastos y un resultado, pero no un estado de resultados completo.

### Flujo de efectivo

Movimiento real de dinero en un periodo. **Implementado** en
`ReportService.GetCashflow`: entradas y salidas por mes y neto.

### Devengo

Reconocer un ingreso o gasto cuando ocurre, no cuando se paga. **Parcial**: las
facturas emitidas se reconocen como ingreso facturado; los gastos se reconocen
al capturarse, normalmente al pagarse. No hay contabilidad de causación
completa.

### Base contable

Regla para registrar. Libro usa una **base simplificada de caja con facturas
emitidas**:

- movimientos: cuando se capturan;
- facturas: al emitirse;
- pagos: al recibirse;
- nómina: al pagarse.

### Periodo

Intervalo de reporte. En Libro suele ser un mes (`yyyy-MM-01` a `yyyy-MM-31`) o
un año. Las fechas son texto `yyyy-MM-dd`.

### Ejercicio fiscal

Año calendario para impuestos. **Parcial**: `taxes annual` agrupa por mes del
año; no calcula declaraciones anuales.

### Corte

Fecha en la que se toma una foto de los datos o de la documentación. En esta
documentación, **25 de octubre de 2026**.

### Saldo

Diferencia entre cargos y abonos, o entre total y pagos. **Implementado**:

- saldo de una factura: `total_cents - paid_cents`;
- saldo por cuenta: `opening_balance + ingresos - gastos`;
- saldo por cobrar: suma de facturas emitidas no pagadas.

### Cuenta por cobrar

Dinero que un cliente debe. **Implementado** con facturas en estado `emitida` y
saldo pendiente. La antigüedad se agrupa en tramos.

### Cuenta por pagar

Dinero que el negocio debe. **No implementado** como módulo. Los gastos se
registran cuando se pagan o capturan; no hay seguimiento de acreedores.

### Conciliación

Comparar dos fuentes para verificar saldos. **No implementado**. Libro no
importa estados de cuenta bancarios.

### Arqueo de caja

Conteo físico de efectivo contra el saldo registrado. **No implementado**.

### Cargo y abono

Débito y crédito de la partida doble. **No implementado**: Libro no usa partida
doble ni pólizas. Usa movimientos de ingreso/gasto y saldos agregados.

### Póliza

Documento contable que agrupa asientos. **No implementado**.

### Asiento

Registro de una transacción en libros. En Libro, la unidad equivalente es un
**movimiento**, una **factura** o un **pago**, no un asiento de partida doble.

### Catálogo

Lista maestra de cuentas, categorías, contactos o productos. **Implementado**
para:

- cuentas (`accounts`);
- categorías (`categories`);
- contactos (`contacts`);
- productos (`products`);
- empleados (`employees`);
- usuarios (`users`).

### Centro de costo

Unidad de negocio a la que se imputa un gasto. **No implementado**.

### Presupuesto

Plan de ingresos y gastos. **No implementado**. No hay comparación
presupuesto/real.

---

## 3. Dinero, IVA e impuestos

### Centavo

Unidad mínima de dinero. **Implementado**: todos los importes monetarios se
guardan como enteros de centavos (`INTEGER`, `long`). `10000` = `$100.00`.

### MXN

Peso mexicano. **Implementado** como moneda de formato (`es-MX`, `$`). No hay
multimoneda.

### Subtotal

Importe antes de impuestos. **Implementado** en movimientos, facturas y nómina.

### IVA

Impuesto al Valor Agregado. **Implementado** como cálculo:

```text
iva = round(subtotal × tasa, AwayFromZero)
total = subtotal + iva
```

Las tasas habituales son 0 %, 8 % y 16 %. La tasa se guarda como `REAL` en
`0..1` (`0.16` = 16 %).

### Tasa de IVA

Porcentaje aplicable. **Implementado** por empresa (`company.iva_rate`), por
producto (`products.iva_rate`), por movimiento y por línea de factura.

### IVA trasladado

IVA cobrado al cliente. **Parcial**: se calcula en facturas y movimientos de
ingreso; no se declara.

### IVA acreditable

IVA pagado en gastos que puede acreditarse. **Parcial**: se registra en
movimientos de gasto; no hay cálculo de acreditamiento ni compensación fiscal.

### IVA por pagar

Diferencia entre IVA trasladado y acreditable. **Parcial**: `taxes summary`
muestra la diferencia del periodo como estimación, no como declaración.

### IEPS

Impuesto Especial sobre Producción y Servicios. **No implementado**.

### ISR

Impuesto sobre la Renta. **Parcial**:

- en nómina se calcula una retención estimada con tramos configurables;
- no se calcula ISR empresarial, provisional ni anual.

### Tramos de ISR

Tabla de límites inferior, cuota fija y tasa marginal. **Implementado** como
JSON en `company.isr_brackets`. El cálculo ajusta los tramos según el periodo
(mensual o quincenal).

### Tarifa marginal

Porcentaje aplicable al excedente del tramo. **Implementado** en
`Payroll/Isr.cs`.

### Cuota fija

Importe base del tramo de ISR. **Implementado**.

### Subsidio al empleo

Beneficio fiscal que reduce la retención de ISR. **No implementado**. La
retención puede ser mayor que la real si aplicara subsidio.

### IMSS

Instituto Mexicano del Seguro Social. **Parcial**:

- `company.imss_rate` (por defecto `0.025`) se aplica a la base gravable;
- es una **estimación de control interno**, no la cuota obrero-patronal oficial;
- no distingue cuotas obreras, patronales, riesgos de trabajo, guarderías ni
  cesantía.

### Infonavit

Instituto del Fondo Nacional de la Vivienda para los Trabajadores. **Parcial**:
se captura como deducción manual por empleado; no se calcula el porcentaje
patronal ni el crédito.

### Retención

Cantidad que se descuenta y se entera a una autoridad. **Parcial**: ISR e IMSS
en nómina son retenciones/estimaciones; no hay entero ni declaración.

### Contribución

Pago obligatorio al Estado. **No implementado** como módulo de declaraciones.

### RFC

Registro Federal de Contribuyentes. **Solo dato** en empresa, contactos y
empleados. Libro no valida su estructura ni lo usa para CFDI.

### CURP

Clave Única de Registro de Población. **Solo dato** en empleados.

### NSS

Número de Seguridad Social. **Solo dato** en empleados.

### Régimen fiscal

Clave del régimen ante el SAT (por ejemplo, `601`). **Solo dato** en empresa. No
cambia cálculos.

### SAT

Servicio de Administración Tributaria. Libro **no** se conecta al SAT.

### CFDI

Comprobante Fiscal Digital por Internet. **No implementado**. Libro no genera,
timbra, cancela ni valida CFDI. Sus facturas son **internas**.

### Factura interna

Documento de control que registra folio, cliente, conceptos, IVA, total, pagos y
saldo. **Implementado**. **No es CFDI** y no tiene validez fiscal.

### PAC

Proveedor Autorizado de Certificación. **No implementado**. No hay integración.

### Timbrado

Proceso de validar un CFDI con el SAT a través de un PAC. **No implementado**.

### DIOT

Declaración Informativa de Operaciones con Terceros. **No implementado**.

### Constancia de situación fiscal

Documento del SAT con datos del contribuyente. **No implementado**. Libro solo
guarda RFC y régimen como texto.

### Nota de crédito

Documento que reduce el importe de una factura. **No implementado**. La
cancelación total sí existe; no hay notas parciales.

### Complemento de pago

Documento fiscal que registra un pago de CFDI. **No implementado**.

### Retención de IVA

IVA que un cliente retiene al proveedor. **No implementado**.

### Declaración

Presentación de impuestos ante la autoridad. **No implementado**. Libro produce
reportes internos, no declaraciones.

### Estimación

Cálculo aproximado de control interno. En Libro, ISR, IMSS e Infonavit son
estimaciones; no sustituyen el cálculo oficial.

---

## 4. Facturación y cobranza

### Factura

En Libro, documento **interno** con folio, cliente, conceptos, IVA y saldo. No es
CFDI. **Implementado**.

### Borrador

Factura editable que aún no tiene folio ni afecta stock. **Implementado**
(`status = 'borrador'`).

### Emitida

Factura con folio asignado que ya afecta stock y cuentas por cobrar.
**Implementado** (`status = 'emitida'`).

### Pagada

Factura cuyo saldo es cero. **Implementado** (`status = 'pagada'`).

### Cancelada

Factura anulada con motivo. **Implementado** (`status = 'cancelada'`). Si estaba
emitida, devuelve stock.

### Folio

Identificador de la factura: `serie-número`, con número de 4 dígitos
(`A-0001`). **Implementado**.

### Serie

Prefijo del folio (`A`, `B`, …). **Implementado** en `company.invoice_series` y
en cada factura.

### Concepto / línea

Renglón de una factura: descripción, cantidad, precio unitario, IVA, subtotal,
IVA y total. **Implementado** en `invoice_items`.

### Condiciones de pago

Texto libre sobre plazo o forma de pago. **Solo dato**
(`invoices.payment_terms`).

### Vencimiento

Fecha en la que la factura debería pagarse. **Implementado**
(`invoices.due_date`).

### Pago

Registro de dinero recibido contra una factura. **Implementado** en `payments`.
Puede ser parcial o total.

### Pago parcial

Pago menor al saldo. **Implementado**. La factura sigue `emitida`.

### Saldo

`total_cents - paid_cents`. **Implementado**.

### Cobranza

Proceso de recuperar cuentas por cobrar. **Parcial**: reportes de antigüedad y
saldos; no hay recordatorios, estados de cuenta ni gestión de cobranza.

### Antigüedad de saldos

Clasificación de facturas vencidas por tramos de días. **Implementado** en
`ReportService.GetReceivableAging`.

### Tramo / bucket

Rango de días de antigüedad (por ejemplo, 0–30, 31–60). **Implementado**.

### Cuenta de ingreso

Cuenta donde se recibe el pago (Caja, Banco, Tarjeta). **Implementado**.

### Método de pago

Texto libre: transferencia, efectivo, tarjeta, etc. **Solo dato**. No hay
catálogo fiscal.

### Referencia

Folio, autorización o nota del pago. **Solo dato**.

### Devolución

Regreso de dinero al cliente. **No implementado**. Se puede cancelar la factura
o registrar un gasto, pero no hay flujo de devolución.

### Nota de venta

Documento simplificado. **No implementado**.

### Cotización

Propuesta previa a la factura. **No implementado**.

### Remisión

Documento de entrega. **No implementado**.

---

## 5. Contactos e inventario

### Contacto

Cliente, proveedor o ambos. **Implementado** con tipo, RFC, correo, teléfono,
dirección y notas.

### Cliente

Contacto al que se le factura o vende. **Implementado** (`type = 'cliente'`).

### Proveedor

Contacto al que se le compra o paga. **Implementado** (`type = 'proveedor'`).

### Ambos

Contacto que es cliente y proveedor. **Implementado** (`type = 'ambos'`).

### Archivado / archivada

Estado lógico que oculta un registro sin borrarlo. **Implementado** en
contactos, categorías y productos (como “inactivo”).

### Producto

Bien o servicio con SKU, precio, costo, IVA, stock y mínimo. **Implementado**.

### Servicio

Producto sin control de stock (`track_stock = 0`). **Implementado**.

### SKU

Clave única de producto. **Implementado**; puede estar vacía, pero si no lo
está, es única.

### Stock

Cantidad disponible. **Implementado** como `REAL`; puede ser negativo si se
fuerza una salida, aunque la emisión de factura valida stock.

### Stock mínimo

Umbral para alertar reposición. **Implementado** (`min_stock`).

### Movimiento de inventario

Entrada, salida o ajuste de stock. **Implementado** en
`inventory_movements`.

### Entrada

Aumento de stock; puede generar un gasto si se asocia a una cuenta.
**Implementado**.

### Salida

Disminución de stock; puede ocurrir por factura o movimiento manual.
**Implementado**.

### Ajuste

Corrección de stock sin compra/venta. **Implementado**. Puede ser positivo o
negativo.

### Costo unitario

Costo por unidad. **Implementado** en productos y movimientos.

### Valor de inventario

`stock × cost_cents` sumado. **Implementado** en el resumen.

### Unidad

Texto libre: pieza, kg, litro, servicio. **Solo dato**.

---

## 6. Nómina y empleados

### Nómina

Cálculo y pago de sueldos y deducciones de un periodo. **Implementado** como
control interno; no genera CFDI de nómina ni timbrado.

### Periodo de nómina

Mes o quincena con fecha de inicio, fin y estado. **Implementado**.

### Mensual

Periodo de un mes. **Implementado**.

### Quincenal

Periodo de 15 días; `quincena` 1 o 2. **Implementado**.

### Días

Días pagados en el periodo. **Implementado** (`days`).

### Base

Sueldo base del periodo. **Implementado** (`base_cents`).

### Percepción

Ingreso del trabajador: sueldo, horas extra, bonos. **Parcial**: base y
percepciones extra.

### Percepción extra

Importe adicional a la base. **Implementado** (`extra_perceptions_cents`).

### Deducción

Descuento al trabajador: ISR, IMSS, Infonavit, préstamos. **Implementado** como
campos.

### Neto

`base + extra - ISR - IMSS - Infonavit - otras deducciones`. **Implementado**.

### ISR de nómina

Retención estimada de ISR. **Parcial**; depende de la tabla configurada.

### IMSS de nómina

Estimación porcentual sobre la base. **Parcial**; no es la cuota oficial.

### Infonavit

Deducción manual. **Parcial**.

### Otras deducciones

Campo libre para préstamos, pensión, etc. **Implementado**.

### Aguinaldo

Prestación anual. **No implementado** como cálculo automático; se puede capturar
como percepción extra.

### Prima vacacional

Prestación por vacaciones. **No implementado** automáticamente.

### Finiquito / liquidación

Cálculo de terminación laboral. **No implementado**.

### SBC

Salario Base de Cotización. **No implementado**. La base de IMSS es el importe
capturado, no el SBC oficial.

### Empleado activo

Empleado con `status = 'activo'`. **Implementado**.

### Empleado inactivo / baja

Empleado con `status = 'inactivo'`. **Implementado**. La baja no siempre fija
`end_date`; es un hallazgo funcional.

### Pago de nómina

Acción que marca el periodo como `pagada`, registra un gasto y guarda cuenta,
fecha y transacción. **Implementado**.

### Reversión de nómina

Deshace el pago y borra el gasto vinculado. **Implementado**.

---

## 7. Estados y enums

### `transactions.type`

| Valor | Significado |
|---|---|
| `ingreso` | Entrada de dinero. |
| `gasto` | Salida de dinero. |

### `transactions.source`

| Valor | Significado |
|---|---|
| `manual` | Capturado por el usuario. |
| `payroll` | Generado por pago de nómina. |
| `invoice` | Generado por factura o pago. |
| `inventory` | Generado por movimiento de inventario. |

### `accounts.type`

| Valor | Significado |
|---|---|
| `efectivo` | Caja. |
| `banco` | Cuenta bancaria. |
| `tarjeta` | Tarjeta de crédito/débito. |

El diálogo ofrece “Otro” con `Tag="otro"`, pero el `CHECK` de la base no lo
acepta. Es un defecto de UI.

### `categories.kind`

| Valor | Significado |
|---|---|
| `ingreso` | Categoría de ingreso. |
| `gasto` | Categoría de gasto. |
| `costo` | Categoría de costo. |

### `contacts.type`

| Valor | Significado |
|---|---|
| `cliente` | Cliente. |
| `proveedor` | Proveedor. |
| `ambos` | Ambos. |

### `invoices.status`

| Valor | Significado |
|---|---|
| `borrador` | Editable, sin folio, sin stock. |
| `emitida` | Con folio, stock descontado, saldo pendiente. |
| `pagada` | Saldo cero. |
| `cancelada` | Anulada con motivo. |

### `inventory_movements.type`

| Valor | Significado |
|---|---|
| `entrada` | Aumenta stock. |
| `salida` | Disminuye stock. |
| `ajuste` | Corrige stock. |

### `employees.status`

| Valor | Significado |
|---|---|
| `activo` | En alta. |
| `inactivo` | Dado de baja lógica. |

### `payroll_periods.status`

| Valor | Significado |
|---|---|
| `borrador` | Editable. |
| `pagada` | Pagada y con gasto vinculado. |

### `payroll_periods.period_type`

| Valor | Significado |
|---|---|
| `mensual` | Un mes. |
| `quincenal` | Quincena 1 o 2. |

---

## 8. Roles y permisos

### Rol

Conjunto de permisos. **Implementado** con cuatro roles.

### `admin`

Administrador. Los 14 permisos, incluidos usuarios, configuración y respaldos.

### `contador`

Contador. Todos los permisos operativos y de lectura, excepto usuarios,
configuración y respaldos.

### `capturista`

Capturista. Cinco permisos de escritura: movimientos, facturas, inventario,
contactos y productos.

### `consulta`

Consulta. Solo `tax.view` y `reports.view`. Puede leer más datos de los que su
descripción sugiere, porque las lecturas solo exigen sesión.

### Permiso

Autorización con forma `recurso.acción`. **Implementado** en
`Services/Permissions.cs` y replicado en el CLI.

### `users.manage`

Gestionar usuarios y roles.

### `settings.manage`

Editar empresa, cuentas, categorías y tarifas.

### `backup.manage`

Crear, listar, restaurar y borrar respaldos.

### `audit.view`

Ver la bitácora.

### `payroll.view`

Ver nómina y empleados.

### `payroll.write`

Crear, editar, pagar y revertir nómina.

### `tax.view`

Ver impuestos.

### `reports.view`

Ver reportes.

### `tx.write`

Crear, editar y borrar movimientos.

### `invoice.write`

Crear, emitir, cancelar y pagar facturas.

### `inventory.write`

Mover inventario y ajustar stock.

### `contacts.write`

Crear, editar y archivar contactos.

### `products.write`

Crear, editar y activar productos.

### `accounts.write`

Crear, editar y archivar cuentas y categorías.

### Sesión

Usuario autenticado en memoria. **Implementado** en la app nativa; en el CLI
one-shot se crea y se pierde en cada proceso, salvo REPL.

### Login

Autenticación con correo y contraseña. **Implementado**.

### Logout

Cierre de sesión. **Implementado**. En el CLI solo limpia la memoria; si hay
credenciales globales, el siguiente comando puede auto-iniciar sesión.

### Bloqueo de login

Límite de intentos fallidos. **Implementado** con 10 intentos por correo cada 10
minutos. En el CLI el contador es estático por proceso y no acumula entre
invocaciones.

### Auditoría

Registro de acciones. **Implementado** en `audit_log` con usuario, acción,
entidad, detalle y fecha.

---

## 9. Seguridad y cifrado

### SQLite

Motor de base de datos embebido. **Implementado** como formato subyacente.

### SQLCipher

Extensión de SQLite que cifra la base completa. **Implementado** mediante
`e_sqlcipher.dll` y `PRAGMA key`.

### Base cifrada

Archivo `contabilidad.db` sin cabecera `SQLite format 3\0`; solo se abre con la
llave correcta. **Implementado**.

### Base en texto plano

Archivo SQLite legado, legible sin llave. **Legado**. Se detecta por la cabecera
y se migra al primer arranque de 2.0.0.

### Llave

Secreto de 32 bytes que cifra la base. **Implementado** en `clave.bin`.

### `clave.bin`

Archivo con la llave protegida por DPAPI. **Implementado**. Sin él no se abre la
base ni se restauran respaldos.

### DPAPI

Data Protection API de Windows. **Implementado** con
`ProtectedData.Protect(..., CurrentUser)`. La llave solo la descifra la misma
cuenta de Windows en el mismo perfil.

### Ámbito `CurrentUser`

La protección DPAPI está ligada al usuario de Windows. Copiar `clave.bin` a otra
cuenta o equipo no garantiza poder descifrarla.

### scrypt

Función de derivación de contraseñas. **Implementado** con `N=16384`, `r=8`,
`p=1`, sal de 16 bytes y hash de 64 bytes.

### Sal

Valor aleatorio que se combina con la contraseña para que dos hashes de la misma
contraseña sean distintos. **Implementado**.

### Hash

Resumen irreversible de una contraseña. **Implementado**. Libro nunca imprime
`password_hash` en la API/CLI.

### Verificación de contraseña

Comparar el hash calculado con el almacenado en tiempo constante.
**Implementado** con `CryptographicOperations.FixedTimeEquals` o equivalente.

### Cifrado en reposo

Protección de los datos cuando el equipo está apagado o el archivo se copia.
**Implementado** con SQLCipher.

### Cifrado en tránsito

Protección de los datos mientras viajan por la red. **No aplica**: la app nativa
no tiene red. El legado usaba HTTP.

### WAL

Write-Ahead Logging de SQLite. **Implementado**. Genera `contabilidad.db-wal` y
`contabilidad.db-shm`.

### `-wal`

Archivo de registro anticipado. Contiene cambios aún no consolidados. Debe
copiarse junto con la base o respaldarse con la API de backup.

### `-shm`

Archivo de memoria compartida de SQLite. Es auxiliar; no se debe usar solo para
restaurar.

### Integridad

Consistencia interna de la base. **Implementado** con
`PRAGMA integrity_check` antes de restaurar y en la autoprueba.

### Respaldo

Copia completa y consistente de la base. **Implementado** en formato `.libro`
(SQLCipher).

### Restauración

Reemplazo de la base actual por un respaldo verificado. **Implementado** con
respaldo de seguridad previo y confirmación.

### Respaldo de seguridad

Copia automática `Libro-antes-restaurar-*.libro` creada antes de restaurar.
**Implementado**.

### Path traversal

Ataque que intenta salir de una carpeta con `..`. **Mitigado** en el legado con
`resolveBackup`; en la app nativa los diálogos eligen archivos, no rutas
arbitrarias del servidor.

### Inyección SQL

Introducir SQL malicioso en una consulta. **Mitigado** con parámetros de Dapper
y consultas preparadas.

### Secreto

Dato que no debe publicarse: contraseña, llave, token, hash. **Regla**: no
incluir secretos en documentación, logs ni respaldos compartidos.

### Token de sesión

Valor aleatorio que identifica una sesión. **Implementado** en el legado con
cookie; en la app nativa no hay token persistente.

### Cookie

Mecanismo del navegador para enviar un token. **Solo legado**
(`contabilidad_sesion`).

### CSRF

Cross-Site Request Forgery. **Parcial**: el legado usa `SameSite=Lax` y el
manejo de Server Actions de Next.js; no hay token explícito.

### SmartScreen

Filtro de Windows que advierte sobre ejecutables no firmados. **Aplica** al
instalador 2.0.0 porque no está firmado.

### Authenticode

Firma digital de ejecutables de Windows. **No implementado**.

### Firma de código

Certificado que identifica al editor. **No implementado** en 1.0.0 ni 2.0.0.

---

## 10. CLI y automatización

### CLI

Command Line Interface. **Implementado** en `agent-harness/Libro.Cli`.

### Comando

Unidad de ejecución del CLI, por ejemplo `transactions list`.

### Grupo

Primer nivel del comando: `accounts`, `invoices`, `payroll`, etc.

### Acción

Segundo nivel: `list`, `show`, `save`, `pay`, etc.

### Opción global

Opción que va **antes** del grupo: `--data-dir`, `--json`, `--email`,
`--password`. Afecta a todo el proceso.

### Opción de acción

Opción que va **después** de la acción: `--id`, `--from`, `--amount-cents`.
Pertenece a esa operación.

### Flag

Opción booleana sin valor: `--json`, `--yes`, `--emit`, `--pretty`.

### `--`

Separador literal. **Implementado**: el parser lo reconoce y, a partir de ahí,
trata todos los tokens como posicionales, aunque empiecen con `--`. No convierte
en literal el valor de una opción; para eso se usa `--opt=valor`.

### `--opt=valor`

Forma de pasar un valor que empieza con `--`. **Implementado**. Sin `=`, un
valor que empiece con `--` se interpreta como otra opción.

### `--data-dir`

Carpeta de datos alternativa. **Implementado**. La convención es escribirla
antes del comando; el parser también la acepta después mediante `FirstText`.
Las credenciales (`--email`/`--password`) sí deben ir antes del comando para el
auto-login.

### `--input`

Archivo JSON con la entrada de una operación. **Implementado**.

### `--stdin`

Entrada JSON por la entrada estándar. **Implementado** en el parser; no está
cubierto por las pruebas.

### `--json`

Salida JSON legible por máquina. **Implementado**. Éxito va a stdout; error a
stderr con código 1.

### `--pretty`

Formato JSON indentado. **Parseado pero no usado**: no cambia la salida.

### `--no-color`

Desactiva color. **Parseado pero no usado**.

### `--quiet`

Silencia salida humana. **Parseado pero no usado**.

### `--all`

Incluye inactivos/archivados en algunos listados. **Parseado pero no usado** en
la implementación actual; cada comando decide sus inclusiones.

### `--yes`

Confirma una operación destructiva, como `backup restore`. **Implementado**.

### `--emit`

Emite una factura al guardarla. **Implementado** en `invoices save`.

### `--new-password`

Contraseña para crear, resetear o cambiar un usuario. **Implementado**. Se usa
para no colisionar con `--password` del login.

### `--password-env`

Nombre de la variable de entorno que contiene la contraseña. **Implementado**
en el parser; no cubierto por pruebas.

### stdout

Salida estándar. **Implementado**: éxito JSON o texto humano.

### stderr

Salida de error. **Implementado**: error JSON o texto humano.

### Código de salida

`0` éxito, `1` error controlado, `2` error de uso/argumentos en algunos casos.
**Implementado**.

### REPL

Read-Eval-Print Loop: modo interactivo. **Implementado** con `libro-cli` sin
argumentos o con `--repl`.

### Tokenizer

Divisor de la línea del REPL en argumentos, respetando comillas y barras de
Windows. **Implementado** en `CliContext.Tokenize`.

### `doctor`

Diagnóstico de base, llave, cifrado, conteos y versión. **Implementado**. No
crea `clave.bin` si falta.

### `schema`

Catálogo de ejemplos JSON por comando. **Implementado**.

### `help`

Ayuda global, de grupo o de acción. **Implementado**. Se intercepta antes de
abrir la base.

### Autenticación automática

Uso de `--email`/`--password` globales o variables `LIBRO_EMAIL`/`LIBRO_PASSWORD`
para iniciar sesión en cada invocación. **Implementado**.

### One-shot

Ejecución de un solo comando y salida. **Implementado**.

### Idempotencia

Propiedad de que repetir una operación no cambie el resultado. **No garantizada**
en general: `save` reemplaza, `pay` puede duplicar pagos, `backup create` crea
otro archivo.

### Reemplazo completo

Operación `save` que escribe todos los campos, no solo los enviados.
**Implementado** en varios comandos. Exige leer, modificar y volver a guardar.

### Efecto secundario

Cambio que no es la salida directa: auditoría, stock, gasto, respaldo.
**Implementado** en muchas operaciones.

### Aislamiento de pruebas

Uso de una carpeta temporal y `--data-dir` para no tocar datos reales.
**Implementado** en `test_cli.py`.

### `LIBRO_DATA_DIR`

Variable de entorno que cambia la carpeta de datos de la app y del CLI.
**Implementado**.

### `LIBRO_EMAIL` / `LIBRO_PASSWORD`

Credenciales para el CLI. **Implementado**.

### `LIBRO_KEEP_TEST_DATA`

Conserva la carpeta temporal de pruebas. **Implementado**.

### `LIBRO_SELFTEST_LOG`

Ruta del log de la autoprueba. **Implementado**.

---

## 11. Instalación y Windows

### Instalador

Programa que copia la aplicación y crea accesos directos. **Implementado** con
Inno Setup.

### Inno Setup

Herramienta para generar instaladores de Windows. **Implementado** en
`installer/libro-native.iss`.

### `AppId`

Identificador estable de la aplicación para actualizaciones y desinstalación.
**Implementado**. El mismo en 1.0.0 y 2.0.0.

### Instalación por usuario

Instalación sin privilegios de administrador, en el perfil del usuario.
**Implementado** (`PrivilegesRequired=lowest`).

### `%LOCALAPPDATA%`

Carpeta local del perfil de Windows. **Implementado** como raíz de la app y los
datos.

### `{app}`

Carpeta de instalación de Inno (`%LOCALAPPDATA%\Programs\Libro`).
**Implementado**.

### Staging

Carpeta temporal con los archivos que se empaquetan
(`installer/native-app`). **Implementado**.

### Publicación / publish

Salida de `dotnet publish`: binarios, dependencias y runtime.
**Implementado**.

### Self-contained

Publicación que incluye el runtime .NET; el destino no necesita .NET instalado.
**Implementado**.

### Runtime

Conjunto de bibliotecas que ejecutan la app. **Implementado** (.NET 8).

### `win-x64`

Identificador de runtime de Windows 64 bits. **Implementado**.

### `x64compatible`

Arquitectura aceptada por Inno: x64 nativo o ARM64 con emulación x64.
**Implementado**.

### ARM64

Arquitectura de algunos equipos Windows. **No hay build nativo**; se ejecuta con
emulación x64.

### UAC

User Account Control. **No se activa**: la instalación es por usuario.

### Desinstalador

Programa que elimina la app. **Implementado** (`unins000.exe`).

### `uninsneveruninstall`

Bandera de Inno que impide que el desinstalador borre una carpeta.
**Implementado** en `{localappdata}\Libro` y `...\data`.

### `UninstallDelete`

Sección que borra rutas al desinstalar. **Implementado** solo para `{app}` y
`{group}`.

### `InstallDelete`

Sección que borra archivos antes de instalar. **Implementado** para limpiar el
legado.

### Restart Manager

Mecanismo de Windows para cerrar aplicaciones que usan archivos en uso.
**Implementado** con `CloseApplications=yes`.

### `AppMutex`

Mutex que permite a Inno detectar una instancia abierta. **No implementado**.

### SmartScreen

Ver §9.

### LZMA2

Algoritmo de compresión de Inno. **Implementado** (`lzma2/max`).

### SHA256

Hash criptográfico de 256 bits. **Implementado** para publicar la huella del
setup.

### Huella / hash publicado

Valor que permite verificar que el instalador no fue alterado.
**Implementado** en `dist/*.sha256`.

### Firma / Authenticode

Ver §9.

### Reproducibilidad

Capacidad de obtener el mismo binario byte a byte. **No garantizada** en el
setup por timestamps y compresión.

### Upgrade

Actualización sobre una instalación existente. **Implementado** con el mismo
`AppId` y carpeta.

### Downgrade

Instalar una versión anterior. **No soportado** de 2.0.0 a 1.0.0.

### Migración

Conversión de la base plana a SQLCipher. **Implementado** en el primer arranque.

### Primer arranque

Primera ejecución de la app, que crea llave, base, esquema y semillas.
**Implementado**.

### Seed / semilla

Datos iniciales (13 categorías y 2 cuentas). **Implementado**.

### `SetupWindow`

Ventana de alta inicial de empresa y administrador. **Implementado**.

### `LoginWindow`

Ventana de inicio de sesión. **Implementado**.

### `MainWindow`

Ventana principal con navegación. **Implementado**.

---

## 12. Fechas, formatos y tipos

### Fecha ISO

Texto `yyyy-MM-dd` (`2026-10-25`). **Implementado** para fechas de negocio.

### Timestamp ISO

Texto `yyyy-MM-ddTHH:mm:ss.fffZ` en UTC. **Implementado** en las marcas nuevas
de C#.

### Timestamp SQLite

Texto `yyyy-MM-dd HH:mm:ss` en UTC. **Implementado** por los valores por defecto
de SQLite.

### UTC

Tiempo universal coordinado. **Implementado** en timestamps.

### Zona horaria de México

Zona usada para mostrar fechas y para calcular “hoy”. **Implementado** con
reserva a la zona local.

### `es-MX`

Cultura de formato en español de México. **Implementado** para dinero y fechas.

### `dd/MM/yyyy`

Formato de entrada del control `DateField`. **Implementado**.

### `d/M/yyyy`

Variante sin ceros. **Implementado**.

### Centavos

Ver §3.

### `INTEGER`

Tipo SQLite para enteros. **Implementado** para dinero, cantidades entero y IDs.

### `REAL`

Tipo SQLite para punto flotante. **Implementado** para tasas, cantidades y
stock.

### `long`

Entero de 64 bits de C#. **Implementado** para centavos.

### `decimal`

Tipo de C# para dinero. **Parcial**: se usa para formatear, no para almacenar.

### Redondeo

Regla para ajustar fracciones. **Implementado** con
`MidpointRounding.AwayFromZero` en IVA e ISR.

### `AwayFromZero`

Redondeo del punto medio hacia arriba en valor absoluto. **Implementado**.

### `yyyy-MM-dd`

Formato canónico de fecha. **Implementado**.

### `yyyyMMdd-HHmmss`

Formato de nombre de respaldo. **Implementado**.

---

## 13. Términos del proyecto

### Libro

Nombre del producto y de la empresa editora. **Vigente** en 2.0.0.

### Libro 2.0.0

Aplicación nativa WPF. **Vigente**.

### Libro 1.0.0

Aplicación Next.js. **Legado**.

### `Libro.Desktop`

Proyecto WPF. **Vigente**.

### `Libro.Cli`

Proyecto de consola que referencia `Libro.Desktop`. **Vigente**.

### `libro-cli`

Nombre del ejecutable/DLL del CLI y del wrapper `.cmd`. **Vigente**.

### Harness

Conjunto de CLI, pruebas, scripts y documentación para agentes.
**Implementado** en `agent-harness/`.

### Skill

Instrucciones empaquetadas para un agente. **Implementado** en
`agent-harness/skills/cli-anything-libro/SKILL.md`.

### CLI-Anything

Metodología de 7 fases para construir un harness de CLI sobre una app.
**Aplicada** al proyecto.

### Graphify

Herramienta que construye el grafo de conocimiento del repositorio.
**Implementado** en `graphify-out/`.

### Grafo

Representación de nodos (archivos, símbolos, conceptos) y aristas.
**Generado**.

### Nodo / arista

Elemento y relación del grafo. **Generado**.

### Comunidad

Grupo de nodos densamente conectados. **Generado**.

### `EXTRACTED` / `INFERRED`

Etiquetas de las aristas del grafo: presentes en el código o inferidas por la
herramienta. **Generado**.

### Evidencia

Salida reproducible que respalda una afirmación. **Implementado** en
`graphify-out/` y en los resultados de prueba.

### Documentación curada

Documentos de `docs/` escritos y revisados por humanos/agentes, no volcados
automáticos. **Vigente**.

### Wiki generada

Markdown producido por Graphify. **Generado**; no sustituye a `docs/`.

### `H-nn`

Identificador consolidado de hallazgo en
[13](13-hallazgos-riesgos.md). **Implementado**.

### `INS-nn`

Hallazgo local del instalador en [08](08-operacion-instalador.md).

### `TST-nn`

Brecha local de pruebas en [09](09-pruebas.md).

### `LEG-nn`

Hallazgo local del legado en [11](11-legado-nextjs.md).

### `UI-nn`

Defecto local de interfaz en [06](06-mapa-ui.md).

### `SEC-nn`

Hallazgo local de seguridad en [04](04-seguridad.md).

### `FUN-nn`

Inconsistencia funcional local en [05](05-modulos-flujos.md).

### `CLI-nn`

Hallazgo local del CLI en [07](07-cli.md).

---

## 14. Acrónimos

| Acrónimo | Expansión | Uso en Libro |
|---|---|---|
| API | Application Programming Interface | CLI y API legada. |
| CFDI | Comprobante Fiscal Digital por Internet | **No implementado**. |
| CLI | Command Line Interface | `libro-cli`. |
| CSV | Comma-Separated Values | Exportaciones. |
| CURP | Clave Única de Registro de Población | Dato de empleado. |
| DPAPI | Data Protection API | Protección de la llave. |
| IEPS | Impuesto Especial sobre Producción y Servicios | No implementado. |
| IMSS | Instituto Mexicano del Seguro Social | Estimación en nómina. |
| Infonavit | Instituto del Fondo Nacional de la Vivienda para los Trabajadores | Deducción manual. |
| ISR | Impuesto sobre la Renta | Retención estimada en nómina. |
| IVA | Impuesto al Valor Agregado | Cálculo en ventas/gastos. |
| JSON | JavaScript Object Notation | Contrato del CLI. |
| MXN | Peso mexicano | Moneda. |
| NSS | Número de Seguridad Social | Dato de empleado. |
| PAC | Proveedor Autorizado de Certificación | No implementado. |
| PDF | Portable Document Format | No se genera. |
| REPL | Read-Eval-Print Loop | Modo interactivo. |
| RFC | Registro Federal de Contribuyentes | Dato. |
| SAT | Servicio de Administración Tributaria | No hay conexión. |
| SHA256 | Secure Hash Algorithm 256 bits | Huella del setup y de respaldos. |
| SKU | Stock Keeping Unit | Clave de producto. |
| SQL | Structured Query Language | Base de datos. |
| SQLCipher | SQLite cifrado | Base nativa. |
| UI | User Interface | Interfaz WPF. |
| UAC | User Account Control | No se activa. |
| UTC | Coordinated Universal Time | Timestamps. |
| WAL | Write-Ahead Logging | Modo de SQLite. |
| WPF | Windows Presentation Foundation | UI nativa. |

---

## 15. Falsos amigos y confusiones frecuentes

| Expresión | Lo que **no** significa | Lo que significa en Libro |
|---|---|---|
| “Factura” | No es un CFDI. | Documento interno con folio y saldo. |
| “Nómina” | No es un CFDI de nómina ni un cálculo oficial. | Control interno de sueldos y deducciones. |
| “Impuestos” | No es una declaración. | Reporte estimado de IVA. |
| “Respaldo” | No es una copia portable a cualquier equipo. | `.libro` cifrado con la llave DPAPI. |
| “Base de datos” | No siempre es `data/` del repositorio. | En 2.0.0, `%LOCALAPPDATA%\Libro\data\contabilidad.db`. |
| “Contraseña” | No es la llave de cifrado. | Credencial de usuario; la llave es `clave.bin`. |
| “Cuenta” | No siempre es una cuenta de usuario. | Puede ser Caja, Banco o Tarjeta. |
| “Categoría” | No es un tipo de movimiento. | Clasificación de ingreso/gasto/costo. |
| “Cancelar” | No siempre borra. | Marca la factura como `cancelada` y conserva el registro. |
| “Eliminar” | No siempre es cancelar. | Borra un borrador, pago o movimiento. |
| “Archivar” | No borra. | Oculta lógicamente. |
| “Activo” | Puede ser un estado o un bien. | En catálogos, registro habilitado; en contabilidad, bien. |
| “Pago” | No es un gasto. | Dinero recibido contra una factura. |
| “Gasto” | No es un pago de factura. | Salida de dinero o consumo. |
| “Saldo” | No es el total. | Total menos pagos. |
| “Folio” | No es el ID interno. | `serie-número`. |
| “Legado” | No es “obsoleto sin valor”. | Código histórico que se conserva para migración y auditoría. |
| “Vigente” | No es “sin hallazgos”. | Producto actual, con riesgos documentados. |
| “Cifrado” | No es “firma”. | Protege confidencialidad; no autentica al editor. |
| “Firma” | No es “cifrado”. | Autentica el origen; no protege el contenido por sí sola. |

---

## 16. Índice rápido

| Si buscas… | Ve a… |
|---|---|
| Cómo se calcula el IVA | §3, [05](05-modulos-flujos.md) |
| Qué es un centavo | §3 |
| Qué es una factura interna | §3, §4 |
| Estados de factura | §7 |
| Roles y permisos | §8, [04](04-seguridad.md) |
| SQLCipher, DPAPI, scrypt | §9, [04](04-seguridad.md) |
| Comandos y JSON | §10, [07](07-cli.md) |
| Instalador y `{app}` | §11, [08](08-operacion-instalador.md) |
| Fechas y redondeo | §12 |
| Siglas | §14 |
| Confusiones | §15 |
| Hallazgos | [13](13-hallazgos-riesgos.md) |
| Decisiones | [14](14-decisiones.md) |
| Evidencia Graphify | [15](15-evidencia-graphify.md) |

---

## 17. Documentos relacionados

- [01 · Visión y alcance](01-vision-alcance.md)
- [02 · Arquitectura](02-arquitectura.md)
- [03 · Modelo de datos](03-modelo-datos.md)
- [04 · Seguridad](04-seguridad.md)
- [05 · Módulos y flujos](05-modulos-flujos.md)
- [06 · Mapa de UI](06-mapa-ui.md)
- [07 · CLI](07-cli.md)
- [08 · Operación e instalador](08-operacion-instalador.md)
- [09 · Pruebas](09-pruebas.md)
- [10 · Inventario de archivos](10-inventario-archivos.md)
- [11 · Legado Next.js](11-legado-nextjs.md)
- [13 · Hallazgos y riesgos](13-hallazgos-riesgos.md)
- [14 · Decisiones](14-decisiones.md)
- [15 · Evidencia Graphify](15-evidencia-graphify.md)
