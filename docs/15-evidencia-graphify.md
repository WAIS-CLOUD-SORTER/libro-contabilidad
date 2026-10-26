# Libro · Evidencia Graphify

**Corte:** 25 de octubre de 2026  
**Snapshot de evidencia:** 2026-10-25 17:39:52 (los artefactos pueden haberse regenerado después)  
**Herramienta:** `graphify 0.9.76` (`%USERPROFILE%\.local\bin\graphify.exe`)  
**Artefacto:** `graphify-out/` (local, no versionado)  
**Regla:** el grafo es evidencia derivada; la verdad está en el código y en la documentación curada de `docs/`.

---

## 1. Qué es Graphify y por qué está aquí

Graphify recorre el repositorio, extrae símbolos y relaciones del código y de los
documentos, y genera:

- un grafo de conocimiento (`graph.json`);
- un reporte de arquitectura (`GRAPH_REPORT.md`);
- un wiki navegable por comunidades;
- HTML interactivos (`graph.html`, `ARQUITECTURA.html`, `ARBOL.html`);
- resultados de consultas (`query-*.txt`).

Se usa para **navegar y descubrir relaciones**, no para decidir qué está
vigente. Un nodo puede representar código actual, documentación curada o código
Next.js archivado. La distinción entre vigente y legado la hace `docs/`, no el
grafo.

### 1.1 Qué puedes preguntar

| Pregunta | Comando |
|---|---|
| ¿Cómo se conecta el cálculo de IVA con facturas y reportes? | `graphify query "IVA facturas reportes"` |
| ¿Qué camino une `InvoiceService` con `TaxService`? | `graphify path "InvoiceService" "TaxService"` |
| ¿Qué es `PayrollService.CalculateInto` y quién lo llama? | `graphify explain "PayrollService.CalculateInto"` |
| ¿Qué nodos son hubs arquitectónicos? | `graphify god-nodes --top 15` |
| ¿Qué se ve afectado si cambio `Database.Initialize`? | `graphify affected "Database.Initialize" --depth 2` |

Las consultas leen `graphify-out/graph.json` por defecto. Si el grafo está
desactualizado, la respuesta también lo estará.

---

## 2. Cómo se generó

Secuencia observada en los logs del corte:

```powershell
# 1. Actualizar el grafo de código
graphify update .

# 2. Refrescar comunidades y sidecar de análisis (si está stale)
graphify cluster-only .

# 3. Exportar el wiki
graphify export wiki --graph graphify-out/graph.json

# 4. Exportar los HTML
graphify export html --graph graphify-out/graph.json
graphify export callflow-html graphify-out/graph.json --output graphify-out/ARQUITECTURA.html
graphify tree --graph graphify-out/graph.json --output graphify-out/ARBOL.html --root .

# 5. Consultas de evidencia
graphify query "arquitectura general" --budget 1800
graphify query "CLI para agentes" --budget 1800
graphify query "contabilidad inventario reportes" --budget 1800
graphify query "seguridad respaldos usuarios" --budget 1600
```

`graphify tree` necesita `--root .` para resolver la jerarquía de archivos. Sin
ese parámetro, el árbol puede quedar vacío o mal enraizado.

### 2.1 Versión y entorno

| Dato | Valor |
|---|---|
| Versión | `graphify 0.9.76` |
| Ejecutable | `%USERPROFILE%\.local\bin\graphify.exe` |
| Sistema | Windows 11 IoT Enterprise LTSC 10.0.26100 |
| Python | 3.12.10 |
| .NET SDK | 8.0.426 |

---

## 3. Artefactos del corte

### 3.1 Núcleo

| Archivo | Bytes | Modificado | Uso | Frescura |
|---|---:|---|---|---|
| `graph.json` | 5,329,560 | 2026-10-25 17:39:52 | Grafo consultable. | **Snapshot**; puede haberse regenerado. |
| `GRAPH_REPORT.md` | 49,963 | 2026-10-25 17:39:52 | Reporte de corpus, nodos, aristas y comunidades. | **Snapshot**; puede haberse regenerado. |
| `graph.html` | 4,003,812 | 2026-10-25 17:39:52 | Visualización interactiva. | **Snapshot**; puede haberse regenerado. |
| `manifest.json` | 45,713 | 2026-10-25 17:39:52 | Archivos vistos y hashes de extracción. | **Snapshot**; puede haberse regenerado. |
| `.graphify_labels.json` | 7,717 | 2026-10-25 17:39:52 | Etiquetas de comunidades. | **Snapshot**; puede haberse regenerado. |
| `.graphify_analysis.json` | 125,961 | 2026-10-23 16:50:49 | Sidecar de cohesión y god nodes. | **Stale**; Graphify lo advierte. |

