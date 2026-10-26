# Libro · Hallazgos y riesgos consolidados

**Corte:** 25 de octubre de 2026  
**Alcance:** aplicación nativa 2.0.0, CLI 1.0.0, instalador 2.0.0 y legado Next.js 1.0.0 archivado.  
**Fuente de verdad:** el código de `desktop/`, `agent-harness/` e `installer/`; este documento consolida y prioriza lo ya verificado en los documentos 04 a 11.

---

## 1. Cómo leer este registro

Los documentos por área usan identificadores locales para que cada hallazgo sea
rastreable en su contexto:

| Prefijo | Documento de origen | Cantidad | Uso |
|---|---|---:|---|
| `SEC-nn` | [04-seguridad.md](04-seguridad.md) | 15 | Seguridad, permisos, llave y auditoría. |
| `FUN-nn` | [05-modulos-flujos.md](05-modulos-flujos.md) | 14 | Reglas contables, nómina, impuestos y alcance funcional. |
| `UI-nn` | [06-mapa-ui.md](06-mapa-ui.md) §8 | 20 | Defectos y rarezas de la interfaz WPF. |
| `CLI-nn` | [07-cli.md](07-cli.md) §17 | 43 | Comportamiento del harness y del parser. |
| `INS-nn` | [08-operacion-instalador.md](08-operacion-instalador.md) | 14 | Instalador, migración, empaquetado y build. |
| `TST-nn` | [09-pruebas.md](09-pruebas.md) | 18 | Brechas de la suite E2E y del self-test. |
| `LEG-nn` | [11-legado-nextjs.md](11-legado-nextjs.md) | 14 | Aplicación Next.js archivada. |

Este documento asigna un identificador consolidado `H-01..H-83`. Un `H-nn`
puede agrupar varios identificadores locales cuando comparten causa, impacto y
acción. La correspondencia completa está en la sección 7; los identificadores
locales aparecen también en su documento de origen, en el mismo orden.

**Regla de precedencia:** si una afirmación de `README.md`,
`RELEASE-NOTES-2.0.0.md` o del legado contradice el código vigente, manda el
código y se registra aquí. `H-01` es el caso de referencia: la impresión de
facturas se anunciaba, pero no existe en la app nativa.

### 1.1 Severidad

| Severidad | Criterio | Respuesta esperada |
|---|---|---|
| **Alta** | Pérdida o corrupción de datos, cifras contables incorrectas, exposición de secretos, bloqueo de una operación esencial o afirmación pública falsa. | Antes del siguiente corte distribuible. |
| **Media** | Error localizado con solución manual, inconsistencia entre pantallas, validación tardía o riesgo acotado. | Siguiente iteración planificada. |
| **Baja** | Cosmético, documental, de accesibilidad o de empaquetado sin impacto contable inmediato. | Backlog priorizado. |
| **Informativa** | Límite de alcance aceptado o decisión de producto. | Documentar y revisar al cambiar el alcance. |

La severidad no es una medida de urgencia comercial. `H-01` es **Alta** porque
invalida una promesa pública y un flujo documentado, no porque dañe la base.

### 1.2 Estado

| Estado | Significado |
|---|---|
| **Abierto** | Verificado y sin corrección aplicada. |
| **Mitigado documentalmente** | El riesgo sigue en el código, pero la documentación ya no lo oculta. |
| **Aceptado** | Límite consciente de alcance; no se corrige en 2.0.0. |
| **Legado** | Solo aplica si se ejecuta `src/` o el instalador 1.0.0. |

### 1.3 Resumen

| Severidad | Total | Vigente | Legado |
|---|---:|---:|---:|
| Alta | 14 | 12 | 2 |
| Media | 36 | 34 | 2 |
| Baja | 29 | 29 | 0 |
| Informativa | 4 | 4 | 0 |
| **Total** | **83** | **79** | **4** |

> Los cuatro hallazgos de legado son `H-41`, `H-42`, `H-43` y `H-83`. No
> cuentan como deuda de la app nativa, pero sí como riesgo mientras el código,
> los datos o el instalador 1.0.0 sigan disponibles.

Los temas que más pesan, en orden:

1. **Integridad contable:** nómina recalculada, costo de ventas referencial y
   definiciones distintas de “por cobrar”.
2. **Cifras que no cuadran:** etiquetas de IVA, límites de 200 filas y sumas
   parciales.
3. **Seguridad de la llave:** pérdida de `clave.bin` y creación silenciosa de
   una llave nueva.
4. **Defensa en profundidad:** servicios que confían en que la UI o el CLI ya
   validaron permisos.
5. **Instalador y migración:** `AppId` compartido, respaldos legados y ausencia
   de migraciones incrementales.
6. **Automatización:** `save` como reemplazo total y parser laxo.

---

## 2. Contabilidad, datos y flujos (`H-01`–`H-16`)

| ID | Sev. | Hallazgo | Evidencia | Impacto | Acción / estado |
|---|---|---|---|---|---|
| **H-01** | Alta | Impresión de facturas anunciada sin implementación. | `desktop/` no contiene `PrintDialog`, `PrintDocument`, `PrintQueue`, `Imprimir` ni `impresión`; `README.md` (lista de módulos, en el corte original) y `RELEASE-NOTES-2.0.0.md` (sección “Funcionalidad incluida”, en el corte original) la anunciaban y se corrigieron el 2026-10-25; el legado usaba `window.print()` en `/imprimir/factura/[id]`. | La promesa pública existió y el flujo sigue sin estar disponible; la documentación ya advierte la limitación. | Decidir impresión/PDF o declararla fuera de alcance. **Mitigado documentalmente**, código abierto. |
| **H-02** | Media | Las etiquetas “Cobrado en ventas” y “Pagado en gastos” no son flujo de caja. | `TaxService.GetSummary` suma IVA de facturas `emitida` o `pagada` y de gastos; `DashboardView.xaml:104,112` y `TaxesView.xaml:63` usan esas etiquetas. | Se puede creer que el IVA mostrado ya se cobró o pagó; decisiones de tesorería equivocadas. | Renombrar a “IVA facturado” y “IVA de gastos”, o calcular IVA efectivamente cobrado/pagado. **Abierto**. |
| **H-03** | Media | “Por cobrar” tiene definiciones distintas. | Panel: `status='emitida' AND date <= hoy` (`DashboardService.cs:82-88`); Facturas: `status='emitida'` sin filtro de fecha (`InvoiceService.cs:196-198`); Contactos usa el saldo de facturas del contacto. | Panel, Facturas, Contactos y CLI muestran cifras distintas para el mismo negocio. | Unificar en un solo servicio y una definición documentada; prueba que compare los cuatro valores. **Abierto**. |
| **H-04** | Media | El tipo de cuenta “Otro” viola el `CHECK` del esquema. | `AccountDialog.xaml:46` ofrece `Tag="otro"`; `Schema.cs` limita `accounts.type` a `efectivo`, `banco` y `tarjeta`. | El alta falla con un error de restricción difícil de interpretar. | Quitar la opción o ampliar el esquema con migración; validar antes de guardar. **Abierto**. |
| **H-05** | Media | Se registran movimientos de inventario que no cambian el stock. | `InvoiceService` inserta `inventory_movements` para toda línea con producto, pero solo descuenta stock si `track_stock=1` (`InvoiceService.cs:386-400`); al cancelar ocurre lo mismo (`:442-455`). | La bitácora muestra salidas/entradas de productos no controlados; los reportes de movimientos no cuadran con el stock. | Insertar el movimiento solo si `track_stock=1`, o marcarlo como informativo y excluirlo de cálculos. **Abierto**. |
| **H-06** | Alta | Recalcular nómina no elimina conceptos obsoletos. | `PayrollService.CalculateInto` inserta o actualiza conceptos, pero no borra los que ya no corresponden al empleado o al periodo. | Neto inflado y nómina pagada con conceptos duplicados o de empleados dados de baja. | Borrar o marcar los conceptos generados antes de recalcular, preservando ajustes manuales; probar con baja y cambio de salario. **Abierto**. |
| **H-07** | Media | Un pago de nómina el último día del mes no entra al resumen. | `PayrollService.GetSummary` compara `paid_at >= 'inicio 00:00:00'` y `paid_at < 'fin 23:59:59'`; `paid_at` se guarda en ISO con `T`. | El resumen mensual omite pagos del último día. | Usar un rango semiabierto con la misma representación o comparar por `date(paid_at)`; prueba de borde. **Abierto**. |
| **H-08** | Baja | Dar de baja a un empleado no fija `end_date`. | `EmployeeService.ToggleEmployee` cambia `status`, pero no `end_date`. | Historial y filtros por fecha incompletos; el empleado puede seguir apareciendo en periodos. | Fijar `end_date` al dar de baja y limpiarlo o ajustarlo al reactivar. **Abierto**. |
| **H-09** | Media | Editar un usuario aplica cambios antes de validar la contraseña nueva. | `UserService.SaveUser` ejecuta el `UPDATE` de nombre, correo, rol y estado y después valida `password` (`UserService.cs:39-83`). | Si la contraseña es inválida, el perfil o el rol ya cambiaron; queda un estado parcial. | Validar la contraseña antes de cualquier escritura y envolver todo en una transacción. **Abierto**. |
| **H-10** | Baja | Una factura de total cero queda atrapada en “emitida”. | `InvoiceService.RegisterPayment` exige monto mayor a cero y no permite pagar más que el saldo. | La factura nunca pasa a “pagada” y ensucia la cartera. | Impedir emitir total cero o permitir un cierre administrativo explícito. **Abierto**. |
| **H-11** | Informativa | No hay cuentas por pagar ni antigüedad de proveedores formales. | Solo existen saldo de facturas y saldos de cuentas; no hay tabla de obligaciones ni aging de proveedores. | No se puede planear el pago a proveedores por vencimiento. | Aceptado como alcance; documentar el uso de fechas y categorías. **Aceptado**. |
| **H-12** | Informativa | El costo de ventas es solo referencial. | `ReportService.GetIncomeStatement` calcula `CogsReferenceCents`, pero `OperatingProfitCents` no lo resta; las compras de inventario ya están en gastos. | La utilidad operativa puede distorsionarse por compras no vendidas. | Aceptado y explicado en la UI; a futuro, costo de ventas contabilizado y separación compra/inventario. **Aceptado**. |
| **H-13** | Informativa | No hay multiempresa. | `company.id=1` y una base por negocio. | No se pueden llevar dos negocios en la misma instalación. | Aceptado; una carpeta de datos por empresa. **Aceptado**. |
| **H-14** | Media | No hay periodos fiscales bloqueados. | Cualquier fecha se acepta; no existe cierre ni bloqueo de periodo. | Cambios retroactivos alteran reportes ya presentados. | Añadir cierre de periodos o, al menos, advertencia y auditoría reforzada. **Abierto**. |
| **H-15** | Alta | En el CLI, `save` es un reemplazo total. | Los comandos construyen la entidad desde flags o `--input` y los servicios hacen `UPDATE` de todos los campos; lo omitido vuelve al default o a cero. | Un script que envía solo un campo puede borrar los demás. | Actualización parcial (`PATCH`) o exigir el objeto completo con validación; documentar y ofrecer `--dry-run`. **Abierto**. |
| **H-16** | Media | Varios `save` y diálogos ignoran campos de estado. | `products save` ignora `initialStock` y `active` en edición; `contacts save` ignora `archived`; `CompanyService.SaveAccount`/`SaveCategory` fuerzan `active=1`/`archived=0` en alta aunque el diálogo o el CLI envíen otro valor. | El usuario cree que actualizó o desmarcó un estado y el cambio no se aplicó. | Respetar los campos o rechazarlos con un error explícito; documentar qué campos son de solo alta. **Abierto**. |

