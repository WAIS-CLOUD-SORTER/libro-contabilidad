# Libro · Decisiones de arquitectura y producto

**Corte:** 25 de octubre de 2026  
**Alcance:** app nativa 2.0.0, CLI 1.0.0, instalador 2.0.0 y legado archivado.  
**Formato:** decisiones estilo ADR ligero. Cada una registra contexto, decisión,
razón, consecuencias y condición para revisarla.

---

## 1. Cómo usar este documento

Este documento explica **por qué** el sistema es como es. No sustituye al
código ni al registro de hallazgos:

- Si buscas **qué hace** el sistema, lee [05-modulos-flujos.md](05-modulos-flujos.md).
- Si buscas **qué está mal o pendiente**, lee
  [13-hallazgos-riesgos.md](13-hallazgos-riesgos.md).
- Si buscas **por qué se eligió** una tecnología o un límite, estás aquí.

Cada decisión tiene un identificador `D-nn`, un estado y una condición de
revisión. Los estados son:

| Estado | Significado |
|---|---|
| **Vigente** | Se aplica en 2.0.0. |
| **Vigente con deuda** | Se aplica, pero tiene una consecuencia conocida registrada como `H-nn`. |
| **Aceptada** | Límite consciente de alcance. |
| **Revisable** | Se tomó para 2.0.0, pero se espera cambiar. |
| **Histórica** | Aplica solo al legado o a una versión anterior. |

---

## 2. Índice de decisiones

| ID | Decisión | Estado | Consecuencia principal |
|---|---|---|---|
| D-01 | Aplicación nativa WPF sobre .NET 8, sin runtime web. | Vigente | Sin Node, navegador, HTTP ni WebView en la app vigente. |
| D-02 | Un solo backend de dominio; el CLI referencia el proyecto WPF. | Vigente | No se duplican reglas contables. |
| D-03 | Servicios estáticos, `AppState` y Dapper sobre SQLite. | Vigente | Código directo y auditable; menos abstracción que un ORM. |
| D-04 | SQLCipher con clave aleatoria protegida por DPAPI del usuario. | Vigente con deuda | Cifrado fuerte, pero la llave no es portable ni rotable (`H-20`). |
| D-05 | Dinero en centavos enteros; tasas en `REAL` de `0..1`. | Vigente | Sin errores de coma flotante en importes. |
| D-06 | Fechas de negocio `yyyy-MM-dd`; marcas de tiempo ISO/UTC. | Vigente | Comparaciones de texto simples; cuidado con formatos mixtos (`H-07`). |
| D-07 | Una empresa por base (`company.id=1`). | Aceptada | Sin multiempresa (`H-13`); una carpeta por negocio. |
| D-08 | Facturas internas, no CFDI; sin PAC ni SAT. | Aceptada | No hay timbrado, XML ni cancelación fiscal. |
| D-09 | Nómina con ISR/IMSS estimados, no cálculo fiscal definitivo. | Aceptada | Los importes son de trabajo, no un acuse fiscal. |
| D-10 | Permisos por rol en UI y CLI; servicios como segunda barrera. | Vigente con deuda | La autorización depende del llamador (`H-27`, `H-28`). |
| D-11 | Auditoría dentro de la misma base cifrada. | Aceptada | No es prueba forense frente al dueño (`H-23`). |
| D-12 | Respaldos `.libro` cifrados y restauración con `--yes`. | Vigente | Restauración segura, pero dependiente de DPAPI (`H-20`). |
| D-13 | Sin migraciones incrementales en 2.0.0. | Vigente con deuda | `user_version` se escribe, no se lee (`H-40`). |
| D-14 | CLI en C# que referencia `Libro.Desktop.csproj`. | Vigente | Un solo binario de dominio; acoplamiento al proyecto WPF. |
| D-15 | Opciones globales antes del comando; `--new-password` en la acción de usuarios. | Vigente | Evita colisión con `--password` de login (`H-59`). |
| D-16 | JSON de éxito a stdout; errores JSON a stderr con salida distinta de cero. | Vigente | Contrato estable para agentes. |
| D-17 | Los comandos `save` reemplazan la entidad completa. | Revisable | Puede borrar campos omitidos (`H-15`). |
| D-18 | REPL para agentes y modo one-shot para scripts. | Vigente | La sesión vive en memoria; no hay tokens. |
| D-19 | Pruebas E2E con `unittest` y directorios temporales. | Vigente con deuda | Nunca tocan datos reales; suite dependiente del orden (`H-72`). |
| D-20 | Documentación curada en `docs/`; Graphify como evidencia derivada. | Vigente | La verdad está en el código y en los documentos, no en el grafo. |
| D-21 | Interfaz y mensajes solo en español. | Aceptada | Sin i18n ni recursos multiidioma. |
| D-22 | Instalación por usuario en `%LOCALAPPDATA%\Programs\Libro`. | Vigente | Sin privilegios de administrador; datos por usuario. |
| D-23 | El artefacto 2.0.0 está congelado; no se recompila por hallazgos cosméticos. | Vigente | Los hallazgos se documentan para la siguiente build. |
| D-24 | El legado se conserva como referencia, no como producto operable. | Aceptada | `src/` y 1.0.0 siguen disponibles; riesgo de ejecución accidental (`H-41`, `H-42`). |
| D-25 | Sin firma de código en 2.0.0. | Vigente con deuda | SmartScreen advierte (`H-19`). |
| D-26 | Sin build ARM64 en 2.0.0. | Aceptada | ARM64 usa emulación (`H-37`). |
| D-27 | Sin API HTTP, nube ni sincronización. | Aceptada | Integración por proceso, JSON o REPL. |
| D-28 | Sin MFA ni bloqueo por inactividad. | Aceptada | La seguridad depende del equipo y la contraseña (`H-25`). |

