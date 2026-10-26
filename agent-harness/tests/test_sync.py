#!/usr/bin/env python3
"""Pruebas end-to-end del servidor de ventas para el teléfono (Sync).

A diferencia de `test_cli.py`, estas pruebas no usan el CLI para hablar con la
base: levantan la aplicación WPF real con el servidor activo y ejercitan la API
HTTP como lo haría un teléfono en la red local.

Se saltan si no existe el ejecutable de Debug, para no romper una suite en un
equipo donde sólo se compiló el CLI.

Ejecutar:
    python -m unittest tests.test_sync -v
    python -m unittest discover -s tests -v
"""

from __future__ import annotations

import json
import os
import shutil
import socket
import subprocess
import tempfile
import time
import unittest
import urllib.error
import urllib.request
from pathlib import Path

HARNESS = Path(__file__).resolve().parents[1]
ROOT = HARNESS.parent
EXE = ROOT / "desktop" / "Libro.Desktop" / "bin" / "Debug" / "net8.0-windows" / "Libro.exe"
DLL = HARNESS / "Libro.Cli" / "bin" / "Release" / "net8.0-windows" / "libro-cli.dll"

LOCALAPPDATA = Path(os.environ.get("LOCALAPPDATA", ""))
CANDIDATE_DOTNET = LOCALAPPDATA / "Microsoft" / "dotnet" / "dotnet.exe"
DOTNET = CANDIDATE_DOTNET if CANDIDATE_DOTNET.exists() else Path(shutil.which("dotnet") or "dotnet")

DEFAULT_DATA_DIR = LOCALAPPDATA / "Libro" / "data"

ADMIN_EMAIL = "admin@libro.local"
ADMIN_PASSWORD = "Secreta123!"
PIN = "135790"


def _free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as probe:
        probe.bind(("127.0.0.1", 0))
        return int(probe.getsockname()[1])


def _call(port: int, method: str, path: str, body=None, token: str | None = None):
    """Devuelve (status, payload). Los errores HTTP se devuelven, no se lanzan."""
    data = json.dumps(body).encode("utf-8") if body is not None else None
    request = urllib.request.Request(f"http://127.0.0.1:{port}{path}", data=data, method=method)
    if data is not None:
        request.add_header("Content-Type", "application/json")
    if token:
        request.add_header("Authorization", f"Bearer {token}")

    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            raw = response.read().decode("utf-8")
            return response.status, json.loads(raw) if raw else {}
    except urllib.error.HTTPError as error:
        raw = error.read().decode("utf-8")
        return error.code, json.loads(raw) if raw else {}


class SyncServerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        if not EXE.exists():
            raise unittest.SkipTest(
                f"No existe {EXE}.\nCompila primero: "
                f'dotnet build desktop\\Libro.Desktop\\Libro.Desktop.csproj -c Debug'
            )
        if not DLL.exists():
            raise unittest.SkipTest(
                f"No existe {DLL}.\n"
                "Compila primero con: agent-harness\\build.ps1"
            )

        cls.data_dir = Path(tempfile.mkdtemp(prefix="libro-sync-tests-"))
        if cls.data_dir.resolve() == DEFAULT_DATA_DIR.resolve():
            raise RuntimeError("La carpeta de pruebas coincide con los datos reales de Libro.")
        if not str(cls.data_dir.resolve()).lower().startswith(
            str(Path(tempfile.gettempdir()).resolve()).lower()
        ):
            raise RuntimeError(f"La carpeta de pruebas no está en TEMP: {cls.data_dir}")

        cls.env = os.environ.copy()
        for name in ("LIBRO_DATA_DIR", "LIBRO_EMAIL", "LIBRO_PASSWORD", "LIBRO_SYNC", "LIBRO_SYNC_PIN"):
            cls.env.pop(name, None)

        cls._seed()
        cls.port = _free_port()
        cls._start_app()

        status, _ = _call(cls.port, "GET", "/api/health")
        if status != 200:
            raise RuntimeError(f"/api/health respondió {status}; el servidor no está sano.")

        status, payload = _call(cls.port, "POST", "/api/pair",
                                {"pin": PIN, "deviceName": "Teléfono de prueba"})
        if status != 200:
            raise RuntimeError(f"El emparejamiento falló con {status}: {payload}")
        cls.token = payload["token"]

    @classmethod
    def _cli(cls, *args: str, auth: bool = True) -> subprocess.CompletedProcess[str]:
        # Las opciones globales van antes del grupo de comando; si se colocan
        # después, el CLI las interpreta como opciones de la operación.
        command = [str(DOTNET), str(DLL), "--data-dir", str(cls.data_dir), "--json"]
        if auth:
            command += ["--email", ADMIN_EMAIL, "--password", ADMIN_PASSWORD]
        command += list(args)
        return subprocess.run(
            command, capture_output=True, text=True, encoding="utf-8",
            errors="replace", timeout=120, env=cls.env, stdin=subprocess.DEVNULL,
        )

    @classmethod
    def _seed(cls) -> None:
        setup = cls._cli(
            "auth", "setup",
            "--company", "Negocio Sync",
            "--name", "Admin Sync",
            "--email", ADMIN_EMAIL,
            "--password", ADMIN_PASSWORD,
            auth=False,
        )
        if setup.returncode != 0:
            raise RuntimeError(f"No se pudo preparar la empresa de prueba: {setup.stderr}")

        def write(name: str, value: dict) -> str:
            path = cls.data_dir / name
            path.write_text(json.dumps(value, ensure_ascii=False), encoding="utf-8")
            return str(path)

        contact = write("contacto.json", {"type": "cliente", "name": "Cliente Sync", "rfc": "XAXX010101000"})
        product = write("producto.json", {
            "name": "Producto Sync", "sku": "SYNC-1", "unit": "pieza",
            "priceCents": 10000, "costCents": 5000, "ivaRate": 0.16,
            "minStock": 1, "trackStock": True, "initialStock": 10,
        })
        for args in (("contacts", "save", "--input", contact), ("products", "save", "--input", product)):
            result = cls._cli(*args)
            if result.returncode != 0:
                raise RuntimeError(f"Falló {' '.join(args[:2])}: {result.stderr}")

    @classmethod
    def _start_app(cls) -> None:
        run_env = cls.env.copy()
        run_env["LIBRO_DEV"] = "1"
        run_env["LIBRO_DATA_DIR"] = str(cls.data_dir)
        run_env["LIBRO_SYNC"] = str(cls.port)
        run_env["LIBRO_SYNC_PIN"] = PIN
        run_env["DOTNET_ROOT"] = str(DOTNET.parent)

        cls.proc = subprocess.Popen(
            [str(EXE), "--dev"], env=run_env,
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        )

        deadline = time.time() + 60
        while time.time() < deadline:
            if cls.proc.poll() is not None:
                raise RuntimeError(
                    f"La aplicación terminó con código {cls.proc.returncode} antes de abrir el puerto."
                )
            try:
                status, _ = _call(cls.port, "GET", "/api/health")
                if status == 200:
                    return
            except (urllib.error.URLError, ConnectionError, OSError):
                time.sleep(0.5)
        raise RuntimeError("El servidor de ventas no abrió el puerto a tiempo.")

    @classmethod
    def tearDownClass(cls) -> None:
        process = getattr(cls, "proc", None)
        if process is not None and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
        if os.environ.get("LIBRO_KEEP_TEST_DATA") == "1":
            print(f"Se conserva la carpeta de pruebas: {cls.data_dir}")
            return
        data_dir = getattr(cls, "data_dir", None)
        if data_dir is not None:
            shutil.rmtree(data_dir, ignore_errors=True)

    # ------------------------------------------------------------------ pruebas

    def test_01_health_es_publico(self):
        status, payload = _call(self.port, "GET", "/api/health")
        self.assertEqual(status, 200)
        self.assertEqual(payload["app"], "Libro")
        self.assertTrue(payload["session"], "La sesión de escritorio debe estar activa en modo --dev.")

    def test_02_sin_token_no_hay_datos(self):
        status, payload = _call(self.port, "GET", "/api/catalog")
        self.assertEqual(status, 401, payload)
        self.assertIn("emparejado", payload["error"].lower())

    def test_03_token_invalido_es_rechazado(self):
        status, _ = _call(self.port, "GET", "/api/catalog", token="token-que-no-existe")
        self.assertEqual(status, 401)

    def test_04_pin_incorrecto_es_rechazado(self):
        status, payload = _call(self.port, "POST", "/api/pair",
                                {"pin": "000000", "deviceName": "Intruso"})
        self.assertEqual(status, 401, payload)
        self.assertFalse(payload.get("token"))

    def test_05_catalogo_trae_datos_sembrados(self):
        status, payload = _call(self.port, "GET", "/api/catalog", token=self.token)
        self.assertEqual(status, 200)
        self.assertEqual(payload["company"], "Negocio Sync")
        self.assertEqual([p["name"] for p in payload["products"]], ["Producto Sync"])
        self.assertEqual([c["name"] for c in payload["contacts"]], ["Cliente Sync"])
        self.assertGreaterEqual(len(payload["accounts"]), 1)

    def test_06_venta_en_efectivo_cobra_y_calcula_iva(self):
        _, catalog = _call(self.port, "GET", "/api/catalog", token=self.token)
        product = catalog["products"][0]
        contact = catalog["contacts"][0]
        account = catalog["accounts"][0]

        status, sale = _call(self.port, "POST", "/api/sales", {
            "contactId": contact["id"],
            "lines": [{
                "productId": product["id"], "description": product["name"],
                "qty": 2, "unitPriceCents": 10000, "ivaRate": 0.16,
            }],
            "payment": {"method": "efectivo", "accountId": account["id"], "requireTerminal": False},
        }, token=self.token)

        self.assertEqual(status, 200, sale)
        # 2 x 100.00 = 200.00, IVA 16% = 32.00, total 232.00
        self.assertEqual(sale["totalCents"], 23200)
        self.assertTrue(sale["paid"], "La venta en efectivo debe quedar cobrada.")
        self.assertEqual(sale["status"], "pagada")
        self.assertRegex(sale["folio"], r"^[A-Z]-\d+$")

    def test_07_venta_con_tarjeta_usa_la_terminal(self):
        _, catalog = _call(self.port, "GET", "/api/catalog", token=self.token)
        product = catalog["products"][0]
        contact = catalog["contacts"][0]

        status, sale = _call(self.port, "POST", "/api/sales", {
            "contactId": contact["id"],
            "lines": [{
                "productId": product["id"], "description": product["name"],
                "qty": 1, "unitPriceCents": 10000, "ivaRate": 0.16,
            }],
            "payment": {"method": "tarjeta", "requireTerminal": False},
        }, token=self.token)

        self.assertEqual(status, 200, sale)
        self.assertTrue(sale["paid"])
        self.assertTrue(sale["terminal"], "Debe informar qué terminal se usó.")
        self.assertTrue(sale["terminalReference"], "Debe devolver la referencia del cobro.")

    def test_08_venta_sin_conceptos_es_invalida(self):
        _, catalog = _call(self.port, "GET", "/api/catalog", token=self.token)
        status, payload = _call(self.port, "POST", "/api/sales", {
            "contactId": catalog["contacts"][0]["id"], "lines": [],
        }, token=self.token)
        self.assertEqual(status, 400, payload)
        self.assertIn("conceptos", payload["error"].lower())

    def test_09_resumen_del_dia(self):
        status, payload = _call(self.port, "GET", "/api/sales/today", token=self.token)
        self.assertEqual(status, 200)
        self.assertGreaterEqual(payload["count"], 2)
        self.assertGreater(payload["totalCents"], 0)
        for row in payload["rows"]:
            self.assertIn("folio", row)

    def test_10_la_pagina_del_telefono_se_sirve(self):
        with urllib.request.urlopen(f"http://127.0.0.1:{self.port}/", timeout=15) as response:
            html = response.read().decode("utf-8")
        self.assertEqual(response.status, 200)
        self.assertIn("Libro · Ventas", html)
        self.assertIn("/api/sales", html)
        # La página no debe traer credenciales incrustadas.
        self.assertNotIn(PIN, html)


if __name__ == "__main__":
    unittest.main(verbosity=2)