---

## 3. Seguridad (`H-17`–`H-28`)

| ID | Sev. | Hallazgo | Evidencia | Impacto | Acción / estado |
|---|---|---|---|---|---|
| **H-17** | Media | El bloqueo de login vive solo en memoria. | `AuthService` usa un `static Dictionary` de intentos por correo. | Reiniciar el proceso borra el bloqueo; la fuerza bruta se vuelve trivial. | Persistir intentos con ventana temporal o usar un límite por cuenta y equipo. **Abierto**. |
| **H-18** | Media | La política de contraseñas solo exige 8 caracteres. | `Validation.ValidatePassword` comprueba longitud, no complejidad ni contraseñas filtradas. | Contraseñas débiles, sobre todo en el administrador. | Subir el mínimo, añadir lista de denegadas y medidor; no romper usuarios existentes. **Abierto**. |
| **H-19** | Media | No hay firma de código. | `build-native.ps1` no usa `SignTool`; `[Setup]` no define `SignTool`; el legado tampoco. | SmartScreen advierte y no hay autenticidad criptográfica del instalador. | Firmar setup y ejecutable con certificado y timestamp; publicar huellas. **Abierto**. |
| **H-20** | Alta | La llave DPAPI no tiene respaldo ni rotación. | `DpapiKeyStore` guarda `clave.bin`; no hay exportación, copia ni rotación; los respaldos dependen de la misma cuenta de Windows. | Perder `clave.bin` equivale a perder la base; los respaldos no son portables. | Documentar el respaldo de `clave.bin` en medio seguro; a futuro, frase de recuperación o clave envuelta. **Abierto**. |
| **H-21** | Alta | El arranque crea una `clave.bin` nueva si falta, aunque la base esté cifrada. | `DpapiKeyStore.GetOrCreate` genera una clave nueva cuando el archivo no existe. | La app arranca, falla al abrir y el usuario puede creer que la base está corrupta; la clave original no se recupera. | Si existe base cifrada y no hay clave, abortar con un mensaje de recuperación; no crear clave. **Abierto**. |
| **H-22** | Media | `--password` expone la contraseña en la línea de comandos. | `Cli.cs` acepta `--password`; también existen `--password-env` y `LIBRO_PASSWORD`. | Historial, Process Explorer y scripts dejan la contraseña visible. | Desaconsejar `--password`; usar `--password-env`; limpiar `argv` en lo posible. **Abierto**. |
| **H-23** | Baja | La auditoría es editable por el dueño de la base. | `audit_log` vive en la misma base SQLCipher. | No sirve como prueba forense frente al propio dueño. | Aceptado; exportar la auditoría a un destino externo si se requiere. **Aceptado**. |
| **H-24** | Baja | `libro.log` no rota ni redacta. | `AppLog` escribe sin límite; el legado hacía lo mismo con `server.log`. | Crecimiento del disco y fuga de rutas o trazas. | Rotación por tamaño, retención y revisión de datos sensibles. **Abierto**. |
| **H-25** | Media | No hay bloqueo por inactividad, MFA ni uso de `sessions`. | No existe temporizador de inactividad; `sessions` solo aparece en el esquema. | Un equipo desatendido queda con la sesión abierta. | Bloqueo por inactividad y re-autenticación para acciones destructivas; MFA fuera del alcance local. **Abierto**. |
| **H-26** | Media | La migración de texto plano deja un `.plano` temporal. | `Database.MigratePlaintextDatabase` mueve el original a `.plano` y lo borra al verificar; si falla, puede quedar. | Residuo sin cifrar con datos reales. | Borrado seguro o cifrado del `.plano`; restaurar y avisar en caso de fallo. **Abierto**. |
| **H-27** | Alta | `CompanyService` no revalida permisos. | `SaveCompany` no llama a `EnsureManager`; la UI y el CLI confían en el gate del llamador. | Un nuevo punto de entrada puede escribir configuración sin `settings.manage`. | Validar el permiso en el servicio (defensa en profundidad) y probarlo. **Abierto**. |
| **H-28** | Alta | Varias lecturas de módulos no verifican permiso explícito. | Los servicios de nómina, facturas, contactos e inventario no comprueban permiso de lectura. | Un rol `consulta` o `capturista` puede leer más de lo declarado si llega a la pantalla o al CLI. | Añadir `EnsureCanRead` por módulo o documentar la matriz real y probarla. **Abierto**. |

---

## 4. Instalador, empaquetado y migración (`H-29`–`H-43`)