> El plugin `graphify-always` puede regenerar estos archivos después de una
> edición. Las cifras de esta tabla describen el snapshot del **2026-10-25
> 17:39:52**; antes de citarlas, comprueba la fecha y el tamaño actuales.

### 3.2 Wiki

| Dato | Valor |
|---|---|
| Archivos | 195 (194 artículos + `index.md`) |
| Tamaño total | 351,118 bytes |
| Generado | 2026-10-25 16:31:18 |
| Nodos que declara | 2,819 |
| Aristas que declara | 6,494 |
| Comunidades que declara | 184 |

El wiki se generó **antes** del último `graphify update`. Sus cifras no coinciden
con `GRAPH_REPORT.md` y no debe citarse como el estado actual del grafo.

### 3.3 HTML

| Archivo | Bytes | Modificado | Contenido |
|---|---:|---|---|
| `ARQUITECTURA.html` | 334,113 | 2026-10-25 16:31:23 | 17 secciones, 16 diagramas Mermaid, 15 tablas de llamadas. |
| `ARBOL.html` | 157,008 | 2026-10-25 16:31:28 | Árbol colapsable D3 v7. |
| `graph.html` | 4,003,812 | 2026-10-25 17:39:52 | Grafo interactivo completo. |

`ARQUITECTURA.html` y `ARBOL.html` se generaron a las 16:31, antes del último
update. Son útiles para una vista general, pero pueden omitir nodos o relaciones
añadidos después.

### 3.4 Consultas de evidencia

| Archivo | Bytes | Nodos encontrados | Nodos mostrados | Tema |
|---|---:|---:|---:|---|
| `query-arquitectura.txt` | 10,902 | 73 | 51 | Arranque, migración, flujos. |
| `query-cli.txt` | 10,824 | 233 | 54 | CLI, pruebas, `CliContext`. |
| `query-contabilidad.txt` | 12,054 | 257 | 65 | Inventario, reportes, impuestos, nómina. |
| `query-seguridad.txt` | 10,690 | 48 | 47 | Respaldos, usuarios, SQLCipher. |

Las cuatro consultas se ejecutaron a las 16:31 contra el `graph.json` de 2,819
nodos. Están **truncadas** por presupuesto de tokens: muestran entre 47 y 65
nodos de los 48 a 257 encontrados. No son una lista exhaustiva.

### 3.5 Snapshots y cache

| Directorio | Archivos | Bytes | Contenido |
|---|---:|---:|---|
| `graphify-out/2026-10-23/` | 5 | 3,932,889 | Snapshot anterior: `graph.json`, reporte, manifest, labels, analysis. |
| `graphify-out/2026-10-25/` | 5 | 5,555,902 | Snapshot del corte: `graph.json`, reporte, manifest, labels, analysis. |
| `graphify-out/cache/` | 561 | 13,949,119 | Cache de extracción; no es evidencia legible. |
| `graphify-out/` total | 785 | 34,099,785 | Incluye wiki, HTML, logs y cache. |

Los snapshots fechados permiten comparar dos cortes. No se deben editar a mano:
son evidencia histórica.

### 3.6 Logs

| Archivo | Bytes | Uso |
|---|---:|---|
| `autostart.log` | 200,407 | Arranque del plugin; puede ser ruidoso. |
| `update-docs.log` | 2,890 | Salida de `graphify update .`. |
| `export-wiki.log` | 1,472 | Salida de `export wiki`. |
| `export-tree.log` | 352 | Salida de `tree`. |
| `export-callflow.log` | 798 | Salida de `export callflow-html`. |

Los logs están en UTF-16LE y PowerShell 5.1 los muestra con caracteres
intercalados. No son fuente de verdad; sirven para reconstruir comandos y
advertencias.

---

## 4. Métricas y discrepancia entre artefactos

### 4.1 Reporte del snapshot

`GRAPH_REPORT.md` (17:39) declara:

| Métrica | Valor |
|---|---:|
| Archivos del corpus | 209 |
| Palabras | ~214,823 |
| Nodos | 4,045 |
| Aristas | 8,250 |
| Comunidades | 245 (207 mostradas, 38 finas omitidas) |
| Extracción | 93 % `EXTRACTED`, 7 % `INFERRED`, 0 % ambiguo |
| Aristas inferidas | 586, confianza promedio 0.93 |
| Archivos no clasificados | 57 (top: `.dll` 14, `.so` 14, `.cache` 6) |