---

## 3. Arquitectura

### D-01 · Aplicación nativa WPF sobre .NET 8

**Contexto.** La versión 1.0.0 era una aplicación Next.js que requería Node,
navegador y un servidor HTTP local. Eso complicaba la instalación, la seguridad
y el soporte en equipos de oficina con Windows.

**Decisión.** Reescribir la aplicación como escritorio nativo en C# / WPF sobre
.NET 8, publicada self-contained para `win-x64`.

**Razón.**

- Un solo runtime y un solo instalador; no se requiere Node ni navegador.
- La base, la UI y los respaldos quedan en el mismo proceso local.
- WPF permite una interfaz de escritorio densa, con DataGrid, teclado y
  diálogos modales, sin servidor HTTP.
- .NET 8 tiene soporte LTS y herramientas de publicación self-contained.

**Consecuencias.**

- Solo Windows; no hay versión web ni macOS/Linux (`H-71`).
- La UI se prueba con herramientas de Windows, no con un navegador (`H-73`).
- El código de impresión del legado no se reutiliza; la app nativa no imprime
  (`H-01`).

**Revisar cuando.** Se necesite acceso remoto, varios usuarios concurrentes o
una versión web. En ese escenario, la respuesta no es añadir un navegador
embebido, sino un diseño explícito de servidor y autenticación.

### D-02 · Un solo backend de dominio

**Contexto.** El CLI necesita ejecutar las mismas reglas contables que la UI:
IVA, folios, pagos, nómina, inventario y permisos. Duplicarlas en el CLI
garantizaría divergencias.

**Decisión.** El proyecto `Libro.Cli` referencia `Libro.Desktop.csproj` y llama
directamente a `AppState`, `AuthService` y los servicios de dominio. No hay una
capa HTTP intermedia ni una segunda implementación.

**Razón.**

- Una sola fuente de verdad para reglas y validaciones.
- Los cambios de dominio se reflejan en UI y CLI al mismo tiempo.
- Menos superficie: no hay servidor, puerto ni token que asegurar.

**Consecuencias.**

- El CLI arrastra dependencias de WPF y se compila para
  `net8.0-windows` (`H-71`).
- Un cambio incompatible en un servicio afecta a ambos consumidores.
- La autorización debe estar en el servicio, no solo en cada interfaz
  (`H-27`, `H-28`).

**Revisar cuando.** Aparezca un consumidor que no pueda ejecutar .NET en
Windows. En ese caso, se justificaría un contrato de procesos o un servicio
local, no copiar la lógica.

### D-03 · Servicios estáticos, `AppState` y Dapper

**Contexto.** La aplicación es de un solo usuario y un solo proceso; no necesita
un contenedor de dependencias ni un ORM completo.

**Decisión.** Usar clases de servicio estáticas, un estado global de aplicación
(`AppState`) con la base, el usuario y la empresa actuales, y Dapper para SQL
explícito sobre SQLite/SQLCipher.

**Razón.**

- El SQL queda visible y auditable; no hay consultas generadas difíciles de
  predecir.
- Menos ceremonia para una app pequeña y local.
- `AppState` simplifica el acceso desde code-behind y desde el CLI.

**Consecuencias.**

- El estado global dificulta pruebas paralelas y aislamiento por prueba
  (`H-72`).