| ID | Sev. | Hallazgo | Evidencia | Impacto | Acción / estado |
|---|---|---|---|---|---|
| **H-29** | Alta | El instalador 2.0.0 comparte `AppId` con 1.0.0. | Ambos `.iss` usan `{28083929-8EB6-45E7-A6CD-D27B813BCC34}`. | Un downgrade o una instalación legada puede reemplazar la app nativa y confundir los datos. | Bloquear 1.0.0 si existe 2.0.0 o usar un `AppId` distinto para el legado; conservar `LEEME.txt`. **Abierto**. |
| **H-30** | Media | La versión está hardcodeada en tres lugares. | `Libro.Desktop.csproj`, `libro-native.iss` (`MyAppVersion`) y `build-native.ps1` (`$Version`). | Un bump incompleto produce un setup con versión o nombre inconsistentes. | Fuente única y validación en el build. **Abierto**. |
| **H-31** | Media | `Libro.exe --self-test` siempre sale con código 0. | `SelfTest.Run` captura excepciones y `Shutdown()` no recibe código. | Un pipeline que solo mira el exit code acepta una autoprueba fallida. | Devolver código 1 en fallo; mantener la comprobación de `SELFTEST_OK`. **Abierto**. |
| **H-32** | Alta | Los respaldos legados `.db` no se migran ni se listan como restaurables. | `SettingsView` filtra `*.libro;*.db`, pero `RestoreFrom` abre con la clave SQLCipher. | El usuario cree que sus respaldos viejos están disponibles y descubre que no. | Importador de `.db` plano o conversión guiada; documentar la limitación. **Abierto**. |
| **H-33** | Media | El `.plano` no se restaura si falla la verificación posterior. | `MigratePlaintextDatabase` mueve el original a `.plano`, mueve el cifrado y verifica; el `catch` solo restaura si falta la base. | Recuperación manual y mensaje engañoso sobre el estado del original. | Restaurar `.plano` siempre que exista y la base nueva no verifique; ajustar el mensaje. **Abierto**. |
| **H-34** | Media | No hay respaldo automático antes de migrar. | `InitializeWizard` solo informa; la migración ocurre al primer arranque y borra `.plano` al éxito. | Si el usuario no respaldó, no hay vuelta atrás al texto plano. | Conservar una copia `.plano` o crear un respaldo `.libro` antes de migrar. **Abierto**. |
| **H-35** | Media | La guardia del build solo rechaza `*.db`. | `build-native.ps1` busca `*.db`; no busca `clave.bin`, `*.libro`, `*.db-wal` ni `libro.log`. | Un staging contaminado podría distribuir secretos. | Ampliar la lista y fallar el build; revisar el staging antes de compilar. **Abierto**. |
| **H-36** | Baja | `libro.ico` no está en el staging. | `[Icons]` usa `{app}\libro.ico`; `installer/native-app` no lo incluye y `[InstallDelete]` borra el icono legado. | Los accesos directos pueden mostrar un icono genérico. | Incluir el icono o usar `{app}\Libro.exe` como `IconFilename`. **Abierto**. |
| **H-37** | Baja | No hay build ARM64 nativo. | `win-x64` y `x64compatible`. | En ARM64 se ejecuta bajo emulación. | Publicar `win-arm64` si se requiere soporte nativo. **Abierto**. |
| **H-38** | Baja | La lista de culturas es manual y el log no lleva BOM. | `build-native.ps1` enumera 12 culturas; `SelfTest` escribe UTF-8 sin BOM y PowerShell 5.1 lo muestra con mojibake. | Paquete más grande y lectura confusa de acentos. | Revisar culturas en cada SDK; usar `Get-Content -Encoding utf8`. **Abierto**. |
| **H-39** | Baja | No hay `AppMutex` ni parada explícita de la app nativa. | `[Setup]` no define `AppMutex`; `PrepareToInstall` solo actúa sobre el legado. | Si la app está abierta, la copia puede fallar o requerir Restart Manager. | Añadir `AppMutex` o documentar el cierre manual. **Abierto**. |
| **H-40** | Alta | No hay migraciones incrementales de esquema. | `Schema.Sql` usa `CREATE TABLE IF NOT EXISTS`; `user_version` solo se escribe. | Una futura 2.x no actualizará bases existentes. | Implementar migraciones versionadas antes de cambiar el esquema. **Abierto**. |
| **H-41** | Alta (legado) | La base y los respaldos del legado están en texto plano. | `src/lib/db.ts` no aplica cifrado; `backups.ts` copia `.db`; el repo conserva `data/` y `backups/legacy-pre-native-20261023/` con SQLite plano y datos reales. | Cualquiera con acceso al disco lee todo; riesgo de distribuir datos si se copia el repo. | No distribuir `data/` ni `backups/`; migrar y borrar copias planas. **Abierto**. |
| **H-42** | Alta (legado) | El legado expone red y sesión. | `start-server.cmd` y `npm run start:lan`; cookie `Secure` solo con `COOKIE_SECURE=1`; `/api/respaldo/[archivo]` descarga respaldos. | Credenciales y datos en la LAN; exfiltración de respaldos. | No ejecutar el legado; si se hace, loopback, TLS y `COOKIE_SECURE=1`. **Abierto**. |
| **H-43** | Media (legado) | La operación y el rebuild del legado dependen del entorno. | `Launcher.cs` usa WMI para `--stop`; `build.ps1` exige `csc.exe` de .NET Framework y Node. | `--stop` puede fallar; el rebuild no es reproducible en cualquier equipo. | Archivar el artefacto y documentar requisitos; no reiniciar el legado. **Aceptado**. |

---

## 5. Interfaz de escritorio (`H-44`–`H-56`)

| ID | Sev. | Hallazgo | Evidencia | Impacto | Acción / estado |
|---|---|---|---|---|---|
| **H-44** | Baja | Mojibake en `TransactionDialog`. | El código contiene `facturaciÃ³n`, `DomiciliaciÃ³n`, `descripciÃ³n` y `categorÃ­a`; el subtítulo de XAML está correcto, pero el constructor lo sobrescribe, y la lista de métodos y las validaciones solo existen en el código. | Subtítulo, opción de pago y validaciones con texto roto. | Corregir los literales y añadir una prueba de cadenas. **Abierto**. |
| **H-45** | Media | Hay etiquetas de acción fijas que no reflejan el estado. | `UsersView.xaml:137` usa `Content="Activar"`; `InventoryView.xaml:197` usa `Content="Desactivar"`; `EmployeesView.xaml:151` usa `Content="Baja"`, sin disparadores. | Un usuario activo puede mostrar “Activar” y la acción real es desactivar; lo mismo al revés. | Enlazar a `ToggleLabel`/`StatusLabel` como ya hacen otras vistas. **Abierto**. |
| **H-46** | Baja | Los botones de creación del Panel se ven para roles de solo lectura. | `DashboardView.xaml:17-20` muestra “Nueva factura” y “Nuevo movimiento” sin comprobar permiso. | El rol `consulta` ve acciones que no puede ejecutar. | Ocultar o deshabilitar por permiso. **Abierto**. |
| **H-47** | Baja | “Usuarios” es una pantalla compartida con Auditoría. | `MainWindow.xaml.cs:52` añade “Usuarios” con `UsersManage` o `AuditView`; `UsersView` muestra solo auditoría si no hay `UsersManage`. | Un Contador entra a “Usuarios” y solo ve auditoría; navegación confusa. | Renombrar a “Auditoría” cuando el rol no administra usuarios, o separar las vistas. **Abierto**. |
| **H-48** | Baja | No hay filtro “Vencidas” y “Emitidas” mezcla estados. | `InvoicesView.xaml:77-83` ofrece Todas, Borradores, Emitidas, Pagadas y Canceladas; “Emitidas” incluye las vencidas. La tabla sí muestra “Vencida” mediante el disparador de `IsOverdue`. | No se puede aislar la cartera vencida desde el filtro. | Añadir “Vencidas” como filtro derivado o combinar estado y vencimiento. **Abierto**. |
| **H-49** | Media | Movimientos y Facturas se cortan en 200 filas. | `InvoicesView.xaml.cs:42` y `TransactionsView.xaml.cs:56` usan `limit: 200`; el pie dice “mostrando N”. | Se ocultan filas sin paginación ni aviso destacado. | Paginación real o aviso con enlace a la exportación. **Abierto**. |
| **H-50** | Media | El saldo del pie de Facturas es parcial. | `InvoicesView.xaml.cs:65` calcula `SumText` sobre `page.Rows`, no sobre todo el filtro. | “Saldo listado” no representa el universo filtrado. | Calcular el saldo en SQL sobre el filtro completo. **Abierto**. |
| **H-51** | Baja | Los CSV exportan estados y orígenes crudos. | `ReportsView.xaml.cs:199+` escribe `row.Type`, `row.Origin` y enums sin traducir. | CSV menos legible y propenso a errores de interpretación. | Mapear a etiquetas en español y documentar las columnas. **Abierto**. |
| **H-52** | Media | No hay accesibilidad declarada. | Cero `AutomationProperties`; no hay `TabIndex` ni `LabeledBy`; los iconos dependen de tooltips. | Lectores de pantalla y navegación por teclado limitados. | Añadir nombres accesibles, orden de tabulación y foco visible; probar con Narrator. **Abierto**. |
| **H-53** | Baja | El diálogo de factura se bloquea si no hay clientes. | `InvoiceDialog.xaml:49` exige un cliente existente y no permite crearlo. | Obliga a cerrar y ir a Contactos; se pierde el contexto. | Añadir alta rápida de cliente en el diálogo. **Abierto**. |
| **H-54** | Media | El diálogo de factura no avisa del stock. | `InvoiceDialog` no consulta existencias; `InvoiceService.Emit` rechaza al emitir (`InvoiceService.cs:332-345`). | El error aparece tarde y el borrador queda a medias. | Validar disponibilidad al elegir producto/cantidad y avisar antes de emitir. **Abierto**. |
| **H-55** | Baja | El diálogo de pago no valida el saldo antes de guardar. | `PaymentDialog` solo valida monto mayor a cero; `RegisterPayment` rechaza el exceso. | Error tardío después de capturar. | Validar contra `invoice.BalanceCents` en el diálogo. **Abierto**. |
| **H-56** | Baja | Una nómina pagada se puede intentar eliminar desde la lista. | `PayrollView.xaml.cs:86` muestra “Eliminar”; `DeletePeriod` rechaza si está pagada. | Confirmación inútil y error posterior. | Ocultar o deshabilitar el botón según el estado. **Abierto**. |

