# Libro · Contabilidad

Aplicación de escritorio **nativa para Windows** para la contabilidad y administración local de un negocio en México. Está escrita en **C# / WPF sobre .NET 8**, guarda todo en una base **SQLCipher cifrada** y no necesita Node.js, navegador, servidor HTTP ni servicios en la nube.

> Versión actual: **2.0.0** · Instalador: `dist\Libro-Setup-2.0.0.exe`  
> Documentación completa: [`docs/README.md`](docs/README.md)

## Módulos

- **Panel**: ingresos, gastos, utilidad, cobranza, caja, inventario y alertas del mes.
- **Movimientos**: ingresos no facturados y gastos, con IVA, categorías, contacto y cuenta.
- **Facturas**: borradores, emisión con folio, pagos, cancelación y control de saldo. Documento interno, **no es CFDI**.
- **Contactos**: clientes y proveedores con RFC, datos de contacto y saldo por cobrar.
- **Inventario**: productos, stock, costo promedio, entradas/salidas/ajustes y compras con gasto automático.
- **Empleados y nómina**: plantilla, periodos mensuales o quincenales, ISR/IMSS estimados, pago con gasto automático.
- **Impuestos**: IVA trasladado, acreditable y neto por mes, resumen anual y exportación CSV.
- **Reportes**: estado de resultados, flujo de efectivo, gastos por categoría, antigüedad de saldos e inventario.
- **Usuarios y configuración**: roles, cuentas, categorías, tarifa de ISR, respaldos y bitácora de actividad.

## Instalación en Windows

El artefacto listo para distribuir es:

`dist\Libro-Setup-2.0.0.exe` (~50.5 MB)

SHA256:

```text
A7B78EE1B22405E696A5B7ADD764DD2E9C5E5F5C04641ACA5E10D790AAFDB525  Libro-Setup-2.0.0.exe
```

Es una instalación **por usuario**, no requiere permisos de administrador:

| Ruta | Contenido |
| --- | --- |
| `%LOCALAPPDATA%\Programs\Libro` | Aplicación nativa y runtime de .NET incluido. |
| `%LOCALAPPDATA%\Libro\data` | Base cifrada, llave DPAPI, respaldos y bitácora. |
| Menú Inicio → Libro | Abrir la aplicación. |
| Menú Inicio → Carpeta de datos | Abrir la carpeta de datos. |
| Menú Inicio → Desinstalar Libro | Desinstalar la aplicación. |

### Instalar o actualizar

1. Cierra Libro si está abierto.
2. Ejecuta `Libro-Setup-2.0.0.exe`.
3. Si Windows SmartScreen muestra una advertencia (el instalador no está firmado digitalmente), elige **Más información → Ejecutar de todas formas**.
4. Elige si quieres acceso directo en el escritorio. La opción de iniciar con Windows está desactivada por defecto.
5. Al terminar, abre **Libro** desde el escritorio o el menú Inicio.

Si vienes de la versión anterior basada en Node/Next.js, el instalador:

- detiene el servidor anterior;
- elimina los archivos de la aplicación vieja (`.next`, `node_modules`, `node.exe`, `server.js`);
- instala la aplicación nativa en la misma carpeta;
- **no toca** `%LOCALAPPDATA%\Libro\data`.

No es necesario desinstalar la versión anterior primero.

### Desinstalar

Usa **Configuración de Windows → Aplicaciones → Libro → Desinstalar** o **Desinstalar Libro** en el menú Inicio.

El desinstalador elimina únicamente la aplicación y los accesos directos. **Conserva los datos, la llave y los respaldos** en `%LOCALAPPDATA%\Libro\data`, de modo que puedes volver a instalar sin perder información.

Si realmente quieres borrar todo, elimina manualmente la carpeta `%LOCALAPPDATA%\Libro\data` después de desinstalar.

## Primer arranque y migración

En una instalación nueva, Libro muestra un asistente para crear el negocio y la cuenta de administrador.

Si ya existe `%LOCALAPPDATA%\Libro\data\contabilidad.db`:

- Si la base es la versión anterior en texto plano, se migra automáticamente a SQLCipher al primer arranque. Se conservan empresa, usuarios, contraseñas `scrypt`, roles y datos.
- Se crea `clave.bin`, una llave aleatoria de 32 bytes protegida con **Windows DPAPI** (`CurrentUser`). No hay contraseña maestra adicional.
- La copia plana original se elimina al terminar la migración. Antes de migrar se creó una copia de seguridad externa en `backups\legacy-pre-native-20261023` en la máquina de desarrollo; esa copia está en texto plano y debe protegerse o eliminarse cuando ya no sea necesaria.

## Datos y cifrado

| Ruta | Contenido |
| --- | --- |
| `%LOCALAPPDATA%\Libro\data\contabilidad.db` | Base SQLCipher cifrada (WAL). |
| `%LOCALAPPDATA%\Libro\data\clave.bin` | Llave aleatoria protegida con DPAPI. **No la borres.** |
| `%LOCALAPPDATA%\Libro\data\respaldos\*.libro` | Respaldos cifrados con la misma llave. |
| `%LOCALAPPDATA%\Libro\data\libro.log` | Bitácora técnica local. |

La base y los respaldos no contienen texto SQLite reconocible. La llave se desprotege con la cuenta de Windows actual; no se escribe en texto plano.

> **Importante:** al estar protegida con DPAPI `CurrentUser`, la llave está ligada a la cuenta de Windows y al perfil del equipo. Los respaldos `.libro` están pensados para restaurarse con la misma cuenta de Windows. Si se pierde el perfil de Windows o se cambia de equipo, es posible que la llave ya no se pueda desproteger. Conserva una copia segura de la carpeta de datos y no borres `clave.bin`.

### Respaldar

En **Configuración → Respaldos** pulsa **Crear respaldo ahora**. Se genera una copia consistente y cifrada de la base. Guarda copias fuera de la computadora (USB, disco externo o nube).

### Restaurar

1. Abre **Configuración → Respaldos**.
2. Elige el archivo `.libro` y confirma.
3. Libro verifica que el respaldo sea válido y cifrado antes de reemplazar la base.
4. Si el respaldo está corrupto, se rechaza y la base actual queda intacta.
5. Al restaurar correctamente, la aplicación se cierra para que vuelvas a abrirla con los datos restaurados.

## Roles

| Rol | Alcance |
| --- | --- |
| Administrador | Todo: usuarios, configuración, respaldos, operación y reportes. |
| Contador | Operación contable, facturas, nómina, impuestos, reportes y bitácora. |
| Capturista | Movimientos, facturas, contactos, productos e inventario. |
| Consulta | Solo lectura de reportes, impuestos y operación. |

Las contraseñas usan `scrypt` con el mismo formato de la versión anterior. La bitácora registra las acciones importantes.

## Compilar desde el código

Requisitos en la PC de compilación:

- Windows 10/11 x64.
- .NET SDK 8.
- Inno Setup 6 (`ISCC.exe`).

Compilar la aplicación:

```powershell
cd desktop\Libro.Desktop
dotnet build -c Debug
```

Compilar el instalador completo (publica self-contained, ejecuta la autoprueba y calcula SHA256):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File installer\build-native.ps1
```

Salidas:

- `installer\native-app\` (aplicación publicada; no se distribuye por separado)
- `dist\Libro-Setup-2.0.0.exe`
- `dist\Libro-Setup-2.0.0.exe.sha256`

Verificar integridad:

```powershell
Get-FileHash .\dist\Libro-Setup-2.0.0.exe -Algorithm SHA256
Get-Content .\dist\Libro-Setup-2.0.0.exe.sha256
```

La autoprueba interna cubre:

- vector de `scrypt` y rechazo de contraseña incorrecta;
- creación de base cifrada y rechazo de llave incorrecta;
- migración de una base plana conservando datos;
- respaldo cifrado;
- rechazo de respaldo corrupto sin dañar la base;
- restauración de respaldo válido y cifrado resultante.

Se puede ejecutar manualmente:

```powershell
& "$env:LOCALAPPDATA\Programs\Libro\Libro.exe" --self-test
```

El resultado se escribe por defecto en `%TEMP%\libro-selftest.log` (o en `LIBRO_SELFTEST_LOG`).

### Modo desarrollo

Para pruebas de interfaz con datos de demostración:

```powershell
$env:LIBRO_DEV = "1"
$env:LIBRO_DATA_DIR = "$env:TEMP\libro-dev"
& .\desktop\Libro.Desktop\bin\Debug\net8.0-windows\Libro.exe --dev
```

La cuenta de demostración es `admin@libro.local` / `Secreta123!`. El modo `--dev` solo funciona con `LIBRO_DEV=1` y no debe usarse con datos reales.

### Herramienta de diagnóstico

`tools\Libro.DbTool` es una utilidad de consola para inspeccionar una carpeta de datos cifrada (empresa, usuarios y conteos). No forma parte del instalador. También puede restablecer una contraseña sobre una **copia** de la base; úsala con cuidado y nunca sobre datos reales sin autorización.

## Estructura

```text
desktop/
  Libro.Desktop/
    App.xaml.cs           arranque, login, modo dev y autoprueba
    Data/                 SQLCipher, DPAPI, esquema, scrypt, respaldos
    Services/             autenticación, empresa, movimientos, facturas, nómina...
    Views/                pantallas WPF y diálogos
    Resources/            estilos y plantillas