- Las consultas se repiten en varios servicios; de ahí las definiciones
  distintas de “por cobrar” (`H-03`).
- No hay migraciones automáticas de esquema (`H-40`).

**Revisar cuando.** Se necesiten pruebas concurrentes, varios usuarios o una
segunda empresa. Ahí conviene inyectar dependencias y separar el estado.

### D-14 · CLI en C# que referencia el proyecto WPF

**Contexto.** Los agentes necesitan automatizar la app sin una API HTTP. El
proyecto ya tiene un backend de dominio en C#.

**Decisión.** Crear una consola `net8.0-windows` que referencia el proyecto de
escritorio y expone comandos, JSON y REPL.

**Razón.**

- Reutiliza el 100 % de las reglas y permisos.
- Se ejecuta en el mismo equipo y carpeta de datos.
- No abre puertos ni requiere un servicio residente.

**Consecuencias.**

- El CLI no corre en Linux/macOS.
- Comparte el estado estático y el bloqueo de SQLite con la app; no se deben
  ejecutar escrituras concurrentes (`H-70`).
- El contrato de comandos debe mantenerse sincronizado con los servicios.

**Revisar cuando.** Se requiera integración desde otra máquina o desde un
lenguaje sin .NET. La respuesta sería un protocolo local explícito, no un
servidor HTTP abierto.

---

## 4. Datos y cifrado

### D-04 · SQLCipher con clave aleatoria y DPAPI

**Contexto.** La base contiene ingresos, gastos, nómina, datos de clientes y
configuración fiscal. El legado la guardaba en texto plano.

**Decisión.** Cifrar la base con SQLCipher. Generar una clave maestra aleatoria,
guardarla en `clave.bin` protegida con DPAPI `CurrentUser` y derivar la clave de
la base con scrypt.

**Razón.**

- Un archivo `.db` copiado no se puede leer sin la cuenta de Windows.
- DPAPI evita almacenar la clave en texto plano.
- scrypt encarece la derivación y protege contra ataques de diccionario si la
  clave se filtra.

**Consecuencias.**

- La base y los respaldos quedan atados a la cuenta y al equipo (`H-20`).
- Si `clave.bin` falta, la app no debe crear una nueva (`H-21`).
- No hay rotación de llave ni recuperación con frase secreta.

**Revisar cuando.** Se necesite mover datos entre equipos, compartir respaldos
o cumplir una política de rotación. La solución debe incluir exportación e
importación de la clave, no solo un botón de “cambiar contraseña”.

### D-05 · Dinero en centavos enteros

**Contexto.** Los importes se muestran en pesos con dos decimales, pero se
calculan IVA, subtotales, nómina e inventario.

**Decisión.** Guardar e intercambiar importes como `long` en centavos. Las tasas
(`iva_rate`, `imss_rate`) son `REAL` en `0..1`. El redondeo usa
`MidpointRounding.AwayFromZero`.

**Razón.**

- Evita errores de coma flotante en sumas y comparaciones.
- Hace explícito el redondeo en cada operación.
- El contrato JSON del CLI es inequívoco: `10000` son $100.00.

**Consecuencias.**

- Todo consumidor debe respetar la unidad; un agente que envíe pesos puede
  producir importes 100 veces mayores.
- Las tasas sí son flotantes; se redondea el resultado, no la tasa.

**Revisar cuando.** Se integre un sistema externo que use decimales. La
conversión debe estar en el borde, no en el dominio.

### D-06 · Fechas de negocio y marcas de tiempo

**Contexto.** La contabilidad trabaja por día; la auditoría y los pagos necesitan
hora.

**Decisión.** Fechas de negocio como texto `yyyy-MM-dd`. Marcas de tiempo nuevas
desde C# en ISO `yyyy-MM-ddTHH:mm:ss.fffZ` (UTC); las de SQLite en
`yyyy-MM-dd HH:mm:ss`. La visualización usa la zona de México.

**Razón.**

- El texto ISO se ordena y compara correctamente.
- La UI y el CLI comparten el mismo formato.
- La zona de visualización es explícita.

**Consecuencias.**

- Mezclar ambos formatos en una comparación de texto produce errores
  (`H-07`: `T` es mayor que un espacio).
- No hay tipo `date` nativo en SQLite; la validación es de aplicación.

**Revisar cuando.** Se necesiten zonas horarias por usuario o fechas con hora
en reglas de negocio. Ahí conviene almacenar UTC y convertir en el borde.

### D-07 · Una empresa por base

**Contexto.** Libro nació para un negocio local. El esquema usa `company.id=1` y
la UI muestra una sola empresa.