---

## 6. CLI y automatización (`H-57`–`H-71`)

| ID | Sev. | Hallazgo | Evidencia | Impacto | Acción / estado |
|---|---|---|---|---|---|
| **H-57** | Media | El parser es laxo con opciones y posiciónales. | `Cli.cs` guarda opciones desconocidas y descarta posiciónales extra. | Un typo como `--emial` no falla y la operación usa defaults. | Lista blanca por comando y error en opciones/posiciónales desconocidos; modo `--strict`. **Abierto**. |
| **H-58** | Baja | `--opt=valor` y flags sin efecto. | Un valor que empieza con `--` exige `=`; `--pretty`, `--no-color`, `--quiet` y `--all` se aceptan pero no se usan. | Confusión y falsas expectativas. | Implementar o eliminar los flags; documentar `--` y `=`. **Abierto**. |
| **H-59** | Media | El alcance de las opciones globales no es uniforme. | `--data-dir` después del comando funciona por `FirstText`, pero la convención es antes; las credenciales después del comando no sirven para auto-login. | Auto-login silenciosamente ausente. | Validar la posición o aceptar credenciales después de forma explícita; error si se detectan. **Abierto**. |
| **H-60** | Baja | La ayuda por acción y los esquemas son incompletos. | `help <grupo> <acción>` repite la ayuda del grupo; `schema payroll.item.update`, `payment.create` y `user.save` son descriptivos y esos comandos usan flags. | El agente espera `--input` y falla. | Ayuda por acción y esquema que refleje los flags reales. **Abierto**. |
| **H-61** | Media | `auth status` y `logout` tienen efectos laterales. | `auth status` inicializa la base; `logout` no impide un auto-login posterior si hay credenciales globales o de entorno. | Creación de datos inesperada y cierre de sesión engañoso. | `status` de solo lectura; `logout` debe ignorar credenciales guardadas o advertir. **Abierto**. |
| **H-62** | Media | Las restricciones de edición se descubren tarde. | `transactions save/delete` rechazan movimientos automáticos; `invoices save/delete` solo aceptan borradores; `invoices pay` no exige una cuenta activa. | Errores después de capturar y pagos sin cuenta. | Validar antes y documentar la máquina de estados; exigir cuenta activa. **Abierto**. |
| **H-63** | Media | `payroll update-item` pone en cero todo numérico omitido. | El comando reconstruye el concepto con defaults 0. | Pérdida de importes si se envía solo un campo. | Actualización parcial o exigir todos los campos. **Abierto**. |
| **H-64** | Baja | `users save` reactiva siempre. | Salvo `--inactive`, el comando fuerza el estado activo. | Un script puede reactivar sin querer. | Distinguir alta y edición; exigir `--active` o `--inactive` al editar. **Abierto**. |
| **H-65** | Alta | `company update` no valida nada. | `CompanyService.SaveCompany` no valida IVA, folio, RFC, nombre ni correo. | Configuración inválida rompe facturación, folios o reportes. | Validar en el servicio y en el CLI; probar límites. **Abierto**. |
| **H-66** | Baja | `backup list` y `audit list` tienen alcance limitado. | `backup list` solo mira la carpeta estándar; `audit list` ignora `--offset`. | No se ven respaldos externos ni se pagina la auditoría. | Añadir `--dir` y un offset real. **Abierto**. |
| **H-67** | Baja | `balances` y `low-stock` usan criterios distintos. | `accounts balances` y `reports balances` excluyen inactivas por defecto; `low-stock` incluye agotados y `summary.lowStockCount` no. | Cifras que no cuadran entre comandos. | Documentar y unificar criterios; `--include-inactive` explícito. **Abierto**. |
| **H-68** | Baja | Las ventanas de fecha por defecto son inconsistentes. | `taxes summary` y `reports income/cashflow` usan el mes actual; `reports monthly` usa 12 meses. | Comparaciones involuntarias entre periodos distintos. | Documentar y aceptar `--from/--to`; valorar `--period`. **Abierto**. |
| **H-69** | Baja | El CLI no exporta CSV ni PDF. | Solo la UI exporta CSV; el CLI devuelve JSON o tablas. | Automatización limitada para reportes. | Añadir `--csv`/`--format` o documentar JSON + herramienta externa. **Abierto**. |
| **H-70** | Media | Restauración y concurrencia tienen efectos secundarios. | `restore` exige `--yes` (correcto), pero no cierra la REPL; WAL y dos CLIs pueden producir `database is locked`. | Sesión apuntando a un usuario inexistente; escrituras perdidas. | Reiniciar el contexto tras restaurar o cerrar la REPL; bloquear escritura concurrente; documentar el cierre de la app. **Abierto**. |
| **H-71** | Informativa | El alcance del CLI es deliberadamente acotado. | Solo Windows, solo español, sin CFDI/PAC, sin API HTTP y sin sesión persistente. | Expectativas incorrectas en integradores. | Aceptado; documentarlo en la ayuda y en la skill. **Aceptado**. |

---

## 7. Pruebas, calidad y legado (`H-72`–`H-83`)

| ID | Sev. | Hallazgo | Evidencia | Impacto | Acción / estado |
|---|---|---|---|---|---|
| **H-72** | Media | La suite depende del orden y comparte estado. | `test_cli.py` usa `self.base` y `self.admin` sin `setUp`/`tearDown` por prueba; los 11 métodos están ordenados. | Un test aislado falla; un fallo temprano contamina los siguientes. | Fixtures independientes o transacciones; documentar el orden. **Abierto**. |
| **H-73** | Alta | No hay pruebas automatizadas de la UI. | No hay UI Automation ni capturas de humo. | Regresiones visuales y de navegación pasan inadvertidas. | Humo con capturas y UI Automation para los flujos críticos. **Abierto**. |
| **H-74** | Media | No hay pruebas automatizadas del instalador. | No existe una VM o pipeline que instale, abra, desinstale y verifique datos. | Un cambio en Inno puede romper la instalación sin que la suite falle. | Pipeline en VM con esas cuatro fases. **Abierto**. |
| **H-75** | Baja | No se mide cobertura. | No hay `coverlet` ni umbral por capa. | No se sabe qué servicios se ejercitan. | Cobertura y umbral por capa. **Abierto**. |
| **H-76** | Media | Muchos comandos no tienen prueba feliz. | Faltan casos de `cancel`, `delete-payment`, `recalc`, `update-item`, `audit`, entre otros. | Errores de regresión no detectados. | Matriz comando × regla. **Abierto**. |
| **H-77** | Baja | Hay una aserción ambigua. | `test_07` usa `contains="no"`. | Podría pasar con un mensaje incorrecto. | Usar una subcadena específica. **Abierto**. |
| **H-78** | Baja | La evidencia de `--help` sin base es implícita. | Depende del orden de `test_01`. | Se pierde si se reordena la suite. | Comprobación explícita de ausencia de `contabilidad.db`. **Abierto**. |
| **H-79** | Baja | Parsing y entorno no están cubiertos. | No hay pruebas de `--stdin`, `--password-env` ni `--`; `pytest` no está instalado; se usa la fecha real. | Regresiones de parser y de fin de mes. | Añadir casos, fecha inyectable y declarar `unittest` como oficial. **Abierto**. |
| **H-80** | Media | Migración y DPAPI no se prueban en escenarios reales. | Solo hay SelfTest de base plana; no se prueba otra cuenta de Windows ni otro equipo. | Un upgrade o restore puede fallar fuera del equipo de desarrollo. | Probar bases legadas y DPAPI en otra cuenta y documentar. **Abierto**. |
| **H-81** | Baja | No hay pruebas de rendimiento. | No se usan bases grandes. | Reportes lentos o bloqueos de UI no se detectan. | Medir con 10k–100k movimientos. **Abierto**. |
| **H-82** | Baja | El bloqueo de login entre procesos no se prueba. | El contador es estático y no se acumula entre procesos. | No hay evidencia automatizada del comportamiento. | Prueba de dos procesos. **Abierto**. |
| **H-83** | Media (legado) | El legado no tiene pruebas. | `src/` no contiene archivos de test. | Cualquier reejecución del legado es a ciegas. | No operar el legado; probar solo la app nativa. **Aceptado**. |

