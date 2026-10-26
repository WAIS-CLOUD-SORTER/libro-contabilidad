# Libro 2.0.0 — Notas de la versión

**Fecha:** 23 de octubre de 2026  
**Instalador:** `dist\Libro-Setup-2.0.0.exe`  
**Tamaño:** 50,513,464 bytes  
**SHA256:** `A7B78EE1B22405E696A5B7ADD764DD2E9C5E5F5C04641ACA5E10D790AAFDB525`

## Resumen

Libro 2.0.0 es una reescritura completa de la aplicación como **escritorio nativo de Windows** en C# / WPF sobre .NET 8. Ya no requiere Node.js, navegador, servidor HTTP ni WebView. La base de datos, la interfaz y los respaldos son locales.

La aplicación anterior basada en Next.js se conserva en `src/` y `dist\Libro-Setup-1.0.0.exe` únicamente como referencia histórica.

> **Corrección posterior al corte:** estas notas anunciaban impresión de facturas,
> pero la app nativa 2.0.0 no incluye impresión ni generación de PDF. La
> limitación está registrada en
> [`docs/13-hallazgos-riesgos.md`](docs/13-hallazgos-riesgos.md#h-01--impresión-de-facturas-anunciada-sin-implementación).

## Funcionalidad incluida

- Panel con ingresos, gastos, utilidad, cobranza, caja, inventario y alertas.
- Movimientos de ingresos y gastos con IVA, categorías, contactos y cuentas.
- Facturas internas con borradores, folio, emisión, pagos, cancelación y control de saldo. **No es CFDI**.
- Contactos de clientes y proveedores con RFC y saldo por cobrar.
- Inventario con costo promedio, entradas, salidas, ajustes y compras ligadas a un gasto.
- Empleados y nómina mensual/quincenal con ISR/IMSS estimados y pago ligado a un gasto.
- Impuestos: IVA trasladado, acreditable y neto por mes, resumen anual y exportación CSV.
- Reportes: estado de resultados, flujo de efectivo, gastos por categoría, antigüedad de saldos e inventario.
- Usuarios con roles, bitácora de actividad, configuración de empresa, cuentas, categorías y respaldos.
- Interfaz completa en español, pensada para México (MXN e IVA).

## Migración y compatibilidad de datos

- La ruta de datos se mantiene en `%LOCALAPPDATA%\Libro\data`.
- Al primer arranque, una base SQLite plana de la versión anterior se migra automáticamente a SQLCipher.
- Se conservan empresa, usuarios, hashes `scrypt`, roles, cuentas, categorías, movimientos, facturas, inventario, empleados y nómina.
- El esquema y el formato de dinero en centavos se mantienen.
- La migración verifica la integridad antes de eliminar la copia plana.
- En la máquina de desarrollo se conservó una copia plana de seguridad en `backups\legacy-pre-native-20261023`. Esa carpeta contiene datos reales sin cifrar: protégela y elimínala cuando ya no sea necesaria.

## Cifrado y respaldos

- Base SQLCipher cifrada en reposo.
- Llave aleatoria de 32 bytes en `clave.bin`, protegida con **Windows DPAPI `CurrentUser`**.
- Sin contraseña maestra adicional.
- Respaldos `.libro` cifrados con la misma llave.
- Verificación de respaldos antes de restaurar; un respaldo corrupto se rechaza y la base actual queda intacta.
- Restauración desde la propia aplicación, con respaldo de seguridad automático.

**Limitación importante:** la llave DPAPI está ligada a la cuenta de Windows y al perfil del equipo. Los respaldos están pensados para restaurarse con la misma cuenta de Windows. Si se pierde el perfil o se cambia de equipo, la llave podría no desprotegerse. Conserva `clave.bin` y una copia segura de la carpeta de datos.

## Instalador

- Instalación **por usuario**, sin permisos de administrador.
- Ruta: `%LOCALAPPDATA%\Programs\Libro`.
- Incluye el runtime de .NET 8; la PC destino no necesita .NET instalado.
- Actualiza una instalación anterior basada en Node/Next.js en el mismo directorio:
  - detiene el servidor anterior;
  - elimina `.next`, `node_modules`, `node.exe`, `server.js` y archivos relacionados;
  - conserva `%LOCALAPPDATA%\Libro\data`;
  - elimina accesos directos obsoletos como **Detener Libro**.
- Crea accesos directos en el menú Inicio y, opcionalmente, en el escritorio.
- El desinstalador elimina solo la aplicación y los accesos directos; **conserva los datos**.
- `UninstallLogMode=overwrite` evita heredar las acciones del desinstalador anterior (incluida la llamada `Libro.exe --stop --silent`, que bloqueaba la desinstalación con la aplicación nativa).

## Verificación realizada

- Compilación nativa con 0 errores y 0 advertencias.
- Autoprueba interna `SELFTEST_OK`:
  - vector `scrypt` y rechazo de contraseña incorrecta;
  - base cifrada y rechazo de llave incorrecta;
  - migración de base plana conservando datos;
  - respaldo cifrado;
  - rechazo de respaldo corrupto sin dañar la base;
  - restauración de respaldo válido y cifrado resultante.
- Migración de la base real de la máquina de desarrollo:
  - empresa **RyB** y usuario administrador conservados;
  - base resultante cifrada (cabecera no SQLite);
  - conteos de tablas sin cambios.
- Ciclo completo de instalación:
  - instalación silenciosa sobre la versión anterior;
  - datos y `clave.bin` con el mismo hash antes/después;
  - archivos legacy eliminados;
  - aplicación instalada y arrancada desde `%LOCALAPPDATA%\Programs\Libro`.
- Ciclo completo de desinstalación:
  - desinstalador termina sin bloquearse;
  - aplicación y accesos directos eliminados;
  - `contabilidad.db` y `clave.bin` intactos.
- Reinstalación final y arranque correcto.

## Limitaciones conocidas

- Facturas internas, sin timbrado CFDI ni PAC.
- Sin impresión de facturas ni generación de PDF.
- Cálculos fiscales y de nómina estimados; no sustituyen al contador ni al SAT.
- Sin acceso por red local ni interfaz web.
- Instalador sin firma digital; SmartScreen puede advertir.
- Respaldos ligados a la cuenta de Windows por DPAPI.
- La interfaz nativa es solo para Windows de 64 bits.

## Actualizar desde 1.0.0

1. Cierra Libro.
2. Ejecuta `Libro-Setup-2.0.0.exe`.
3. Acepta la ruta de instalación existente.
4. Abre Libro y verifica empresa, usuarios y datos.
5. Mantén una copia de `%LOCALAPPDATA%\Libro\data` antes de cambios importantes.

No ejecutes el instalador 1.0.0 después de actualizar a 2.0.0: comparte el identificador de aplicación y podría reemplazar archivos de la versión nativa.