**Decisión.** Mantener una empresa por base. Para llevar dos negocios, se usan
dos carpetas de datos y dos accesos directos.

**Razón.**

- Simplifica folios, configuración, reportes y respaldos.
- Evita una dimensión `company_id` en cada tabla y el riesgo de mezclar datos.
- El respaldo de una empresa es un archivo autocontenido.

**Consecuencias.**

- No hay consolidación ni cambio de empresa en la misma sesión (`H-13`).
- El usuario debe recordar qué carpeta corresponde a cada negocio.

**Revisar cuando.** Un usuario necesite consolidar varios negocios o compartir
catálogos. Sería un cambio de modelo mayor, no un simple filtro.

### D-08 · Facturas internas, no CFDI

**Contexto.** El usuario necesita controlar folios, conceptos, pagos y saldo,
pero no se busca timbrar ante el SAT.

**Decisión.** Las facturas son documentos internos. Se marcan explícitamente
como **“No es CFDI”**. No hay XML, PAC, timbre, cancelación fiscal ni envío al
SAT.

**Razón.**

- El alcance es contabilidad interna, no facturación fiscal.
- Integrar CFDI exigiría PAC, certificados, XML 4.0 y cumplimiento continuo.
- Un documento interno mal etiquetado como CFDI sería un riesgo legal.

**Consecuencias.**

- No se puede usar como comprobante fiscal (`H-71`).
- La impresión, si se implementa, debe conservar la leyenda “No es CFDI”
  (`H-01`).
- La numeración es interna y no sustituye un folio fiscal.

**Revisar cuando.** El producto quiera facturar fiscalmente. Sería un proyecto
aparte con proveedor PAC y certificación.

### D-09 · Nómina estimada, no cálculo fiscal definitivo

**Contexto.** La nómina debe apoyar el control interno de sueldos, ISR e IMSS,
pero el producto no incluye CFDI de nómina, subsidio al empleo ni actualización
normativa continua.

**Decisión.** Calcular una nómina de referencia con tarifa mensual de ISR
configurable (sin subsidio al empleo) y una tasa simplificada de IMSS. Los
conceptos se guardan editables y el pago genera un gasto; no se timbra ni se
presenta ante el SAT.

**Razón.**

- Permite planear el costo de la plantilla sin un módulo fiscal completo.
- Evita prometer una precisión que requeriría tarifas, cuotas y CFDI oficiales.
- Mantiene el alcance local y verificable de la app.

**Consecuencias.**

- Los importes pueden diferir de los cálculos oficiales del SAT o del IMSS.
- Las tasas deben revisarse y actualizarse manualmente.
- La integridad de la nómina sigue dependiendo de `H-06` y `H-08`.

**Revisar cuando.** Se requiera nómina fiscal. Sería un proyecto separado con
CFDI de nómina, timbrado y actualización normativa.

### D-13 · Sin migraciones incrementales en 2.0.0

**Contexto.** La 2.0.0 es la primera versión nativa. El esquema se crea con
`CREATE TABLE IF NOT EXISTS` y `user_version` se escribe.

**Decisión.** No introducir un motor de migraciones en 2.0.0; documentar la
deuda y implementarlo antes del primer cambio de esquema.

**Razón.**

- La base nativa nace con el esquema completo; no hay versiones intermedias que
  migrar.
- Añadir migraciones sin necesidad inmediata retrasa el corte.

**Consecuencias.**

- Cualquier 2.x con columnas nuevas romperá bases existentes (`H-40`).
- El `user_version` actual no sirve como entrada porque nadie lo lee.

**Revisar cuando.** Se planifique el primer cambio de esquema. La condición es
bloqueante: no se publica una 2.x con cambios de esquema sin migraciones.

---

## 5. Seguridad y permisos

### D-10 · Permisos en UI/CLI; servicios como segunda barrera

**Contexto.** Hay 14 permisos y cuatro roles. La UI oculta acciones y el CLI
valida antes de llamar a los servicios.

**Decisión.** Mantener los gates en la UI y el CLI, y añadir validación en los
servicios cuando sea posible.

**Razón.**

- La UI necesita saber qué mostrar.
- El CLI necesita devolver errores claros.
- El servicio es la última barrera si un nuevo llamador olvida el gate.

**Consecuencias.**

- Hoy la segunda barrera es incompleta (`H-27`, `H-28`).
- La matriz de roles promete más separación de la que el servicio garantiza.

**Revisar cuando.** Se añada un nuevo consumidor (otro CLI, una integración o
una prueba). La regla debe ser: todo servicio público valida permiso.