---

## 8. Fichas de prioridad Alta

Las siguientes fichas no repiten el registro; explican qué cerrar y cómo saber
que quedó cerrado. Las 14 fichas cubren los 14 hallazgos de severidad Alta,
incluidos los dos del legado.

### H-01 · Impresión de facturas anunciada sin implementación

**Qué pasa.** La app nativa 2.0.0 no tiene botón, vista, `PrintDialog`,
`PrintDocument`, `PrintQueue` ni generación de PDF. El `README.md` y las notas
de versión **anunciaban** “impresión” en el corte original; ambos se corrigieron
el 2026-10-25 y ahora advierten la limitación. El único código de impresión que
existió está en el legado: la ruta `/imprimir/factura/[id]` y `window.print()`.

**Por qué importa.** Un usuario compra o actualiza esperando imprimir facturas.
No es un fallo de cálculo, pero sí una promesa pública incumplida y un bloqueo
de flujo para quien necesita entregar un comprobante interno.

**Cierre recomendado.** Decidir explícitamente una de dos rutas:

1. **Implementar** una vista de impresión nativa con `FixedDocument` o
   generación de PDF, sin navegador ni WebView, con copia “No es CFDI”.
2. **Declarar fuera de alcance** y corregir `README.md` y
   `RELEASE-NOTES-2.0.0.md`; mantener `H-01` como limitación conocida.

**Criterio de aceptación.** `README.md` y las notas no afirman una capacidad
inexistente; si se implementa, existe una prueba que abre la vista, genera el
documento y verifica el texto “No es CFDI”.

### H-06 · Recalcular nómina deja conceptos obsoletos

**Qué pasa.** `PayrollService.CalculateInto` inserta o actualiza los conceptos
calculados, pero no elimina los que ya no corresponden. Si un empleado cambia de
salario, se da de baja o se corrige una tasa, el periodo puede conservar
conceptos de la versión anterior.

**Por qué importa.** El neto se infla y el pago genera un gasto mayor al
debido. Es un error contable silencioso: la pantalla y el recibo parecen
válidos.

**Cierre recomendado.** Antes de recalcular, borrar o marcar como obsoletos los
conceptos generados por el cálculo anterior, preservando los ajustes manuales.
Hacerlo en una transacción y registrar la diferencia en auditoría.

**Criterio de aceptación.** Un periodo recalculado después de un cambio de
salario o de una baja produce el mismo neto que un periodo creado desde cero
con los datos actuales; una prueba E2E lo demuestra.

### H-15 · `save` es reemplazo total en el CLI

**Qué pasa.** Los comandos `save` construyen la entidad desde flags o `--input`
y los servicios actualizan todos los campos. Si un script envía solo `--name`,
los demás campos vuelven a su default o a cero.

**Por qué importa.** Es la vía más directa a pérdida de datos por automatización:
un agente cree que está editando un campo y borra importes, fechas o estados.

**Cierre recomendado.** Ofrecer actualización parcial real o exigir el objeto
completo con validación y error claro. Añadir `--dry-run` para previsualizar el
cambio y documentar qué campos son obligatorios en alta y en edición.

**Criterio de aceptación.** Enviar un solo campo no altera los demás; un payload
incompleto se rechaza antes de escribir; una prueba cubre ambos casos.

### H-20 · La llave DPAPI no tiene respaldo ni rotación

**Qué pasa.** `clave.bin` se protege con DPAPI del usuario actual. No hay
exportación, copia de seguridad ni rotación. Los respaldos `.libro` dependen de
esa misma cuenta y equipo.

**Por qué importa.** Si se pierde `clave.bin`, la base y todos los respaldos son
irrecuperables. Si se pierde la cuenta de Windows, ocurre lo mismo. El usuario
suele descubrirlo cuando ya no hay vuelta atrás.

**Cierre recomendado.** Documentar y automatizar el respaldo de `clave.bin` en
un medio seguro; a futuro, exportar/importar una clave envuelta con una frase de
recuperación y un procedimiento de rotación que recifre la base.

**Criterio de aceptación.** El runbook permite restaurar en otra cuenta de
Windows con la clave de respaldo; una prueba documentada lo verifica.

### H-21 · El arranque crea una llave nueva si falta

**Qué pasa.** `DpapiKeyStore.GetOrCreate` genera una clave nueva cuando
`clave.bin` no existe, sin comprobar si ya hay una base cifrada.

**Por qué importa.** La app arranca, no puede abrir la base y deja una clave
nueva que no corresponde. El usuario puede interpretar que la base está
corrupta, borrarla o restaurar un respaldo que tampoco abrirá.

**Cierre recomendado.** Si existe `contabilidad.db` cifrada y falta la clave,
abortar con un mensaje de recuperación; nunca crear una clave en ese escenario.
`doctor` ya evita crear la clave y debe seguir siendo el diagnóstico seguro.

**Criterio de aceptación.** Base cifrada sin `clave.bin` produce un error claro,
no crea archivo y no modifica la base; prueba E2E.

### H-27 · `CompanyService` no revalida permisos

**Qué pasa.** `SaveCompany` no comprueba `settings.manage`; la UI y el CLI
validan antes de llamarlo.

**Por qué importa.** La seguridad depende de que cada llamador recuerde el gate.
Un comando nuevo, un test o una integración futura pueden escribir configuración
sin permiso.

**Cierre recomendado.** Validar el permiso dentro del servicio, además del gate
de la interfaz. Es defensa en profundidad, no duplicación inútil.

**Criterio de aceptación.** Una llamada directa al servicio con un usuario sin
`settings.manage` lanza `UnauthorizedAccessException` y no escribe.

### H-28 · Lecturas de módulos sin permiso explícito

**Qué pasa.** Los servicios de nómina, facturas, contactos e inventario no
verifican permiso de lectura. La matriz de roles promete más separación de la
que el servicio garantiza.

**Por qué importa.** Un rol `consulta` o `capturista` puede leer datos sensibles
si llega a la pantalla o al CLI. La UI puede ocultar la navegación, pero el
servicio no es la última línea de defensa.

**Cierre recomendado.** Añadir guardas de lectura por módulo o ajustar la matriz
a lo que realmente se aplica. Probar cada rol contra cada comando de lectura.

**Criterio de aceptación.** Una matriz rol × comando se ejecuta en la suite y
demuestra que cada rol solo lee lo declarado.

### H-29 · `AppId` compartido entre 1.0.0 y 2.0.0

**Qué pasa.** Los dos instaladores usan el mismo `AppId`. El legado puede
instalarse sobre la app nativa o desinstalarse afectando sus datos.

**Por qué importa.** Un downgrade accidental reemplaza la app y confunde la
carpeta de datos. La desinstalación del legado puede borrar respaldos que el
usuario cree seguros.

**Cierre recomendado.** Bloquear la instalación de 1.0.0 si detecta 2.0.0, o
usar un `AppId` distinto para el legado. Mantener el aviso de `LEEME.txt` y
probar el ciclo instalar/desinstalar en una VM limpia.