### 4.2 Wiki y HTML

El wiki (16:31) declara 2,819 nodos, 6,494 aristas y 184 comunidades. Es el
mismo grafo que alimentó `ARQUITECTURA.html` y `ARBOL.html`.

### 4.3 Por qué no coinciden

1. El wiki y los HTML se exportaron a las 16:31.
2. El `graph.json` y el reporte se regeneraron a las 17:39, después de añadir
   documentación.
3. `.graphify_analysis.json` sigue siendo del 23 de octubre y registra 1,701
   nodos; Graphify lo marca como stale.
4. `manifest.json` lista 211 archivos, mientras el reporte cuenta 209; dos
   archivos pueden haber cambiado entre la extracción y el conteo.
5. El wiki tiene 194 artículos porque agrupa por comunidad, no por archivo.

**Regla práctica:** cita la métrica con su artefacto y su hora. No mezcles
"4,045 nodos" con "wiki de 194 artículos" como si fueran el mismo corte.

### 4.4 Qué métrica usar

| Pregunta | Artefacto recomendado |
|---|---|
| ¿Cuántos nodos y aristas declara el último reporte? | `GRAPH_REPORT.md` o `graph.json` (17:39). |
| ¿Qué comunidades y artículos existen? | `wiki/index.md` (16:31), con la salvedad de que está atrasado. |
| ¿Cómo se conecta un símbolo? | `graph.json` con `query`, `path` o `explain`. |
| ¿Cuál era el estado anterior? | `graphify-out/2026-10-23/`. |
| ¿Qué archivos se extrajeron? | `manifest.json` (211 entradas). |

---

## 5. Cómo usar el grafo sin equivocarse

### 5.1 Para explorar arquitectura

```powershell
graphify query "¿Cómo se calcula el IVA de una factura y cómo llega a impuestos?" --budget 2500
graphify path "InvoiceService.Emit" "TaxService.GetSummary"
graphify explain "DashboardService.GetDashboard"
```

El resultado es un subgrafo, no una respuesta redactada. Úsalo para encontrar
archivos y símbolos; luego lee el código.

### 5.2 Para evaluar impacto

```powershell
graphify affected "Database.Initialize" --depth 2
graphify affected "CompanyService.SaveCompany" --relation calls --depth 2
```

`affected` recorre las aristas en reversa. Si el grafo está incompleto, la lista
también lo estará. Contrasta con `grep` o con la lectura directa antes de
concluir que un cambio es seguro.

### 5.3 Para documentación

El grafo incluye nodos de `docs/` y de `src/` (legado). Un nodo llamado
`4. Definición de las 16 tablas` puede ser un documento curado; un nodo
`facturas/[id]/page.tsx` es legado. El nombre no siempre lo aclara. Verifica la
ruta antes de citar.

### 5.4 Para agentes

1. Empieza con `graphify query` para acotar el subgrafo.
2. Usa `graphify path` para entender una relación entre dos símbolos.
3. Usa `graphify explain` para un concepto.
4. Lee el archivo citado; no respondas solo con el grafo.
5. Si el grafo no tiene el nodo, usa `grep` y actualiza el grafo después.

---

## 6. Lo que el grafo no prueba

| Afirmación | Por qué el grafo no basta |
|---|---|
| “La app no imprime” | Requiere buscar APIs de impresión y leer el código (`H-01`). |
| “El rol `consulta` no puede leer nómina” | Requiere probar la matriz de permisos (`H-28`). |
| “El instalador incluye el icono” | Requiere listar el staging (`H-36`). |
| “La base está cifrada” | Requiere inspeccionar la cabecera y la clave (`H-20`). |
| “Las pruebas pasan 11/11” | Requiere ejecutar la suite. |
| “El legado está archivado” | Requiere leer `src/` y `dist/` (`H-41`, `H-42`). |

El grafo describe relaciones; no ejecuta, no mide y no distingue intención de
implementación. Es una herramienta de navegación, no un test.

---

## 7. Regeneración

### 7.1 Cuándo regenerar

- Después de añadir o mover archivos de código.
- Después de cambiar la documentación curada.
- Antes de una auditoría que cite nodos o comunidades.
- Cuando `graphify check-update .` indique cambios pendientes.

### 7.2 Secuencia recomendada