### D-11 · Auditoría en la misma base

**Contexto.** Se necesita rastrear altas, cambios, pagos y accesos.

**Decisión.** Guardar `audit_log` en la misma base SQLCipher, con usuario,
acción, entidad y detalle.

**Razón.**

- Es simple y consistente con la transacción que audita.
- El respaldo incluye la auditoría.
- No requiere un servicio externo.

**Consecuencias.**

- El dueño de la base puede editarla; no es prueba forense (`H-23`).
- No hay firma en cadena ni exportación inmutable.

**Revisar cuando.** Se requiera cumplimiento o evidencia legal. Habría que
exportar a un destino externo con firma o sellado.

### D-12 · Respaldos cifrados y restauración con `--yes`

**Contexto.** Un respaldo debe poder restaurarse sin ambigüedad y sin destruir
la base actual por accidente.

**Decisión.** Los respaldos son archivos `.libro` cifrados. `backup restore`
exige `--yes`; antes de reemplazar, crea un respaldo de seguridad.

**Razón.**

- El respaldo hereda la protección de SQLCipher.
- `--yes` evita restauraciones accidentales en scripts.
- El respaldo de seguridad permite deshacer.

**Consecuencias.**

- Los respaldos no son portables sin la clave DPAPI (`H-20`).
- La restauración no reinicia la REPL (`H-70`).
- Los `.db` legados no se restauran (`H-32`).

**Revisar cuando.** Se implemente recuperación con frase secreta o se admitan
respaldos legados.

### D-28 · Sin MFA ni bloqueo por inactividad

**Contexto.** La app es local, de un solo usuario por sesión de Windows.

**Decisión.** No implementar MFA ni bloqueo por inactividad en 2.0.0. Sí se
mantiene el límite de intentos y el hash scrypt.

**Razón.**

- El perímetro principal es la cuenta de Windows y el disco.
- MFA exigiría un segundo factor y un proceso de recuperación.
- El bloqueo por inactividad puede implementarse después sin cambiar el modelo.

**Consecuencias.**

- Un equipo desatendido queda con la sesión abierta (`H-25`).
- Una contraseña robada es suficiente (`H-18`).

**Revisar cuando.** Se use en equipos compartidos o se manejen datos de
terceros con requisitos de cumplimiento.

---

## 6. CLI y automatización

### D-15 · Opciones globales antes del comando

**Contexto.** El parser necesita distinguir credenciales globales de opciones
de la operación. `--password` sirve para login; crear o restablecer un usuario
necesita otra opción.

**Decisión.** Las opciones globales (`--data-dir`, `--email`, `--password`,
`--json`, `--debug`) van antes del comando. Las contraseñas nuevas se pasan con
`--new-password` en la acción que las requiere (`auth change-password`,
`users save` o `users reset-password`), no como opción global.

**Razón.**

- Evita colisiones entre el login y la creación de usuarios.
- Hace explícito el contexto de autenticación.
- El `--data-dir` se resuelve antes de abrir la base.

**Consecuencias.**

- `--data-dir` después del comando funciona por `FirstText`, pero no es la
  convención (`H-59`).
- Las credenciales después del comando no sirven para auto-login.

**Revisar cuando.** Se rediseñe el parser. La regla debe seguir siendo
inequívoca: global antes, operación después.

### D-16 · Contrato JSON para agentes

**Contexto.** Los agentes necesitan leer resultados y errores sin parsear texto
humano.

**Decisión.** Con `--json`, el éxito va a stdout como JSON y los errores a
stderr como JSON, con código de salida distinto de cero. Sin `--json`, la salida
es tablas o texto legible.

**Razón.**

- Permite pipelines y reintentos sin ambigüedad.
- Separa el flujo de datos del flujo de diagnóstico.
- Los códigos de salida son estables.

**Consecuencias.**

- Un error de validación no debe escribir JSON parcial en stdout.
- Los comandos deben mantener nombres y tipos estables.
- La ayuda y los esquemas deben reflejar el contrato real (`H-60`).

**Revisar cuando.** Se añada un nuevo tipo de salida (CSV, PDF). Debe ser
explícito y no romper el contrato JSON.

### D-17 · `save` como reemplazo total

**Contexto.** Los servicios de dominio reciben entidades completas y actualizan
todas las columnas. El CLI necesitaba una forma simple de crear y editar.

**Decisión.** Los comandos `save` construyen la entidad completa desde flags o
`--input` y llaman al servicio; lo omitido toma el default.