**Criterio de aceptación.** Instalar 1.0.0 sobre 2.0.0 se aborta con un mensaje
claro; desinstalar 2.0.0 no borra `%LOCALAPPDATA%\Libro\data`.

### H-32 · Los respaldos legados `.db` no se migran ni se restauran

**Qué pasa.** El diálogo de restauración acepta `*.libro;*.db`, pero
`RestoreFrom` abre el archivo con la clave SQLCipher. Un `.db` legado en texto
plano no puede abrirse así.

**Por qué importa.** El usuario cree que sus respaldos viejos están disponibles.
Al intentar restaurarlos, falla o, peor, reemplaza la base actual sin un camino
de vuelta claro.

**Cierre recomendado.** Detectar el formato antes de restaurar y explicar la
diferencia; ofrecer un importador que cifre el `.db` plano en una base nueva.
Como mínimo, no presentar los `.db` como restaurables.

**Criterio de aceptación.** Seleccionar un `.db` plano muestra un mensaje
específico y no toca la base actual; si se implementa el importador, una prueba
convierte y verifica los datos.

### H-40 · Sin migraciones incrementales de esquema

**Qué pasa.** `Schema.Sql` usa `CREATE TABLE IF NOT EXISTS`; `user_version` se
escribe pero no se lee. Una base creada por 1.x o por una 2.x anterior no
recibe columnas nuevas.

**Por qué importa.** El primer cambio de esquema posterior a 2.0.0 romperá
consultas o dejará columnas ausentes en instalaciones existentes. Es una deuda
que crece con cada versión.

**Cierre recomendado.** Introducir un motor de migraciones versionadas:
`user_version` como entrada, pasos idempotentes, respaldo previo y transacción.
No cambiar el esquema en 2.0.0 sin este mecanismo.

**Criterio de aceptación.** Una base `user_version=1` se actualiza a la versión
siguiente conservando datos; una prueba parte de una base real y verifica
conteos y columnas.

### H-41 · Datos legados en texto plano

**Qué pasa.** El legado no cifra la base; sus respaldos son copias `.db`. El
repositorio conserva `data/` y `backups/legacy-pre-native-20261023/` con SQLite
plano y datos reales.

**Por qué importa.** Cualquiera con acceso al disco o al repositorio lee todo.
Distribuir el proyecto, un ZIP o una captura de la carpeta puede filtrar datos
reales.

**Cierre recomendado.** Mover esos directorios fuera del repositorio, a un medio
seguro, y borrar las copias planas después de migrar. Añadir reglas de exclusión
y una verificación de build que falle si aparecen `.db` con datos.

**Criterio de aceptación.** El repositorio no contiene bases planas con datos
reales; el staging del instalador no las incluye; el build lo verifica.

### H-42 · Exposición de red y sesión del legado

**Qué pasa.** El legado puede escuchar en LAN (`start-server.cmd`,
`npm run start:lan`), la cookie solo es `Secure` con `COOKIE_SECURE=1` y
`/api/respaldo/[archivo]` permite descargar respaldos completos.

**Por qué importa.** En LAN, credenciales y datos viajan sin TLS; un usuario con
`backup.manage` puede exfiltrar toda la base. Es un riesgo solo si se ejecuta el
legado, pero el artefacto y las instrucciones siguen presentes.

**Cierre recomendado.** No ejecutar el legado. Si se hace, forzar loopback, TLS
y `COOKIE_SECURE=1`, y exigir autorización y auditoría en la descarga de
respaldos. Valorar retirar el instalador 1.0.0 del directorio de distribución.

**Criterio de aceptación.** El arranque por defecto escucha solo en
`127.0.0.1`; la configuración LAN exige TLS y cookie segura; la descarga de
respaldos queda autorizada y auditada.

### H-65 · `company update` no valida nada

**Qué pasa.** `CompanyService.SaveCompany` actualiza nombre, RFC, régimen,
dirección, IVA, serie, folio siguiente, IMSS e ISR sin validar.

**Por qué importa.** Un IVA fuera de `0..1`, una serie vacía o un folio negativo
rompen la facturación y los reportes. Un nombre o correo inválido degrada la
configuración sin aviso.

**Cierre recomendado.** Validar en el servicio: nombre obligatorio, RFC con
formato, IVA en rango, serie no vacía, folio positivo, correo válido y régimen
presente. Devolver errores claros al CLI y a la UI, sin escribir nada si falla.

**Criterio de aceptación.** Un payload inválido se rechaza y no modifica la
empresa; una prueba cubre cada límite.

### H-73 · Sin pruebas automatizadas de la UI

**Qué pasa.** No hay UI Automation ni capturas de humo. Los 20 defectos de
interfaz del documento 06 son exactamente el tipo de regresión que una prueba
visual detectaría.

**Por qué importa.** La suite E2E valida servicios y CLI, pero no la navegación,
los permisos visuales, los botones con etiqueta fija ni los diálogos.

**Cierre recomendado.** Empezar con cinco flujos de humo: login, alta de
movimiento, emisión y pago de factura, cálculo de nómina y respaldo/restauración.
Usar UI Automation para los elementos y capturas para la apariencia.

**Criterio de aceptación.** CI ejecuta los cinco flujos, falla si falta un
elemento o cambia una etiqueta crítica y conserva capturas como evidencia.

---

## 9. Temas transversales

### 9.1 Una sola definición por cifra

“Por cobrar”, “IVA cobrado”, “saldo listado” y “utilidad” se calculan en más de
un lugar. Cuando dos consultas no comparten definición, la discrepancia no se
detecta hasta que un usuario compara pantallas. La corrección estructural es
una capa de consultas de reporte con nombres explícitos y pruebas de
consistencia entre panel, facturas, contactos y CLI (`H-02`, `H-03`, `H-50`).

### 9.2 Defensa en profundidad

La UI y el CLI validan permisos, pero varios servicios no. Esto funciona
mientras todos los llamadores recuerden hacerlo. La corrección es mover la
autorización al servicio y dejar la UI como una comodidad, no como la única
barrera (`H-27`, `H-28`).

### 9.3 Validar antes de escribir

Tres patrones se repiten: validar después de actualizar (`H-09`), validar al
emitir en vez de al capturar (`H-54`, `H-55`) y confiar en que el llamador envía
todos los campos (`H-15`, `H-63`). La regla debería ser: validar, transaccionar
y luego escribir; si falta un campo obligatorio, rechazar antes de tocar la
base.

### 9.4 La documentación también es un contrato

`H-01` demuestra que una afirmación de release puede sobrevivir a la
implementación. README, notas de versión y ayuda del CLI deben generarse o
verificarse contra el código. Cuando no se pueda, el registro de hallazgos debe
ser el correctivo inmediato.

### 9.5 Contención del legado

El legado no es solo código archivado: conserva datos reales en texto plano,
un servidor LAN y un instalador con el mismo `AppId`. Mientras siga en el
repositorio y en `dist/`, es un riesgo operativo (`H-29`, `H-41`, `H-42`,
`H-83`). La decisión de conservarlo como referencia es válida solo si se
separan los datos y se impide su ejecución accidental.

### 9.6 Pruebas proporcionales al riesgo

La suite actual cubre bien el CLI y los servicios, pero no la UI, el instalador,
la migración ni DPAPI en otra cuenta. Los hallazgos de mayor severidad
(`H-20`, `H-21`, `H-29`, `H-40`, `H-73`) son precisamente los que no tienen
prueba. La inversión en pruebas debe seguir a la severidad, no a la facilidad.

---

## 10. Priorización

### 10.1 P0 — antes del siguiente artefacto distribuible

| Orden | IDs | Motivo |
|---:|---|---|
| 1 | `H-01` | La capacidad sigue ausente aunque la documentación ya la advierte; falta decidir impresión/PDF o cerrar el alcance. |
| 2 | `H-41`, `H-42` | Datos reales en texto plano y exposición de red del legado. |
| 3 | `H-20`, `H-21` | Pérdida de la llave o creación de una llave falsa. |
| 4 | `H-29` | El instalador legado puede reemplazar la app nativa. |
| 5 | `H-06` | Nómina pagada con neto inflado. |
| 6 | `H-15` | Pérdida de datos por automatización parcial. |
| 7 | `H-65` | Configuración inválida que rompe facturación. |
| 8 | `H-27`, `H-28` | La autorización depende del llamador. |
| 9 | `H-32` | Respaldos legados presentados como restaurables. |
| 10 | `H-40` | Deuda que bloquea cualquier cambio futuro de esquema. |