```powershell
# 1. Verificar si hay cambios pendientes
graphify check-update .

# 2. Actualizar código
graphify update .

# 3. Refrescar el sidecar si está stale
graphify cluster-only .

# 4. Regenerar reporte y visualizaciones
graphify export html --graph graphify-out/graph.json
graphify export callflow-html graphify-out/graph.json --output graphify-out/ARQUITECTURA.html
graphify tree --graph graphify-out/graph.json --output graphify-out/ARBOL.html --root .
graphify export wiki --graph graphify-out/graph.json
```

### 7.3 Verificación después de regenerar

1. `GRAPH_REPORT.md` y `graph.json` deben tener la misma hora o una diferencia
   de segundos.
2. `wiki/index.md` debe declarar un número de nodos cercano al del reporte.
3. `graphify check-update .` no debe reportar cambios pendientes.
4. Las consultas de evidencia deben repetirse si se van a citar.
5. No mezcles snapshots de fechas distintas en una misma tabla.

### 7.4 Si el grafo queda vacío o incompleto

- Confirma que estás en la raíz del repositorio.
- Revisa `.gitignore` y `.graphifyignore`; un patrón agresivo puede excluir
  `desktop/` o `docs/`.
- Ejecuta `graphify update . --force` solo si un refactor eliminó código y el
  grafo se niega a reducirse.
- Revisa `graphify-out/autostart.log` si el plugin no arrancó.
- Para documentación e imágenes, el update de código no basta; hace falta la
  extracción semántica del asistente (`/graphify --update`).

---

## 8. Relación con la documentación curada

| Necesidad | Documento |
|---|---|
| Entender el producto y el alcance | [01-vision-alcance.md](01-vision-alcance.md) |
| Entender capas y dependencias | [02-arquitectura.md](02-arquitectura.md) |
| Tablas, columnas y enums | [03-modelo-datos.md](03-modelo-datos.md) |
| Seguridad, DPAPI y permisos | [04-seguridad.md](04-seguridad.md) |
| Fórmulas y flujos | [05-modulos-flujos.md](05-modulos-flujos.md) |
| Pantallas y diálogos | [06-mapa-ui.md](06-mapa-ui.md) |
| Comandos y JSON | [07-cli.md](07-cli.md) |
| Instalador y runbook | [08-operacion-instalador.md](08-operacion-instalador.md) |
| Pruebas y evidencia | [09-pruebas.md](09-pruebas.md) |
| Inventario de archivos | [10-inventario-archivos.md](10-inventario-archivos.md) |
| Legado Next.js | [11-legado-nextjs.md](11-legado-nextjs.md) |
| Glosario | [12-glosario.md](12-glosario.md) |
| Hallazgos y riesgos | [13-hallazgos-riesgos.md](13-hallazgos-riesgos.md) |
| Decisiones | [14-decisiones.md](14-decisiones.md) |

**Precedencia:** código > documentación curada > README/notas de versión >
`graphify-out/` > legado. Si el grafo contradice el código, gana el código y el
grafo debe regenerarse.

---

## 9. Seguridad y privacidad del artefacto

- El grafo indexa código y documentación, no la base de datos ni los respaldos
  binarios.
- No contiene contraseñas, hashes ni la llave DPAPI.
- Puede contener rutas absolutas de la máquina de desarrollo y nombres de
  archivos; no lo publiques sin revisar.
- `graphify-out/` está en `.gitignore`; es un artefacto local.
- No ejecutes Graphify sobre `data/`, `backups/` o copias con datos reales. Esos
  directorios contienen SQLite y no deben entrar al corpus.
- Si compartes `ARQUITECTURA.html` o `graph.html`, revisa que no incluyan rutas
  o nombres de usuarios reales.

---

## 10. Mantenimiento de este documento

Actualiza esta página cuando:

| Cambio | Acción |
|---|---|
| Nueva versión de Graphify | Actualizar la tabla de versión y probar los comandos. |
| Nuevo `graphify update` | Actualizar métricas, horas y snapshots. |
| Cambio de wiki o HTML | Revisar la sección 3 y la discrepancia de la sección 4. |
| Nuevo hallazgo sobre el grafo | Añadirlo a `docs/13-hallazgos-riesgos.md` si afecta decisiones. |
| Retiro del legado | Regenerar y verificar que `src/` ya no aparezca. |

### 10.1 Checklist de corte

- [ ] `graph.json` y `GRAPH_REPORT.md` tienen la misma fecha.
- [ ] El wiki se regeneró después del último update.
- [ ] `.graphify_analysis.json` no está stale.
- [ ] Las consultas citadas se repitieron contra el grafo actual.
- [ ] No se mezclan métricas de snapshots distintos.
- [ ] `docs/` sigue siendo la fuente principal y el grafo solo la evidencia.