**Razón.**

- Reutiliza los servicios sin una capa de parches por campo.
- El objeto completo es explícito y validable.
- Es simple de documentar.

**Consecuencias.**

- Un payload parcial puede borrar campos (`H-15`).
- `payroll update-item` pone en cero lo omitido (`H-63`).
- Los agentes deben leer primero (`show`) y enviar todo.

**Revisar cuando.** Se implemente actualización parcial o un `--dry-run`. Es
una de las decisiones más urgentes de revisar.

### D-18 · REPL y modo one-shot

**Contexto.** Un agente puede necesitar varias operaciones en una sesión, o una
sola operación desde un script.

**Decisión.** Soportar ambos: `libro-cli` sin comando abre una REPL; con comando
ejecuta una operación y termina. La sesión de la REPL vive en memoria.

**Razón.**

- La REPL evita autenticarse en cada paso.
- El one-shot es predecible para scripts.
- No hay tokens ni archivos de sesión que proteger.

**Consecuencias.**

- `restore` no reinicia la REPL (`H-70`).
- `logout` no impide un auto-login si hay credenciales globales (`H-61`).
- La REPL tokeniza rutas de Windows con cuidado, pero no es un shell completo.

**Revisar cuando.** Se necesite una sesión persistente entre procesos. Sería un
token explícito, con expiración y revocación.

### D-27 · Sin API HTTP, nube ni sincronización

**Contexto.** La app es local y puede automatizarse por proceso. Existe la
tentación de exponer una API HTTP o sincronizar con la nube para integrarla.

**Decisión.** No incluir API HTTP, servidor residente, nube ni sincronización en
2.0.0. La integración es por el CLI, JSON y la REPL en el mismo equipo.

**Razón.**

- No añade puertos, autenticación de red, TLS ni superficie remota.
- Mantiene los datos y la llave en el equipo del usuario.
- El CLI ya reutiliza el backend sin duplicar reglas.

**Consecuencias.**

- No hay acceso desde otra máquina ni desde un lenguaje sin .NET.
- El CLI está acoplado a Windows y al proyecto WPF (`H-71`).
- La concurrencia entre procesos queda limitada por SQLite (`H-70`).

**Revisar cuando.** Se requiera integración remota. La respuesta debe ser un
protocolo explícito con autenticación y autorización, no un HTTP abierto.

---

## 7. Pruebas y documentación

### D-19 · `unittest` con directorios temporales

**Contexto.** La suite E2E necesita ejecutar el CLI real contra una base real,
sin tocar los datos del usuario. `pytest` no está disponible en el entorno.

**Decisión.** Usar `unittest` de Python, crear una carpeta temporal por corrida
y pasar `--data-dir`. Las pruebas son compatibles con `pytest`, pero se ejecutan
con `python -m unittest`.

**Razón.**

- Cero dependencias adicionales.
- Aislamiento real: cada corrida usa una base nueva.
- Los comandos se prueban como los usaría un agente.

**Consecuencias.**

- La suite comparte estado entre pruebas y depende del orden (`H-72`).
- No hay `setUp`/`tearDown` por prueba (`TST-02`).
- `pytest` no se usa, aunque los nombres de test sean compatibles (`H-79`).

**Revisar cuando.** Se añada CI o se necesiten pruebas paralelas. Ahí conviene
una fixture por prueba y una base por caso.

### D-20 · Documentación curada y Graphify como evidencia

**Contexto.** Graphify genera un grafo, un wiki y HTML a partir del código. Es
útil para navegar, pero puede quedar desactualizado y no distingue lo vigente de
lo archivado.

**Decisión.** La documentación curada vive en `docs/` y es la entrega principal.
`graphify-out/` es evidencia derivada, se regenera y no se versiona.

**Razón.**

- La documentación curada explica decisiones, riesgos y contexto que el grafo
  no puede inferir.
- El grafo cambia con cada edición y no es estable como contrato.
- Separar ambas evita que un artefacto viejo se cite como verdad.

**Consecuencias.**

- Hay que mantener `docs/` a mano.
- El grafo puede estar sucio o desactualizado; se advierte en
  [15-evidencia-graphify.md](15-evidencia-graphify.md).
- Los identificadores `H-nn` y locales deben mantenerse estables.

**Revisar cuando.** Se adopte otra herramienta de documentación. La regla se
mantiene: lo curado manda, lo generado ilustra.

### D-21 · Solo español

**Contexto.** El usuario, la contabilidad y los documentos son mexicanos.

**Decisión.** Interfaz, mensajes, documentación y ayuda del CLI en español. No
hay recursos multiidioma.