### 10.2 P1 — siguiente iteración

`H-02`, `H-03`, `H-04`, `H-05`, `H-07`, `H-09`, `H-14`, `H-17`, `H-18`,
`H-22`, `H-25`, `H-26`, `H-30`, `H-31`, `H-33`, `H-34`, `H-35`, `H-45`,
`H-49`, `H-50`, `H-52`, `H-54`, `H-57`, `H-59`, `H-61`, `H-62`, `H-63`,
`H-72`, `H-73`, `H-74`, `H-76`, `H-80`.

Criterio: errores localizados con solución manual, inconsistencias entre
pantallas y brechas de prueba que ya tienen un caso identificado.

> `H-73` es **Alta**, pero se planifica en P1: no bloquea la operación actual y
> requiere una inversión de UI Automation que no cabe en un corte cosmético.

### 10.3 P2 — backlog

`H-08`, `H-10`, `H-11`, `H-12`, `H-13`, `H-16`, `H-19`, `H-23`, `H-24`,
`H-36`, `H-37`, `H-38`, `H-39`, `H-43`, `H-44`, `H-46`, `H-47`, `H-48`,
`H-51`, `H-53`, `H-55`, `H-56`, `H-58`, `H-60`, `H-64`, `H-66`, `H-67`,
`H-68`, `H-69`, `H-70`, `H-71`, `H-75`, `H-77`, `H-78`, `H-79`, `H-81`,
`H-82`, `H-83`.

Criterio: cosmético, documental, accesibilidad, empaquetado o límite aceptado.
`H-11`, `H-12`, `H-13` y `H-71` son informativos y no requieren código en 2.0.0.

---

## 11. Mapa de identificadores locales a `H-nn`

Las tablas siguientes permiten saltar de un documento de área al registro
consolidado sin perder trazabilidad. El sentido inverso está en la columna
“Fuentes” de las secciones 2 a 7.

### 11.1 Seguridad (`SEC`)

| Local | H | Tema |
|---|---|---|
| SEC-01 | H-27 | `CompanyService` no revalida permisos. |
| SEC-02 | H-28 | Lecturas de módulos sin permiso explícito. |
| SEC-03 | H-17 | Bloqueo de intentos solo en memoria. |
| SEC-04 | H-18 | Contraseña mínima sin complejidad. |
| SEC-05 | H-19 | Sin firma de código. |
| SEC-06 | H-20 | Llave sin respaldo ni rotación. |
| SEC-07 | H-21 | Arranque crea `clave.bin` si falta. |
| SEC-08 | H-22 | `--password` en línea de comandos. |
| SEC-09 | H-23 | Auditoría editable por el dueño. |
| SEC-10 | H-24 | `libro.log` sin rotación ni redacción. |
| SEC-11 | H-25 | Sin bloqueo por inactividad. |
| SEC-12 | H-25 | Sin MFA. |
| SEC-13 | H-25 | `sessions` sin uso. |
| SEC-14 | H-26 | Migración deja `.plano` temporal. |
| SEC-15 | H-09 | Edición de usuario con cambio parcial. |

### 11.2 Módulos y flujos (`FUN`)

| Local | H | Tema |
|---|---|---|
| FUN-01 | H-02 | “Cobrado en ventas” usa facturas, no pagos. |
| FUN-02 | H-03 | “Por cobrar” con definiciones distintas. |
| FUN-03 | H-04 | Cuenta “Otro” inválida. |
| FUN-04 | H-05 | Movimientos sin efecto en stock. |
| FUN-05 | H-06 | Recalcular nómina deja conceptos obsoletos. |
| FUN-06 | H-07 | Pago de nómina el último día del mes. |
| FUN-07 | H-08 | Baja de empleado sin `end_date`. |
| FUN-08 | H-09 | Edición de usuario con cambio parcial. |
| FUN-09 | H-01 | Impresión anunciada pero inexistente. |
| FUN-10 | H-10 | Factura de total cero atrapada. |
| FUN-11 | H-11 | Sin CxP/CxC formal ni aging. |
| FUN-12 | H-12 | Costo de ventas solo referencial. |
| FUN-13 | H-13 | Sin multiempresa. |
| FUN-14 | H-14 | Sin periodos fiscales bloqueados. |

### 11.3 Interfaz (`UI`)

| Local | H | Tema |
|---|---|---|
| UI-01 | H-44 | Mojibake en `TransactionDialog`. |
| UI-02 | H-45 | “Activar” fijo en Usuarios. |
| UI-03 | H-45 | “Desactivar” fijo en Inventario. |
| UI-04 | H-45 | “Baja” fijo en Empleados. |
| UI-05 | H-04 | Tipo de cuenta “Otro” inválido. |
| UI-06 | H-16 | Altas ignoran casillas de estado. |
| UI-07 | H-02 | Etiquetas de IVA imprecisas. |
| UI-08 | H-03 | “Por cobrar” con definiciones distintas. |
| UI-09 | H-46 | Botones del Panel siempre visibles. |
| UI-10 | H-47 | “Usuarios” para auditores. |
| UI-11 | H-48 | Sin filtro “Vencidas”; “Emitidas” mezcla estados. |
| UI-12 | H-49 | Límite silencioso de 200 filas. |
| UI-13 | H-50 | Saldo del pie parcial. |
| UI-14 | H-51 | CSV con estados crudos. |
| UI-15 | H-01 | Sin impresión en la app nativa. |
| UI-16 | H-52 | Sin accesibilidad declarada. |
| UI-17 | H-53 | Factura bloqueada sin clientes. |
| UI-18 | H-54 | Sin aviso de stock en factura. |
| UI-19 | H-55 | Pago sin validación previa de saldo. |
| UI-20 | H-56 | Nómina pagada eliminable desde la lista. |

### 11.4 CLI (`CLI`)

| Local | H | Tema |
|---|---|---|
| CLI-01 | H-57 | Opciones desconocidas en silencio. |
| CLI-02 | H-58 | Valor con `--` exige `--opt=valor`. |
| CLI-03 | H-58 | `--pretty`, `--no-color`, `--quiet` y `--all` sin efecto. |
| CLI-04 | H-59 | Alcance de `--data-dir` y credenciales. |
| CLI-05 | H-57 | Posiciónales extra ignorados. |
| CLI-06 | H-60 | `help <grupo> <acción>` sin ayuda específica. |
| CLI-07 | H-60 | `schema` descriptivo para comandos con flags. |
| CLI-08 | H-61 | `auth status` inicializa la base. |
| CLI-09 | H-61 | `auth logout` no impide re-login. |
| CLI-10 | H-17 | Bloqueo de login no persiste entre procesos. |
| CLI-11 | H-15 | `save` es reemplazo total. |
| CLI-12 | H-16 | `accounts save` fuerza `active=1` en alta. |
| CLI-13 | H-16 | `categories save` fuerza `archived=0` en alta. |
| CLI-14 | H-16 | `products save` ignora `initialStock` y `active`. |
| CLI-15 | H-16 | `contacts save` ignora `archived`. |
| CLI-16 | H-62 | `transactions save/delete` rechazan automáticos. |
| CLI-17 | H-62 | `invoices save/delete` solo aceptan borradores. |
| CLI-18 | H-62 | `invoices pay` no exige cuenta activa. |
| CLI-19 | H-63 | `payroll update-item` pone en 0 lo omitido. |
| CLI-20 | H-06 | `payroll recalc` deja conceptos obsoletos. |
| CLI-21 | H-64 | `users save` reactiva siempre. |
| CLI-22 | H-09 | `users save` actualiza perfil antes de validar. |
| CLI-23 | H-08 | `employees toggle` no escribe `end_date`. |
| CLI-24 | H-65 | `company update` no valida. |
| CLI-25 | H-66 | `backup list` solo ve la carpeta estándar. |
| CLI-26 | H-66 | `audit list` ignora `--offset`. |
| CLI-27 | H-03 | `dashboard` y `invoices overview` no coinciden. |
| CLI-28 | H-67 | `balances` excluye cuentas inactivas. |
| CLI-29 | H-67 | `low-stock` incluye agotados; el resumen no. |
| CLI-30 | H-68 | Ventanas de fecha por defecto inconsistentes. |
| CLI-31 | H-69 | Sin exportación CSV/PDF desde el CLI. |
| CLI-32 | H-01 | Sin impresión de facturas en 2.0.0. |
| CLI-33 | H-70 | `restore` no cierra la REPL. |
| CLI-34 | H-70 | `restore` exige `--yes` (guarda correcta). |
| CLI-35 | H-20 | Respaldos DPAPI no portables. |
| CLI-36 | H-70 | Concurrencia y `database is locked`. |
| CLI-37 | H-70 | Dos CLIs escribiendo la misma carpeta. |
| CLI-38 | H-71 | Solo Windows. |
| CLI-39 | H-71 | Solo español. |
| CLI-40 | H-71 | No es CFDI/PAC. |
| CLI-41 | H-71 | No hay API HTTP. |
| CLI-42 | H-71 | Sin sesión persistente ni tokens. |
| CLI-43 | H-40 | Sin migraciones incrementales. |

