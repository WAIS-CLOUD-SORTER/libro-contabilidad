# Libro · Documentación técnica

Documentación curada de **Libro Contabilidad 2.0.0**: la aplicación nativa de
Windows, su base cifrada, el instalador, el harness CLI para agentes y el código
heredado que se conserva como referencia.

> **Corte documental:** 25 de octubre de 2026.
> **Aplicación:** 2.0.0 · **CLI:** 1.0.0 · **Esquema de base:** `user_version = 1`.
> **Instalador:** `dist\Libro-Setup-2.0.0.exe` (50,513,464 bytes).
> **SHA256:** `A7B78EE1B22405E696A5B7ADD764DD2E9C5E5F5C04641ACA5E10D790AAFDB525`.

---

## 1. Qué es Libro

Libro es una aplicación de escritorio **nativa para Windows** para llevar la
contabilidad interna de un negocio en México:

- **C# / WPF sobre .NET 8**, sin Node.js, navegador, servidor HTTP ni WebView en
  la aplicación vigente.
- Base **SQLCipher** cifrada, llave maestra aleatoria protegida con **DPAPI** del
  usuario de Windows.
- Interfaz en **español**, importes en **MXN** con **IVA**, fechas en
  `yyyy-MM-dd` y operación local sin nube.
- Facturas **internas**: controlan folio, conceptos, pagos y saldo. **No son
  CFDI**, no se timbran y no se envían al SAT.
- Un **CLI agent-native** (`agent-harness\Libro.Cli`) que referencia los mismos
  servicios C# de la aplicación; no reimplementa contabilidad.

## 2. Qué no es Libro

- No es un sistema de facturación electrónica ni un PAC.
- No es multiusuario en red ni multitenant: cada cuenta de Windows tiene su
  carpeta y su llave.
- No es una aplicación web, móvil o en la nube.
- No sustituye al contador: el ISR y el IMSS son **cálculos de referencia
  editables**, no declaraciones fiscales.