**Razón.**

- Evita traducciones incompletas y términos fiscales ambiguos.
- Reduce el alcance de la UI.
- El usuario objetivo trabaja en español.

**Consecuencias.**

- No hay i18n (`H-71`).
- Los mensajes de error del framework pueden aparecer en inglés.

**Revisar cuando.** Se despliegue en otro país o idioma. Sería un cambio de
recursos, no de lógica.

---

## 8. Instalación y distribución

### D-22 · Instalación por usuario

**Contexto.** La app debe instalarse sin privilegios de administrador y guardar
datos por usuario de Windows.

**Decisión.** Inno Setup instala en
`%LOCALAPPDATA%\Programs\Libro`; los datos viven en
`%LOCALAPPDATA%\Libro\data`; la llave en la misma carpeta.

**Razón.**

- No requiere UAC ni una cuenta administrativa.
- Cada usuario de Windows tiene su propia base y su propia llave DPAPI.
- La desinstalación no debe borrar los datos.

**Consecuencias.**

- No hay instalación “para todos los usuarios”.
- Los respaldos y la llave quedan atados a esa cuenta.
- El instalador 1.0.0 comparte `AppId` y puede interferir (`H-29`).

**Revisar cuando.** Se necesite una instalación corporativa por máquina. Habría
que separar datos por usuario y definir una política de migración.

### D-23 · Artefacto 2.0.0 congelado

**Contexto.** El instalador 2.0.0 ya se publicó y tiene SHA256 registrado. Los
hallazgos detectados después del corte incluyen cosméticos, de empaquetado y de
documentación.

**Decisión.** No recompilar el artefacto 2.0.0 por hallazgos que no bloquean la
operación. Documentarlos para la siguiente build. La excepción son los riesgos
altos de seguridad o pérdida de datos, que se corrigen en una versión nueva.

**Razón.**

- Reemplazar un artefacto publicado rompe la trazabilidad del SHA256.
- Una recompilación cosmética obliga a repetir pruebas de instalación.
- El usuario necesita una referencia estable.

**Consecuencias.**

- `H-36` (icono) y `H-38` (BOM) quedan para la próxima build.
- `H-01` se corrige en documentación, no en el binario.
- Cualquier corrección de código exige una versión nueva, no un reemplazo
  silencioso.

**Revisar cuando.** Aparezca un riesgo alto que justifique una 2.0.1. En ese
caso, se genera un artefacto nuevo con su propio SHA256 y notas.

### D-25 · Sin firma de código en 2.0.0

**Contexto.** Firmar requiere un certificado de código y un proceso de
timestamp. No se dispone de uno en el corte.

**Decisión.** Publicar sin firma, documentar la limitación y verificar el
SHA256 por un canal separado.

**Razón.**

- No se puede simular una firma.
- El SHA256 permite verificar integridad, aunque no autenticidad.

**Consecuencias.**

- SmartScreen advierte (`H-19`).
- Un instalador alterado no se detecta criptográficamente por el usuario.
- La verificación depende de un canal confiable para el hash.

**Revisar cuando.** Se obtenga un certificado. La firma debe cubrir setup y
ejecutable, con timestamp.

### D-26 · Sin build ARM64

**Contexto.** El equipo de desarrollo y los usuarios objetivo son x64.

**Decisión.** Publicar solo `win-x64`; permitir ejecución en ARM64 bajo
emulación.

**Razón.**

- Un solo artefacto y una sola matriz de pruebas.
- La emulación es suficiente para una app de oficina.

**Consecuencias.**

- En ARM64 el consumo y el arranque son mayores (`H-37`).
- No se prueba nativamente en esa arquitectura.

**Revisar cuando.** Aparezcan usuarios ARM64 o se despliegue en equipos
Snapdragon. Publicar `win-arm64` y probar el instalador.

---

## 9. Legado

### D-24 · Legado como referencia, no como producto

**Contexto.** `src/` contiene la aplicación Next.js 1.0.0; `dist/` conserva su
instalador. Sirve para entender la migración y comparar funciones.

**Decisión.** Conservar el código y el artefacto como referencia histórica. No
operarlo, no distribuirlo con datos y no incluir sus bases en el instalador
nativo.

**Razón.**

- Permite rastrear de dónde vienen rutas, fórmulas y decisiones.
- Evita perder la historia de la migración.
- El usuario ya tiene la app nativa.

**Consecuencias.**

- El repositorio conserva datos reales en texto plano si no se limpian
  (`H-41`).
