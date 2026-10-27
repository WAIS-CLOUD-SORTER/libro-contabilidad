# Libro 2.1.0 — Notas de la versión

**Fecha:** 26 de octubre de 2026  
**Instalador:** `Libro-Setup-2.1.0.exe`  
**Tamaño:** 58,170,838 bytes (55.5 MiB)  
**SHA256:** `292D6346F91597B79AC43043652BBA8BEEE0C0EC3DD21E351A1D674DF225B686`

## Resumen

Libro 2.1.0 continúa la aplicación nativa de Windows presentada en 2.0.0 y añade ventas desde el teléfono, un adaptador de cobro con Mercado Pago Point y una app Android opcional. Sigue siendo una aplicación **local**: los datos viven en una base SQLCipher cifrada, la llave se protege con DPAPI y no hay servicios en la nube ni telemetría.

## Novedades

- **Ventas por celular.** Servidor HTTP opcional, apagado por defecto y limitado a la red privada. PIN de 6 dígitos por arranque, token por dispositivo (solo se guarda su hash) y sesión ligada a la app de escritorio.
- **App Android Libro Ventas.** Envoltorio WebView de la misma interfaz de ventas; guarda la dirección del servidor y permite HTTP local. Se distribuye como APK aparte.
- **Mercado Pago Point.** Credenciales capturadas en Configuración y cifradas con DPAPI; el adaptador usa la Orders API (`POST /v1/orders`).
- **Atajos de teclado** y diálogo de ayuda.
- **Estados vacíos** en el Panel y aviso de alcance en la documentación.
- **Windows 10/11**: manifiesto de compatibilidad y verificación del instalador.
- **Correcciones de robustez:** una base cifrada sin `clave.bin` ya no genera una llave nueva (aborta con mensaje de recuperación); recalcular nómina elimina conceptos de empleados dados de baja; el build rechaza bases, llaves y respaldos en el staging.

## Requisitos

- Windows 10 u 11, 64 bits.
- No hace falta instalar .NET: el instalador incluye el runtime.
- Instalación por usuario en `%LOCALAPPDATA%\Programs\Libro`; no requiere administrador.

## Instalación y verificación

1. Descarga `Libro-Setup-2.1.0.exe`.
2. Verifica el SHA256:

   ```powershell
   Get-FileHash .\Libro-Setup-2.1.0.exe -Algorithm SHA256
   ```

   Debe coincidir con `292D6346F91597B79AC43043652BBA8BEEE0C0EC3DD21E351A1D674DF225B686`.
3. Ejecuta el instalador. Como no está firmado digitalmente, SmartScreen puede advertir; usa **Más información → Ejecutar de todas formas** solo si el hash coincide.
4. Actualiza sobre 2.0.0 sin perder datos: se conserva `%LOCALAPPDATA%\Libro\data`.

## Limitaciones conocidas

- Las facturas son documentos internos: **no son CFDI** y no se timbran.
- La app no imprime facturas ni genera PDF.
- **Mercado Pago Point no se ha validado contra el servicio real ni con dinero real.** Sin credenciales se usa una terminal simulada. Confirma el contrato de la Orders API antes de cobrar.
- El instalador no está firmado; Windows puede mostrar SmartScreen.
- La llave DPAPI está ligada a la cuenta y al perfil de Windows. Conserva `clave.bin` y una copia segura de la carpeta de datos.
- Las ventas por celular requieren la PC encendida, Libro abierto y la misma red Wi-Fi; el canal local es HTTP, protegido por PIN y token.

## Documentación

- [`README.md`](README.md) — instalación, operación y arquitectura.
- [`docs/README.md`](docs/README.md) — índice de la documentación.
- [`docs/13-hallazgos-riesgos.md`](docs/13-hallazgos-riesgos.md) — hallazgos y limitaciones.

## Código fuente

El código de esta versión está en la rama `master` de este repositorio. El código Next.js de `src/` y el instalador 1.0.0 se conservan solo como referencia histórica; no deben instalarse sobre la versión nativa.