- No incluye impresión de facturas en la versión nativa 2.0.0. El README y las
  notas de versión la anunciaban antes de la corrección documental del
  2026-10-25; ahora advierten la limitación. Véase
  [13-hallazgos-riesgos.md](13-hallazgos-riesgos.md#h-01--impresión-de-facturas-anunciada-sin-implementación).

## 3. Estado de los documentos y del código

| Etiqueta | Significado |
| --- | --- |
| **Vigente** | Comportamiento implementado en `desktop/` o `agent-harness/` y verificado contra el código el 2026-10-25. |
| **Archivado** | Código Next.js de `src/` conservado como referencia histórica. No se ejecuta ni forma parte del producto 2.0.0. |
| **Generado** | Artefacto de Graphify en `graphify-out/`. Es evidencia derivada, no fuente de verdad. |
| **Hallazgo** | Diferencia verificada entre lo anunciado y lo implementado, o riesgo técnico. |
| **Prueba** | Evidencia reproducible de `agent-harness/tests` o `SelfTest.cs`. |

La **fuente de verdad** es, en este orden:

1. el código de `desktop/` y `agent-harness/`;
2. los scripts de `installer/`;
3. esta documentación;
4. `README.md` y `RELEASE-NOTES-2.0.0.md` (se corrigieron las afirmaciones
   detectadas; si queda alguna desactualizada, se registra como hallazgo);
5. `graphify-out/` y la documentación de `src/` (archivada).

## 4. Índice

| Documento | Contenido | Lectores |
| --- | --- | --- |
| [01-vision-alcance.md](01-vision-alcance.md) | Problema, alcance, invariantes, roles y no objetivos. | Producto, negocio, QA |
| [02-arquitectura.md](02-arquitectura.md) | Capas, proyectos, dependencias, arranque, estado y patrones de datos. | Desarrollo |
| [03-modelo-datos.md](03-modelo-datos.md) | Las 16 tablas, columnas, enums, índices, relaciones y datos semilla. | Desarrollo, datos |
| [04-seguridad.md](04-seguridad.md) | SQLCipher, DPAPI, scrypt, sesiones, permisos, auditoría y respaldos. | Seguridad, auditoría |
| [05-modulos-flujos.md](05-modulos-flujos.md) | Reglas y fórmulas de cada módulo, máquinas de estado y efectos cruzados. | Desarrollo, QA, contabilidad |
| [06-mapa-ui.md](06-mapa-ui.md) | Ventanas, navegación, pantallas, diálogos, acciones y exports CSV. | UX, QA, soporte |
| [07-cli.md](07-cli.md) | Contrato JSON, opciones globales, catálogo completo y REPL. | Agentes, integradores |
| [08-operacion-instalador.md](08-operacion-instalador.md) | Instalación, actualización, desinstalación, respaldos y runbook. | Operaciones, soporte |
| [09-pruebas.md](09-pruebas.md) | Pruebas E2E, self-test, aislamiento de datos y resultados. | QA, desarrollo |
| [10-inventario-archivos.md](10-inventario-archivos.md) | Inventario exhaustivo de archivos y su función. | Desarrollo, mantenimiento |
| [11-legado-nextjs.md](11-legado-nextjs.md) | Aplicación Next.js archivada, rutas y diferencias con la nativa. | Mantenimiento, auditoría |
| [12-glosario.md](12-glosario.md) | Términos contables, fiscales y técnicos. | Todos |
| [13-hallazgos-riesgos.md](13-hallazgos-riesgos.md) | Hallazgos verificados, severidad y acción recomendada. | Producto, seguridad |
| [14-decisiones.md](14-decisiones.md) | Decisiones de arquitectura y producto con su razón. | Desarrollo, producto |
| [15-evidencia-graphify.md](15-evidencia-graphify.md) | Qué generó Graphify y cómo usarlo sin confundirlo con código vigente. | Desarrollo, agentes |

## 5. Rutas de lectura recomendadas

**Desarrollador nuevo**

1. [01-vision-alcance.md](01-vision-alcance.md)
2. [02-arquitectura.md](02-arquitectura.md)
3. [03-modelo-datos.md](03-modelo-datos.md)
4. [05-modulos-flujos.md](05-modulos-flujos.md)
5. [10-inventario-archivos.md](10-inventario-archivos.md)

**Soporte y operaciones**

1. [08-operacion-instalador.md](08-operacion-instalador.md)
2. [04-seguridad.md](04-seguridad.md) (respaldos y DPAPI)
3. [06-mapa-ui.md](06-mapa-ui.md)
4. [13-hallazgos-riesgos.md](13-hallazgos-riesgos.md)

**Agente o integrador**

1. [07-cli.md](07-cli.md)
2. [04-seguridad.md](04-seguridad.md) (credenciales y datos)
3. [05-modulos-flujos.md](05-modulos-flujos.md) (reglas de negocio)

**Auditoría técnica**

1. [02-arquitectura.md](02-arquitectura.md)
2. [03-modelo-datos.md](03-modelo-datos.md)
3. [04-seguridad.md](04-seguridad.md)
4. [13-hallazgos-riesgos.md](13-hallazgos-riesgos.md)
5. [15-evidencia-graphify.md](15-evidencia-graphify.md)

## 6. Convenciones de esta documentación

### Importes

Todos los importes monetarios se guardan y se intercambian como **enteros en
centavos** (`long` en C#, `INTEGER` en SQLite). `10000` significa `$100.00`.
No se usa `double` para dinero. Las tasas (`iva_rate`, `imss_rate`) sí son
`REAL` en el rango `0..1`; `0.16` es 16 %.

### Fechas y horas

- Fechas de negocio: texto `yyyy-MM-dd`.
- Marcas de tiempo nuevas de C#: `yyyy-MM-ddTHH:mm:ss.fffZ` (UTC).
- Marcas de tiempo por defecto de SQLite: `yyyy-MM-dd HH:mm:ss` (UTC).
- La zona de visualización es México (`Central Standard Time (Mexico)` o
  `America/Mexico_City`, con reserva a la zona local).
- La mezcla de formatos de timestamp es deliberada/heredada; la UI y los
  filtros la toleran. Véase
  [03-modelo-datos.md](03-modelo-datos.md#11-fechas-horas-y-zona-horaria).

### Permisos

Se nombran con la forma `recurso.acción` (`tx.write`, `backup.manage`, …). La
matriz completa está en [04-seguridad.md](04-seguridad.md#72-matriz-completa).

### Rutas

Se usan rutas relativas a la raíz del repositorio, salvo las rutas de runtime,
que se indican con `%LOCALAPPDATA%`. En código, las rutas absolutas de esta
máquina de desarrollo (`C:\Proyectos\IDK`) solo aparecen como contexto.

### Referencias al código

Cada afirmación relevante cita el archivo y el símbolo, por ejemplo:
`desktop/Libro.Desktop/Services/InvoiceService.cs → EmitInternal`. En hallazgos
puntuales se añade el número de línea del corte para localizar la evidencia; si
el archivo cambia, prevalece el símbolo y la línea puede quedar desfasada.

## 7. Verificación de esta documentación

Comandos usados para contrastar el contenido:

```powershell
# Compilar aplicación y CLI, y ejecutar pruebas E2E
powershell -ExecutionPolicy Bypass -File agent-harness\build.ps1 -Test

# Alternativa con el wrapper
cmd /c agent-harness\run-tests.cmd

# Verificar el instalador publicado
Get-FileHash dist\Libro-Setup-2.0.0.exe -Algorithm SHA256
(Get-Item dist\Libro-Setup-2.0.0.exe).Length

# Ver el estado de la base y la llave sin abrir datos reales
agent-harness\libro-cli.cmd --json doctor
```

Resultados registrados:

- Compilación: **0 advertencias, 0 errores**.
- Pruebas E2E: **11/11**, `Ran 11 tests in 32.251s` (el tiempo varía según el equipo).
- Instalador: SHA256 y tamaño coinciden con
  `dist\Libro-Setup-2.0.0.exe.sha256`.
- `doctor` no crea `clave.bin` cuando falta; lo reporta.

## 8. Mantenimiento

Actualiza la documentación cuando cambie cualquiera de estos contratos:

| Cambio | Documentos a revisar |
| --- | --- |
| Tabla, columna, índice o enum | 03, 05, 10 |
| Permiso, rol o regla de autorización | 04, 06, 07 |
| Fórmula contable, IVA, ISR o nómina | 05, 12, 13 |
| Comando u opción del CLI | 07, 09 |
| Instalador, ruta o tarea | 08, 10 |
| Prueba nueva o cambio de resultado | 09 |
| Hallazgo corregido o nuevo | 13 |

Para refrescar la evidencia derivada:

```powershell
graphify update .
graphify tree --root .
```

`graphify-out/` está en `.gitignore`; es un artefacto local, no una fuente
versionada. La documentación curada de `docs/` sí es la entrega principal.

## 9. Límites de esta documentación

- No es asesoría fiscal, legal ni contable. Las tasas de ISR/IMSS y los
  regímenes fiscales deben confirmarse con un profesional.
- Describe el código y los artefactos presentes el 2026-10-25; no garantiza
  compatibilidad con versiones futuras.
- No reproduce secretos, contraseñas reales, hashes de contraseña ni la llave
  de cifrado.
- Las credenciales de prueba y las de modo desarrollo son sintéticas y solo
  aplican a bases temporales o de demo.