- El instalador 1.0.0 comparte `AppId` (`H-29`).
- El servidor LAN y la descarga de respaldos siguen siendo riesgos si se
  ejecutan (`H-42`).

**Revisar cuando.** Se decida retirar el legado. La condición es migrar los
datos, borrar las copias planas y archivar el artefacto fuera del repositorio.

---

## 10. Alternativas descartadas

| Alternativa | Por qué no se eligió | Qué la haría viable |
|---|---|---|
| Seguir con Next.js/Electron | Requiere Node, navegador y servidor; difícil de instalar y asegurar en oficina. | Un requisito real de acceso web o multiplataforma. |
| API HTTP local | Añade puerto, autenticación, CORS y superficie de ataque. | Integración desde otra máquina o lenguaje. |
| ORM completo (EF Core) | Oculta SQL y complica consultas de reporte; la app es pequeña. | Un modelo mucho más grande o varias bases. |
| Guardar dinero como `double` | Errores de redondeo en sumas e IVA. | Nunca para dinero. |
| Facturación CFDI/PAC | Requiere certificados, XML, timbre y cumplimiento continuo. | Un producto fiscal separado. |
| Multiempresa en una base | Añade `company_id` a todo y riesgo de mezclar datos. | Consolidación multiempresa real. |
| Nube/sincronización | Contradice la operación local y la privacidad del usuario. | Un requisito explícito de colaboración. |
| Imprimir con navegador embebido | Reintroduce WebView y dependencia web. | Nunca; si se imprime, nativo o PDF. |
| MFA en 2.0.0 | Complejidad de recuperación para una app local. | Equipos compartidos o requisito de cumplimiento. |
| Migraciones automáticas en 2.0.0 | No hay versiones intermedias que migrar. | El primer cambio de esquema de una 2.x. |

---

## 11. Deuda derivada de las decisiones

| Decisión | Deuda | Hallazgo |
|---|---|---|
| D-04 | Llave no portable ni rotable. | `H-20` |
| D-04 | Arranque crea llave nueva si falta. | `H-21` |
| D-06 | Formatos de fecha mixtos. | `H-07` |
| D-10 | Servicios sin segunda barrera completa. | `H-27`, `H-28` |
| D-13 | Sin migraciones incrementales. | `H-40` |
| D-17 | `save` reemplaza todo. | `H-15`, `H-63` |
| D-19 | Suite dependiente del orden. | `H-72` |
| D-22 | `AppId` compartido con el legado. | `H-29` |
| D-23 | Artefacto con icono ausente y BOM. | `H-36`, `H-38` |
| D-24 | Datos legados en texto plano y red expuesta. | `H-41`, `H-42` |
| D-25 | Sin firma de código. | `H-19` |
| D-28 | Sin bloqueo por inactividad ni MFA. | `H-25` |

---

## 12. Cómo añadir una decisión

1. Asigna el siguiente `D-nn`.
2. Escribe **Contexto**, **Decisión**, **Razón**, **Consecuencias** y
   **Revisar cuando**.
3. Añádela al índice con estado y consecuencia principal.
4. Si la decisión genera un riesgo, regístralo en
   [13-hallazgos-riesgos.md](13-hallazgos-riesgos.md) y enlaza el `H-nn`.
5. Si reemplaza una decisión anterior, no la borres: marca la anterior como
   **Histórica** y explica el cambio.

### 12.1 Plantilla

```markdown
### D-nn · Título breve

**Contexto.** Qué problema o restricción existía.

**Decisión.** Qué se decidió, en una frase verificable.

**Razón.** Por qué esa opción y no otra.

**Consecuencias.** Qué se gana, qué se pierde y qué deuda queda.

**Revisar cuando.** Condición observable que obliga a revisar la decisión.
```

---

## 13. Enlaces

- Visión y alcance: [01-vision-alcance.md](01-vision-alcance.md).
- Arquitectura: [02-arquitectura.md](02-arquitectura.md).
- Modelo de datos: [03-modelo-datos.md](03-modelo-datos.md).
- Seguridad: [04-seguridad.md](04-seguridad.md).
- Módulos y flujos: [05-modulos-flujos.md](05-modulos-flujos.md).
- CLI: [07-cli.md](07-cli.md).
- Instalador: [08-operacion-instalador.md](08-operacion-instalador.md).
- Pruebas: [09-pruebas.md](09-pruebas.md).
- Hallazgos: [13-hallazgos-riesgos.md](13-hallazgos-riesgos.md).
- Evidencia Graphify: [15-evidencia-graphify.md](15-evidencia-graphify.md).