installer/
  libro-native.iss        instalador Inno Setup nativo
  build-native.ps1        publicación + autoprueba + instalador + SHA256
  libro.ico               icono de la aplicación
tools/
  Libro.DbTool/           diagnóstico local (no se instala)
src/                      aplicación anterior Next.js (solo referencia)
dist/                     instaladores
backups/                  copias de seguridad de desarrollo/migración
```

## Alcance y limitaciones

Esta herramienta es de **control interno** y apoyo operativo:

- Las facturas son documentos internos. **No timbra CFDI**, no usa PAC y no sustituye comprobantes fiscales.
- La versión nativa 2.0.0 **no imprime facturas** ni genera PDF. El README y las notas de versión anteriores anunciaban impresión; es una limitación conocida (véase [`docs/13-hallazgos-riesgos.md`](docs/13-hallazgos-riesgos.md#h-01--impresión-de-facturas-anunciada-sin-implementación)).
- Los impuestos se calculan con los datos capturados (IVA trasladado/acreditable). No considera retenciones, notas de crédito fiscales, importaciones, DIOT, declaraciones anuales ni el esquema real del SAT.
- La nómina es una estimación: ISR con tarifa mensual configurable (sin subsidio al empleo), IMSS con tasa simplificada. No genera CFDI de nómina ni cálculo oficial de cuotas.
- El costo de ventas es una referencia con costo promedio al facturar; no es un costeo formal.
- No hay conciliación bancaria automática.
- Es una aplicación local de Windows; no incluye acceso por red local ni interfaz web.
- El instalador no está firmado digitalmente; Windows puede mostrar SmartScreen.
- La llave DPAPI está ligada a la cuenta de Windows. Los respaldos no son portables a otro perfil/equipo sin esa llave y ese perfil.

Antes de presentar declaraciones o tomar decisiones fiscales, valida las cifras con tu contador.

## Aplicación anterior (legacy)

El código Next.js en `src/`, el instalador `installer\libro.iss` y `dist\Libro-Setup-1.0.0.exe` se conservan únicamente como referencia histórica. **No los instales sobre la versión nativa 2.0.0**: comparten el mismo identificador de aplicación y podrían reemplazar archivos de la aplicación nativa. La versión vigente es la 2.0.0.

---

Proyecto privado. Hecho para uso local; revisa y adapta la configuración fiscal con un profesional antes de usarlo en producción.

## CLI para agentes

El proyecto incluye `agent-harness\`, un CLI nativo en C#/.NET 8 que usa los mismos servicios, base SQLCipher y permisos que la aplicación WPF. Sirve para automatización y agentes, con salida JSON, REPL, esquemas de entrada y pruebas end-to-end en una carpeta temporal.

- Launcher: `agent-harness\libro-cli.cmd`
- Documentación: `agent-harness\README.md`
- Plan y resultados de pruebas: `agent-harness\TEST.md`
- Skill generada: `agent-harness\skills\cli-anything-libro\SKILL.md`

Ejemplo:

```powershell
.\agent-harness\libro-cli.cmd --json doctor
.\agent-harness\libro-cli.cmd --json dashboard
```

No requiere Node.js, navegador ni servidor HTTP. Las facturas siguen siendo documentos internos, no CFDI.