### 11.5 Instalador (`INS`)

| Local | H | Tema |
|---|---|---|
| INS-01 | H-19 | Setup sin firma digital. |
| INS-02 | H-36 | `libro.ico` ausente en el staging. |
| INS-03 | H-29 | `AppId` compartido con 1.0.0. |
| INS-04 | H-30 | Versión hardcodeada en tres lugares. |
| INS-05 | H-31 | `--self-test` siempre sale 0. |
| INS-06 | H-32 | Respaldos `.db` no migran ni restauran. |
| INS-07 | H-33 | `.plano` no se restaura si falla la verificación. |
| INS-08 | H-34 | Sin respaldo automático antes de migrar. |
| INS-09 | H-35 | Guardia del build solo rechaza `*.db`. |
| INS-10 | H-39 | Sin `AppMutex` ni parada de la app. |
| INS-11 | H-37 | Sin build ARM64. |
| INS-12 | H-40 | Sin migraciones incrementales. |
| INS-13 | H-38 | Lista de culturas manual. |
| INS-14 | H-38 | Log UTF-8 sin BOM. |

### 11.6 Pruebas (`TST`)

| Local | H | Tema |
|---|---|---|
| TST-01 | H-72 | Suite dependiente del orden. |
| TST-02 | H-72 | Sin `setUp`/`tearDown` por prueba. |
| TST-03 | H-73 | Sin pruebas de UI. |
| TST-04 | H-74 | Sin pruebas del instalador. |
| TST-05 | H-70 | Sin pruebas de concurrencia. |
| TST-06 | H-75 | Sin cobertura. |
| TST-07 | H-76 | Comandos sin prueba feliz. |
| TST-08 | H-77 | Aserción `contains="no"` ambigua. |
| TST-09 | H-78 | Evidencia implícita de `--help` sin base. |
| TST-10 | H-79 | Sin pruebas de `--stdin`, `--password-env` ni `--`. |
| TST-11 | H-61 | Sin prueba de `logout` con re-login. |
| TST-12 | H-80 | Sin migración de esquema real. |
| TST-13 | H-80 | Sin DPAPI en otra cuenta/equipo. |
| TST-14 | H-81 | Sin pruebas de rendimiento. |
| TST-15 | H-52 | Sin pruebas de accesibilidad. |
| TST-16 | H-79 | `pytest` no instalado. |
| TST-17 | H-79 | Suite usa la fecha actual. |
| TST-18 | H-82 | Sin prueba de bloqueo entre procesos. |

### 11.7 Legado (`LEG`)

| Local | H | Tema |
|---|---|---|
| LEG-01 | H-41 | Base y respaldos en texto plano. |
| LEG-02 | H-42 | Servidor HTTP sin TLS y opción LAN. |
| LEG-03 | H-29 | `AppId` compartido y desinstalación destructiva. |
| LEG-04 | H-42 | Cookie sin `Secure` por defecto. |
| LEG-05 | H-42 | Respaldos descargables por HTTP. |
| LEG-06 | H-32 | Restauración manual sin verificación. |
| LEG-07 | H-17 | Bloqueo de login solo en memoria. |
| LEG-08 | H-83 | Sin pruebas automatizadas. |
| LEG-09 | H-19 | Sin firma de código. |
| LEG-10 | H-43 | `--stop` depende de WMI. |
| LEG-11 | H-24 | Log del servidor sin rotación. |
| LEG-12 | H-40 | Sin migraciones de esquema. |
| LEG-13 | H-43 | Build exige `csc.exe` y Node. |
| LEG-14 | H-01 | Impresión dependiente del navegador. |

---

## 12. Verificación y límites

### 12.1 Cómo se verificó

- Lectura directa de `desktop/Libro.Desktop`, `agent-harness/Libro.Cli`,
  `installer/` y `src/` en el corte.
- Búsquedas de símbolos y cadenas: `PrintDialog`, `TrackStock`, `paid_at`,
  `AutomationProperties`, `--password`, `AppId`, `SignTool`, `user_version`.
- Medición de artefactos: 16 tablas, 265 archivos de staging, 50,513,464 bytes
  del instalador, SHA256 publicado.
- Ejecución de la suite: **11/11**, `Ran 11 tests in 32.251s` (el tiempo varía según el equipo).
- Inspección de `data/` y `backups/legacy-pre-native-20261023/`: firma
  `SQLite format 3` y datos reales, sin cifrado.
- Contraste con los documentos 04 a 11 y con `graphify-out/` como evidencia
  derivada, nunca como fuente de verdad.

### 12.2 Qué no cubre

- No es una auditoría fiscal, legal ni de cumplimiento.
- No se probó el instalador en una VM limpia ni en otra cuenta de Windows.
- No se ejecutó el legado; los hallazgos `LEG` se basan en lectura de código y
  artefactos.
- No se probó DPAPI en otro equipo ni la restauración de un respaldo en otra
  cuenta.
- No se midió cobertura ni rendimiento con bases grandes.
- No se incluyen contraseñas, hashes, llaves ni datos personales reales.

### 12.3 Cómo citar un hallazgo

Usa el identificador consolidado y, si hace falta contexto, el local:

> “La nómina puede pagar un neto inflado (`H-06`, antes `FUN-05` y `CLI-20`).”

Para un defecto de interfaz:

> “El botón de Usuarios muestra ‘Activar’ aunque el usuario esté activo
> (`H-45`, antes `UI-02`).”

---

## 13. Mantenimiento

1. **Si corriges un hallazgo**, no borres la fila: cambia el estado a
   **Corregido**, indica la versión y añade la prueba que lo demuestra.
2. **Si discovers uno nuevo**, asígnale el siguiente `H-nn`, regístralo en la
   sección temática y añade su fila al mapa local si tiene identificador de
   área.
3. **Si cambia una definición contable**, revisa `H-02`, `H-03`, `H-06`,
   `H-07`, `H-12` y `H-50`.
4. **Si cambia el instalador**, revisa `H-19`, `H-20`, `H-29`, `H-30`, `H-32`
   a `H-40`.
5. **Si cambia el CLI**, revisa `H-15`, `H-57` a `H-70`.
6. **Si se retira el legado**, cierra `H-41`, `H-42`, `H-83` y actualiza
   [11-legado-nextjs.md](11-legado-nextjs.md).
7. **Si se implementa impresión**, actualiza `H-01`, el ancla de
   [docs/README.md](README.md) y las notas de versión.

### 13.1 Estados permitidos

| Estado | Uso |
|---|---|
| **Abierto** | Verificado y sin corrección. |
| **Mitigado documentalmente** | El riesgo sigue, pero ya está advertido. |
| **Corregido** | Código y prueba en la versión indicada. |
| **Aceptado** | Límite consciente de alcance. |
| **Obsoleto** | El código o el escenario ya no existe; se conserva por historia. |

---

## 14. Enlaces

- Seguridad y permisos: [04-seguridad.md](04-seguridad.md).
- Reglas y fórmulas: [05-modulos-flujos.md](05-modulos-flujos.md).
- Interfaz: [06-mapa-ui.md](06-mapa-ui.md).
- CLI: [07-cli.md](07-cli.md).
- Instalador: [08-operacion-instalador.md](08-operacion-instalador.md).
- Pruebas: [09-pruebas.md](09-pruebas.md).
- Inventario: [10-inventario-archivos.md](10-inventario-archivos.md).
- Legado: [11-legado-nextjs.md](11-legado-nextjs.md).
- Decisiones: [14-decisiones.md](14-decisiones.md).
- Evidencia Graphify: [15-evidencia-graphify.md](15-evidencia-graphify.md).



